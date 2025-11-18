using System.Collections.Concurrent;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;
using HakoJS.VM;
using Raylib_cs;
using PortAudioSharp;

namespace raylib;

public class Program
{
    private const int MainThreadTickRate = 8;
    private const string ScriptFileName = "demo.ts";
    private static readonly ConcurrentQueue<Action> MainThreadQueue = new();
    private static readonly ManualResetEventSlim HasWork = new(false);
    private static volatile bool _isRunning = true;

    [System.STAThread]
    public static async Task Main(string[] args)
    {
        using var runtime = Hako.Initialize<WasmtimeEngine>();
        runtime.ConfigureModules()
            .WithModule<RaylibModule>()
            .WithModule<AudioModule>()
            .WithModule<TerrainModule>()
            .WithModule<MathModule>()
            .Apply();

        File.WriteAllText("raylib.d.ts", RaylibModule.TypeDefinition);
        File.WriteAllText("audio.d.ts", AudioModule.TypeDefinition);
        File.WriteAllText("terrain.d.ts", TerrainModule.TypeDefinition);
        File.WriteAllText("math.d.ts", MathModule.TypeDefinition);

        using var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole().WithTimers());
        var tsCode = File.ReadAllText(ScriptFileName);

        StartScriptExecution(realm, tsCode);
        RunMainThreadLoop();
        ProcessRemainingActions();

        await Hako.ShutdownAsync();
    }

    private static void StartScriptExecution(Realm realm, string code)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var result = await realm.EvalAsync(code, new()
                {
                    Type = EvalType.Module,
                    FileName = ScriptFileName
                });
            }
            catch (Exception ex) { Console.Error.WriteLine($"Script error: {ex}"); }
            finally { _isRunning = false; HasWork.Set(); }
        });
    }

    private static void RunMainThreadLoop()
    {
        while (_isRunning)
        {
            HasWork.Wait(TimeSpan.FromMilliseconds(MainThreadTickRate));
            HasWork.Reset();
            while (MainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Console.Error.WriteLine($"Action error: {ex}"); }
            }
        }
    }

    private static void ProcessRemainingActions()
    {
        while (MainThreadQueue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception ex) { Console.Error.WriteLine($"Action error: {ex}"); }
        }
    }

    internal static void RunOnMainThread(Action action)
    {
        using var done = new ManualResetEventSlim(false);
        Exception? ex = null;
        MainThreadQueue.Enqueue(() => { try { action(); } catch (Exception e) { ex = e; } finally { done.Set(); } });
        HasWork.Set();
        done.Wait();
        if (ex != null) throw ex;
    }

    internal static T RunOnMainThread<T>(Func<T> func)
    {
        using var done = new ManualResetEventSlim(false);
        Exception? ex = null;
        T? result = default;
        MainThreadQueue.Enqueue(() => { try { result = func(); } catch (Exception e) { ex = e; } finally { done.Set(); } });
        HasWork.Set();
        done.Wait();
        return ex != null ? throw ex : result!;
    }
}

#region Math Module (Quaternions & Smooth Damping)

[JSModule(Name = "math")]
[JSModuleInterface(InterfaceType = typeof(Quat), ExportName = "Quat")]
internal partial class MathModule
{
    [JSModuleMethod(Name = "quatIdentity")]
    public static Quat QuatIdentity() => new(0, 0, 0, 1);

    [JSModuleMethod(Name = "quatFromEuler")]
    public static Quat QuatFromEuler(double pitch, double yaw, double roll)
    {
        double cy = Math.Cos(yaw * 0.5);
        double sy = Math.Sin(yaw * 0.5);
        double cp = Math.Cos(pitch * 0.5);
        double sp = Math.Sin(pitch * 0.5);
        double cr = Math.Cos(roll * 0.5);
        double sr = Math.Sin(roll * 0.5);

        return new Quat(
            sr * cp * cy - cr * sp * sy,
            cr * sp * cy + sr * cp * sy,
            cr * cp * sy - sr * sp * cy,
            cr * cp * cy + sr * sp * sy
        );
    }

    [JSModuleMethod(Name = "quatFromAxisAngle")]
    public static Quat QuatFromAxisAngle(V3 axis, double angle)
    {
        double halfAngle = angle * 0.5;
        double s = Math.Sin(halfAngle);
        return new Quat(axis.X * s, axis.Y * s, axis.Z * s, Math.Cos(halfAngle));
    }

    [JSModuleMethod(Name = "quatMultiply")]
    public static Quat QuatMultiply(Quat q1, Quat q2)
    {
        return new Quat(
            q1.W * q2.X + q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y,
            q1.W * q2.Y + q1.Y * q2.W + q1.Z * q2.X - q1.X * q2.Z,
            q1.W * q2.Z + q1.Z * q2.W + q1.X * q2.Y - q1.Y * q2.X,
            q1.W * q2.W - q1.X * q2.X - q1.Y * q2.Y - q1.Z * q2.Z
        );
    }

    [JSModuleMethod(Name = "quatRotateVector")]
    public static V3 QuatRotateVector(V3 v, Quat q)
    {
        var qv = new Quat(v.X, v.Y, v.Z, 0);
        var qConj = new Quat(-q.X, -q.Y, -q.Z, q.W);
        var result = QuatMultiply(QuatMultiply(q, qv), qConj);
        return new V3(result.X, result.Y, result.Z);
    }

    [JSModuleMethod(Name = "quatSlerp")]
    public static Quat QuatSlerp(Quat q1, Quat q2, double t)
    {
        double dot = q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
        
        if (dot < 0.0)
        {
            q2 = new Quat(-q2.X, -q2.Y, -q2.Z, -q2.W);
            dot = -dot;
        }

        if (dot > 0.9995)
        {
            return new Quat(
                q1.X + t * (q2.X - q1.X),
                q1.Y + t * (q2.Y - q1.Y),
                q1.Z + t * (q2.Z - q1.Z),
                q1.W + t * (q2.W - q1.W)
            );
        }

        double theta = Math.Acos(dot);
        double sinTheta = Math.Sin(theta);
        double w1 = Math.Sin((1 - t) * theta) / sinTheta;
        double w2 = Math.Sin(t * theta) / sinTheta;

        return new Quat(
            q1.X * w1 + q2.X * w2,
            q1.Y * w1 + q2.Y * w2,
            q1.Z * w1 + q2.Z * w2,
            q1.W * w1 + q2.W * w2
        );
    }

    [JSModuleMethod(Name = "smoothDampFloat")]
    public static double SmoothDampFloat(double from, double to, double speed, double dt)
    {
        return from + (to - from) * (1 - Math.Exp(-speed * dt));
    }

    [JSModuleMethod(Name = "smoothDampV3")]
    public static V3 SmoothDampV3(V3 from, V3 to, double speed, double dt)
    {
        double factor = 1 - Math.Exp(-speed * dt);
        return new V3(
            from.X + (to.X - from.X) * factor,
            from.Y + (to.Y - from.Y) * factor,
            from.Z + (to.Z - from.Z) * factor
        );
    }

    [JSModuleMethod(Name = "smoothDampQuat")]
    public static Quat SmoothDampQuat(Quat from, Quat to, double speed, double dt)
    {
        double t = 1 - Math.Exp(-speed * dt);
        return QuatSlerp(from, to, t);
    }

    [JSModuleMethod(Name = "v3Add")]
    public static V3 V3Add(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [JSModuleMethod(Name = "v3Scale")]
    public static V3 V3Scale(V3 v, double s) => new(v.X * s, v.Y * s, v.Z * s);

    [JSModuleMethod(Name = "v3Length")]
    public static double V3Length(V3 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    [JSModuleMethod(Name = "v3Distance")]
    public static double V3Distance(V3 a, V3 b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    [JSModuleMethod(Name = "v3Normalize")]
    public static V3 V3Normalize(V3 v)
    {
        double len = V3Length(v);
        return len > 0 ? new V3(v.X / len, v.Y / len, v.Z / len) : v;
    }

    [JSModuleMethod(Name = "clamp")]
    public static double Clamp(double value, double min, double max) => 
        Math.Max(min, Math.Min(max, value));

    [JSModuleMethod(Name = "degToRad")]
    public static double DegToRad(double deg) => deg * Math.PI / 180.0;
}

[JSObject]
internal partial record Quat(double X, double Y, double Z, double W);

#endregion

#region Terrain Module

[JSModule(Name = "terrain")]
[JSModuleInterface(InterfaceType = typeof(Chunk), ExportName = "Chunk")]
[JSModuleInterface(InterfaceType = typeof(Block), ExportName = "Block")]
internal partial class TerrainModule
{
    private static readonly ConcurrentDictionary<string, Chunk> _cache = new();
    private static readonly ConcurrentDictionary<string, float> _heightCache = new();
    private static int[] _perm = new int[512];
    private const int MaxCacheSize = 200;

    [JSModuleMethod(Name = "setSeed")]
    public static void SetSeed(int seed)
    {
        _cache.Clear();
        _heightCache.Clear();
        var rng = new Random(seed);
        var p = Enumerable.Range(0, 256).OrderBy(_ => rng.Next()).ToArray();
        for (int i = 0; i < 256; i++) { _perm[i] = p[i]; _perm[i + 256] = p[i]; }
    }

    [JSModuleMethod(Name = "preloadAsync")]
    public static async Task PreloadAsync(int cx, int cz, int radius, int size, double bs)
    {
        var tasks = new List<Task>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                int x = cx + dx, z = cz + dz;
                tasks.Add(Task.Run(() => GenChunk(x, z, size, bs)));
            }
        await Task.WhenAll(tasks);
    }

    [JSModuleMethod(Name = "getChunk")]
    public static Chunk GetChunk(int cx, int cz, int size, double bs)
    {
        string key = $"{cx},{cz}";
        return _cache.TryGetValue(key, out var chunk) ? chunk : GenChunk(cx, cz, size, bs);
    }

    private static Chunk GenChunk(int cx, int cz, int size, double bs)
    {
        string key = $"{cx},{cz}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        if (_cache.Count > MaxCacheSize)
        {
            var toRemove = _cache.Keys.Take(_cache.Count - MaxCacheSize / 2).ToList();
            foreach (var k in toRemove) _cache.TryRemove(k, out _);
        }

        var heights = new float[size * size];
        if (Avx2.IsSupported) GenSIMD(cx, cz, size, bs, heights);
        else GenScalar(cx, cz, size, bs, heights);

        var blocks = new Block[size * size];
        int idx = 0;
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                float h = heights[z * size + x];
                double wx = (cx * size + x) * bs;
                double wz = (cz * size + z) * bs;
                var (r, g, b) = GetColor(h);
                blocks[idx++] = new Block(wx, h, wz, r, g, b);
            }

        var chunk = new Chunk(cx, cz, blocks);
        _cache[key] = chunk;
        return chunk;
    }

    private static void GenSIMD(int cx, int cz, int size, double bs, float[] heights)
    {
        Parallel.For(0, size, z =>
        {
            int wz = cz * size + z;
            float fz = (float)(wz * bs);
            for (int x = 0; x < size; x += 8)
            {
                int wx = cx * size + x;
                var vx = Vector256.Create(
                    (float)((wx + 0) * bs), (float)((wx + 1) * bs), (float)((wx + 2) * bs), (float)((wx + 3) * bs),
                    (float)((wx + 4) * bs), (float)((wx + 5) * bs), (float)((wx + 6) * bs), (float)((wx + 7) * bs));
                var vz = Vector256.Create(fz);
                var h = Height(vx, vz);
                Span<float> hs = stackalloc float[8];
                h.CopyTo(hs);
                for (int i = 0; i < 8 && x + i < size; i++) heights[z * size + x + i] = hs[i];
            }
        });
    }

    private static void GenScalar(int cx, int cz, int size, double bs, float[] heights)
    {
        Parallel.For(0, size, z =>
        {
            for (int x = 0; x < size; x++)
            {
                int wx = cx * size + x, wz = cz * size + z;
                float fx = (float)(wx * bs), fz = (float)(wz * bs);
                heights[z * size + x] = CalcHeight(fx, fz);
            }
        });
    }

    private static float CalcHeight(float x, float z)
    {
        float h = Noise(x * 0.008f, z * 0.008f) * 50f;
        h += Noise(x * 0.02f, z * 0.02f) * 25f;
        h += Noise(x * 0.06f, z * 0.06f) * 8f;
        h += Noise(x * 0.003f, z * 0.003f) * 70f;
        return MathF.Max(5f, h + 30f);
    }

    private static Vector256<float> Height(Vector256<float> x, Vector256<float> z)
    {
        var h1 = Avx.Multiply(NoiseSIMD(Avx.Multiply(x, Vector256.Create(0.008f)), Avx.Multiply(z, Vector256.Create(0.008f))), Vector256.Create(50f));
        var h2 = Avx.Multiply(NoiseSIMD(Avx.Multiply(x, Vector256.Create(0.02f)), Avx.Multiply(z, Vector256.Create(0.02f))), Vector256.Create(25f));
        var h3 = Avx.Multiply(NoiseSIMD(Avx.Multiply(x, Vector256.Create(0.06f)), Avx.Multiply(z, Vector256.Create(0.06f))), Vector256.Create(8f));
        var h4 = Avx.Multiply(NoiseSIMD(Avx.Multiply(x, Vector256.Create(0.003f)), Avx.Multiply(z, Vector256.Create(0.003f))), Vector256.Create(70f));
        return Avx.Max(Avx.Add(Avx.Add(Avx.Add(h1, h2), Avx.Add(h3, h4)), Vector256.Create(30f)), Vector256.Create(5f));
    }

    private static Vector256<float> NoiseSIMD(Vector256<float> x, Vector256<float> z)
    {
        Span<float> xv = stackalloc float[8], zv = stackalloc float[8];
        x.CopyTo(xv); z.CopyTo(zv);
        return Vector256.Create(
            Noise(xv[0], zv[0]), Noise(xv[1], zv[1]), Noise(xv[2], zv[2]), Noise(xv[3], zv[3]),
            Noise(xv[4], zv[4]), Noise(xv[5], zv[5]), Noise(xv[6], zv[6]), Noise(xv[7], zv[7])
        );
    }

    private static float Noise(float x, float z)
    {
        int xi = ((int)MathF.Floor(x)) & 255, zi = ((int)MathF.Floor(z)) & 255;
        float xf = x - MathF.Floor(x), zf = z - MathF.Floor(z);
        float u = Fade(xf), v = Fade(zf);
        int aa = _perm[_perm[xi] + zi], ab = _perm[_perm[xi] + zi + 1];
        int ba = _perm[_perm[xi + 1] + zi], bb = _perm[_perm[xi + 1] + zi + 1];
        float x1 = Lerp(Grad(aa, xf, zf), Grad(ba, xf - 1, zf), u);
        float x2 = Lerp(Grad(ab, xf, zf - 1), Grad(bb, xf - 1, zf - 1), u);
        return Lerp(x1, x2, v);
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);
    private static float Grad(int h, float x, float z)
    {
        h &= 15; float g = 1.0f + (h & 7);
        if ((h & 8) != 0) g = -g;
        return ((h & 1) != 0 ? g * x : g * z);
    }

    private static (int r, int g, int b) GetColor(float h) => h switch
    {
        < 20 => (50, 100, 180),
        < 35 => (220, 200, 150),
        < 70 => (80, 160, 70),
        < 95 => (60, 120, 50),
        < 120 => (100, 100, 105),
        _ => (240, 240, 250)
    };

    [JSModuleMethod(Name = "getHeight")]
    public static double GetHeight(double x, double z)
    {
        string key = $"{(int)(x / 4)},{(int)(z / 4)}";
        if (_heightCache.TryGetValue(key, out var cached)) return cached;
        
        float h = CalcHeight((float)x, (float)z);
        _heightCache[key] = h;
        
        if (_heightCache.Count > 10000)
        {
            var toRemove = _heightCache.Keys.Take(5000).ToList();
            foreach (var k in toRemove) _heightCache.TryRemove(k, out _);
        }
        
        return h;
    }

    [JSModuleMethod(Name = "clear")]
    public static void Clear()
    {
        _cache.Clear();
        _heightCache.Clear();
    }
}

[JSObject]
internal partial record Chunk(int ChunkX, int ChunkZ, Block[] Blocks);

[JSObject]
internal partial record Block(double X, double Y, double Z, int R, int G, int B);

#endregion

#region Audio Module

[JSModule(Name = "audio")]
internal partial class AudioModule
{
    private static PortAudioSharp.Stream? _stream;
    private static bool _init = false;
    private static readonly ConcurrentQueue<Gen> _gens = new();
    private const int SR = 44100;

    private class Gen { public int Freq; public double Vol; public int Frames; public double Phase; }

    [JSModuleMethod(Name = "init")]
    public static void Init()
    {
        if (_init) return;
        try
        {
            PortAudio.Initialize();
            _stream = new PortAudioSharp.Stream(null, new StreamParameters
            {
                device = PortAudio.DefaultOutputDevice,
                channelCount = 1,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = 0.05
            }, SR, 512, StreamFlags.NoFlag, Callback, null);
            _stream.Start();
            _init = true;
        }
        catch (Exception ex) { Console.Error.WriteLine($"Audio init failed: {ex.Message}"); }
    }

    private static StreamCallbackResult Callback(IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        unsafe
        {
            float* buf = (float*)output;
            for (int i = 0; i < frameCount; i++) buf[i] = 0f;
            var remove = new List<Gen>();
            foreach (var g in _gens)
            {
                if (g.Frames <= 0) { remove.Add(g); continue; }
                int n = Math.Min((int)frameCount, g.Frames);
                for (int i = 0; i < n; i++)
                {
                    buf[i] += (float)(Math.Sin(2.0 * Math.PI * g.Phase) * g.Vol);
                    g.Phase += g.Freq / (double)SR;
                    if (g.Phase >= 1.0) g.Phase -= 1.0;
                }
                g.Frames -= n;
            }
            foreach (var g in remove) _gens.TryDequeue(out _);
        }
        return StreamCallbackResult.Continue;
    }

    [JSModuleMethod(Name = "play")]
    public static void Play(int freq, int ms, double vol)
    {
        if (!_init) Init();
        if (_gens.Count > 50) return;
        _gens.Enqueue(new Gen { Freq = freq, Vol = vol * 0.15, Frames = (int)(ms * SR / 1000.0) });
    }

    [JSModuleMethod(Name = "stop")]
    public static void Stop() { while (_gens.TryDequeue(out _)) { } }

    [JSModuleMethod(Name = "shutdown")]
    public static void Shutdown()
    {
        _stream?.Stop(); _stream?.Dispose(); _stream = null;
        Stop(); PortAudio.Terminate(); _init = false;
    }
}

#endregion

#region Raylib Module

[JSModule(Name = "raylib")]
[JSModuleInterface(InterfaceType = typeof(V3), ExportName = "V3")]
[JSModuleInterface(InterfaceType = typeof(Cam), ExportName = "Cam")]
[JSModuleInterface(InterfaceType = typeof(Col), ExportName = "Col")]
internal partial class RaylibModule
{
    [JSModuleMethod(Name = "init")]
    public static void Init(int w, int h, string t) =>
        Program.RunOnMainThread(() => 
        {
            Raylib.InitWindow(w, h, t);
            Raylib.SetWindowState(ConfigFlags.VSyncHint);
        });

    [JSModuleMethod(Name = "close")]
    public static void Close() => Program.RunOnMainThread(Raylib.CloseWindow);

    [JSModuleMethod(Name = "shouldClose")]
    public static bool ShouldClose() => Program.RunOnMainThread(Raylib.WindowShouldClose);

    [JSModuleMethod(Name = "setFPS")]
    public static void SetFPS(int fps) => Program.RunOnMainThread(() => Raylib.SetTargetFPS(fps));

    [JSModuleMethod(Name = "beginDraw")]
    public static void BeginDraw() => Program.RunOnMainThread(Raylib.BeginDrawing);

    [JSModuleMethod(Name = "endDraw")]
    public static void EndDraw() => Program.RunOnMainThread(Raylib.EndDrawing);

    [JSModuleMethod(Name = "clear")]
    public static void Clear(Col c) =>
        Program.RunOnMainThread(() => Raylib.ClearBackground(new Raylib_cs.Color(c.R, c.G, c.B, c.A)));

    [JSModuleMethod(Name = "text")]
    public static void Text(string t, int x, int y, int s, Col c) =>
        Program.RunOnMainThread(() => Raylib.DrawText(t, x, y, s, new Raylib_cs.Color(c.R, c.G, c.B, c.A)));

    [JSModuleMethod(Name = "rect")]
    public static void Rect(int x, int y, int w, int h, Col c) =>
        Program.RunOnMainThread(() => Raylib.DrawRectangle(x, y, w, h, new Raylib_cs.Color(c.R, c.G, c.B, c.A)));

    [JSModuleMethod(Name = "begin3D")]
    public static void Begin3D(Cam cam) =>
        Program.RunOnMainThread(() => Raylib.BeginMode3D(new Raylib_cs.Camera3D
        {
            Position = new System.Numerics.Vector3((float)cam.Pos.X, (float)cam.Pos.Y, (float)cam.Pos.Z),
            Target = new System.Numerics.Vector3((float)cam.Tar.X, (float)cam.Tar.Y, (float)cam.Tar.Z),
            Up = new System.Numerics.Vector3((float)cam.Up.X, (float)cam.Up.Y, (float)cam.Up.Z),
            FovY = (float)cam.Fov,
            Projection = CameraProjection.Perspective
        }));

    [JSModuleMethod(Name = "end3D")]
    public static void End3D() => Program.RunOnMainThread(Raylib.EndMode3D);

    [JSModuleMethod(Name = "cube")]
    public static void Cube(V3 p, double w, double h, double l, Col c) =>
        Program.RunOnMainThread(() => Raylib.DrawCube(
            new System.Numerics.Vector3((float)p.X, (float)p.Y, (float)p.Z),
            (float)w, (float)h, (float)l, new Raylib_cs.Color(c.R, c.G, c.B, c.A)));

    [JSModuleMethod(Name = "isKeyDown")]
    public static bool IsKeyDown(int k) => Program.RunOnMainThread(() => Raylib.IsKeyDown((KeyboardKey)k));

    [JSModuleMethod(Name = "isKeyPressed")]
    public static bool IsKeyPressed(int k) => Program.RunOnMainThread(() => Raylib.IsKeyPressed((KeyboardKey)k));

    [JSModuleValue(Name = "KEY_W")] public static int KEY_W => 87;
    [JSModuleValue(Name = "KEY_A")] public static int KEY_A => 65;
    [JSModuleValue(Name = "KEY_S")] public static int KEY_S => 83;
    [JSModuleValue(Name = "KEY_D")] public static int KEY_D => 68;
    [JSModuleValue(Name = "KEY_SPACE")] public static int KEY_SPACE => 32;
    [JSModuleValue(Name = "KEY_UP")] public static int KEY_UP => 265;
    [JSModuleValue(Name = "KEY_DOWN")] public static int KEY_DOWN => 264;
    [JSModuleValue(Name = "KEY_LEFT")] public static int KEY_LEFT => 263;
    [JSModuleValue(Name = "KEY_RIGHT")] public static int KEY_RIGHT => 262;
    [JSModuleValue(Name = "KEY_ENTER")] public static int KEY_ENTER => 257;
    [JSModuleValue(Name = "KEY_Q")] public static int KEY_Q => 81;
    [JSModuleValue(Name = "KEY_E")] public static int KEY_E => 69;
}

[JSObject(ReadOnly = false)]
internal partial record V3(double X, double Y, double Z);

[JSObject(ReadOnly = false)]
internal partial record Cam(V3 Pos, V3 Tar, V3 Up, double Fov);

[JSObject(ReadOnly = false)]
internal partial record Col(int R, int G, int B, int A);

#endregion