using System.Collections.Concurrent;
using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;
using HakoJS.VM;
using Raylib_cs;

namespace raylib;

public class Program
{
    private const int MainThreadTickRate = 16;
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
            .Apply();

        File.WriteAllText("raylib.d.ts" ,RaylibModule.TypeDefinition);

        using var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());
        var tsCode = File.ReadAllText(ScriptFileName);

        StartScriptExecution(realm, tsCode);
        RunMainThreadLoop();
        ProcessRemainingActions();
        
       
        await Hako.ShutdownAsync();

        Console.WriteLine("Application closed.");
    }

    private static void StartScriptExecution(Realm realm, string code)
    {
        _ = Task.Run(() =>
        {
            try
            {
                realm.EvalCode(code, new()
                {
                    Type = EvalType.Module,
                    FileName = ScriptFileName
                }).Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Script execution error: {ex}");
            }
            finally
            {
                _isRunning = false;
                HasWork.Set();
            }
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
                ExecuteAction(action);
            }
        }
    }

    private static void ProcessRemainingActions()
    {
        while (MainThreadQueue.TryDequeue(out var action))
        {
            ExecuteAction(action);
        }
    }

    private static void ExecuteAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing action: {ex}");
        }
    }

    internal static void RunOnMainThread(Action action)
    {
        using var completed = new ManualResetEventSlim(false);
        Exception? exception = null;

        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        HasWork.Set();
        completed.Wait();

        if (exception != null)
            throw exception;
    }

    internal static T RunOnMainThread<T>(Func<T> func)
    {
        using var completed = new ManualResetEventSlim(false);
        Exception? exception = null;
        T? result = default;

        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        HasWork.Set();
        completed.Wait();

        return exception != null ? throw exception : result!;
    }
}

#region Raylib Module

[JSModule(Name = "raylib")]
[JSModuleClass(ClassType = typeof(Vector2), ExportName = "Vector2")]
[JSModuleClass(ClassType = typeof(Vector3), ExportName = "Vector3")]
[JSModuleClass(ClassType = typeof(Camera3D), ExportName = "Camera3D")]
[JSModuleClass(ClassType = typeof(Color), ExportName = "Color")]
internal partial class RaylibModule
{
    // Window functions
    [JSModuleMethod(Name = "initWindow")]
    public static void InitWindow(int width, int height, string title) =>
        Program.RunOnMainThread(() => Raylib.InitWindow(width, height, title));

    [JSModuleMethod(Name = "closeWindow")]
    public static void CloseWindow() =>
        Program.RunOnMainThread(Raylib.CloseWindow);

    [JSModuleMethod(Name = "windowShouldClose")]
    public static bool WindowShouldClose() =>
        Program.RunOnMainThread(Raylib.WindowShouldClose);

    [JSModuleMethod(Name = "setTargetFPS")]
    public static void SetTargetFps(int fps) =>
        Program.RunOnMainThread(() => Raylib.SetTargetFPS(fps));

    [JSModuleMethod(Name = "getScreenWidth")]
    public static int GetScreenWidth() =>
        Program.RunOnMainThread(Raylib.GetScreenWidth);

    [JSModuleMethod(Name = "getScreenHeight")]
    public static int GetScreenHeight() =>
        Program.RunOnMainThread(Raylib.GetScreenHeight);

    // Drawing functions
    [JSModuleMethod(Name = "beginDrawing")]
    public static void BeginDrawing() =>
        Program.RunOnMainThread(Raylib.BeginDrawing);

    [JSModuleMethod(Name = "endDrawing")]
    public static void EndDrawing() =>
        Program.RunOnMainThread(Raylib.EndDrawing);

    [JSModuleMethod(Name = "clearBackground")]
    public static void ClearBackground(Color color)
    {
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.ClearBackground(nativeColor));
    }

    // 2D drawing
    [JSModuleMethod(Name = "drawCircleV")]
    public static void DrawCircleV(Vector2 center, double radius, Color color)
    {
        var nativeCenter = new System.Numerics.Vector2((float)center.X, (float)center.Y);
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawCircleV(nativeCenter, (float)radius, nativeColor));
    }

    [JSModuleMethod(Name = "drawText")]
    public static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawText(text, x, y, fontSize, nativeColor));
    }

    [JSModuleMethod(Name = "drawFPS")]
    public static void DrawFps(int x, int y) =>
        Program.RunOnMainThread(() => Raylib.DrawFPS(x, y));

    [JSModuleMethod(Name = "drawRectangle")]
    public static void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawRectangle(x, y, width, height, nativeColor));
    }

    [JSModuleMethod(Name = "drawRectangleLines")]
    public static void DrawRectangleLines(int x, int y, int width, int height, Color color)
    {
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawRectangleLines(x, y, width, height, nativeColor));
    }

    // 3D camera functions
    [JSModuleMethod(Name = "updateCamera")]
    public static void UpdateCamera(Camera3D camera, int cameraMode) =>
        Program.RunOnMainThread(() =>
        {
            var cam = (Raylib_cs.Camera3D)camera;
            Raylib.UpdateCamera(ref cam, (CameraMode)cameraMode);
            camera.UpdateFrom(cam);
        });

    [JSModuleMethod(Name = "beginMode3D")]
    public static void BeginMode3D(Camera3D camera)
    {
        var nativeCamera = new Raylib_cs.Camera3D
        {
            Position = new System.Numerics.Vector3((float)camera.Position.X, (float)camera.Position.Y, (float)camera.Position.Z),
            Target = new System.Numerics.Vector3((float)camera.Target.X, (float)camera.Target.Y, (float)camera.Target.Z),
            Up = new System.Numerics.Vector3((float)camera.Up.X, (float)camera.Up.Y, (float)camera.Up.Z),
            FovY = (float)camera.FovY,
            Projection = (CameraProjection)camera.Projection
        };
        Program.RunOnMainThread(() => Raylib.BeginMode3D(nativeCamera));
    }

    [JSModuleMethod(Name = "endMode3D")]
    public static void EndMode3D() =>
        Program.RunOnMainThread(Raylib.EndMode3D);

    // 3D drawing functions
    [JSModuleMethod(Name = "drawPlane")]
    public static void DrawPlane(Vector3 centerPos, Vector2 size, Color color)
    {
        var nativePos = new System.Numerics.Vector3((float)centerPos.X, (float)centerPos.Y, (float)centerPos.Z);
        var nativeSize = new System.Numerics.Vector2((float)size.X, (float)size.Y);
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawPlane(nativePos, nativeSize, nativeColor));
    }

    [JSModuleMethod(Name = "drawCube")]
    public static void DrawCube(Vector3 position, double width, double height, double length, Color color)
    {
        var nativePos = new System.Numerics.Vector3((float)position.X, (float)position.Y, (float)position.Z);
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawCube(nativePos, (float)width, (float)height, (float)length, nativeColor));
    }

    [JSModuleMethod(Name = "drawCubeWires")]
    public static void DrawCubeWires(Vector3 position, double width, double height, double length, Color color)
    {
        var nativePos = new System.Numerics.Vector3((float)position.X, (float)position.Y, (float)position.Z);
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        Program.RunOnMainThread(() => Raylib.DrawCubeWires(nativePos, (float)width, (float)height, (float)length, nativeColor));
    }

    // Utility functions
    [JSModuleMethod(Name = "getRandomValue")]
    public static int GetRandomValue(int min, int max) =>
        Program.RunOnMainThread(() => Raylib.GetRandomValue(min, max));

    [JSModuleMethod(Name = "colorAlpha")]
    public static Color ColorAlpha(Color color, double alpha)
    {
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        return Program.RunOnMainThread(() => Raylib.ColorAlpha(nativeColor, (float)alpha));
    }

    [JSModuleMethod(Name = "fade")]
    public static Color Fade(Color color, double alpha)
    {
        var nativeColor = new Raylib_cs.Color(color.R, color.G, color.B, color.A);
        return Program.RunOnMainThread(() => Raylib.Fade(nativeColor, (float)alpha));
    }
    


    // Input functions
    [JSModuleMethod(Name = "isKeyPressed")]
    public static bool IsKeyPressed(int key) =>
        Program.RunOnMainThread(() => Raylib.IsKeyPressed((KeyboardKey)key));
    
    [JSModuleMethod(Name = "isKeyDown")]
    public static bool IsKeyDown(int key) =>
        Program.RunOnMainThread(() => Raylib.IsKeyDown((KeyboardKey)key));
    
    [JSModuleMethod(Name = "isKeyPressedRepeat")]
    public static bool IsKeyPressedRepeat(int key) =>
        Program.RunOnMainThread(() => Raylib.IsKeyPressedRepeat((KeyboardKey)key));
    
    [JSModuleMethod(Name = "isKeyReleased")]
    public static bool IsKeyReleased(int key) =>
        Program.RunOnMainThread(() => Raylib.IsKeyReleased((KeyboardKey)key));
    
    [JSModuleMethod(Name = "isKeyUp")]
    public static bool IsKeyUp(int key) =>
        Program.RunOnMainThread(() => Raylib.IsKeyUp((KeyboardKey)key));

    // Keyboard keys
    [JSModuleValue(Name = "KEY_SPACE")]
    public static int KeySpace => (int)KeyboardKey.Space;

    [JSModuleValue(Name = "KEY_ONE")]
    public static int KeyOne => (int)KeyboardKey.One;

    [JSModuleValue(Name = "KEY_TWO")]
    public static int KeyTwo => (int)KeyboardKey.Two;

    [JSModuleValue(Name = "KEY_THREE")]
    public static int KeyThree => (int)KeyboardKey.Three;

    [JSModuleValue(Name = "KEY_FOUR")]
    public static int KeyFour => (int)KeyboardKey.Four;

    [JSModuleValue(Name = "KEY_P")]
    public static int KeyP => (int)KeyboardKey.P;

    [JSModuleValue(Name = "KEY_W")]
    public static int KeyW => (int)KeyboardKey.W;

    [JSModuleValue(Name = "KEY_A")]
    public static int KeyA => (int)KeyboardKey.A;

    [JSModuleValue(Name = "KEY_S")]
    public static int KeyS => (int)KeyboardKey.S;

    [JSModuleValue(Name = "KEY_D")]
    public static int KeyD => (int)KeyboardKey.D;
    
    [JSModuleValue(Name = "KEY_UP")]
    public static int KeyUp => (int)KeyboardKey.Up;
    
    [JSModuleValue(Name = "KEY_DOWN")]
    public static int KeyDown => (int)KeyboardKey.Down;
    
    [JSModuleValue(Name = "KEY_LEFT")]
    public static int KeyLeft => (int)KeyboardKey.Left;
    
    [JSModuleValue(Name = "KEY_RIGHT")]
    public static int KeyRight => (int)KeyboardKey.Right;

    // Camera modes
    [JSModuleValue(Name = "CAMERA_CUSTOM")]
    public static int CameraCustom => (int)CameraMode.Custom;

    [JSModuleValue(Name = "CAMERA_FREE")]
    public static int CameraFree => (int)CameraMode.Free;

    [JSModuleValue(Name = "CAMERA_FIRST_PERSON")]
    public static int CameraFirstPerson => (int)CameraMode.FirstPerson;

    [JSModuleValue(Name = "CAMERA_THIRD_PERSON")]
    public static int CameraThirdPerson => (int)CameraMode.ThirdPerson;

    [JSModuleValue(Name = "CAMERA_ORBITAL")]
    public static int CameraOrbital => (int)CameraMode.Orbital;

    // Camera projection types
    [JSModuleValue(Name = "CAMERA_PERSPECTIVE")]
    public static int CameraPerspective => (int)CameraProjection.Perspective;

    [JSModuleValue(Name = "CAMERA_ORTHOGRAPHIC")]
    public static int CameraOrthographic => (int)CameraProjection.Orthographic;

    // Colors - cache them as static readonly to avoid recreation
    private static readonly Color _white = new() { _color = Raylib_cs.Color.White };
    private static readonly Color _rayWhite = new() { _color = Raylib_cs.Color.RayWhite };
    private static readonly Color _lightGray = new() { _color = Raylib_cs.Color.LightGray };
    private static readonly Color _gray = new() { _color = Raylib_cs.Color.Gray };
    private static readonly Color _darkGray = new() { _color = Raylib_cs.Color.DarkGray };
    private static readonly Color _maroon = new() { _color = Raylib_cs.Color.Maroon };
    private static readonly Color _blue = new() { _color = Raylib_cs.Color.Blue };
    private static readonly Color _skyBlue = new() { _color = Raylib_cs.Color.SkyBlue };
    private static readonly Color _lime = new() { _color = Raylib_cs.Color.Lime };
    private static readonly Color _gold = new() { _color = Raylib_cs.Color.Gold };
    private static readonly Color _purple = new() { _color = Raylib_cs.Color.Purple };
    private static readonly Color _darkPurple = new() { _color = Raylib_cs.Color.DarkPurple };
    private static readonly Color _black = new() { _color = Raylib_cs.Color.Black };

    [JSModuleValue(Name = "WHITE")]
    public static Color White => _white;

    [JSModuleValue(Name = "RAYWHITE")]
    public static Color RayWhite => _rayWhite;

    [JSModuleValue(Name = "LIGHTGRAY")]
    public static Color LightGray => _lightGray;

    [JSModuleValue(Name = "GRAY")]
    public static Color Gray => _gray;

    [JSModuleValue(Name = "DARKGRAY")]
    public static Color DarkGray => _darkGray;

    [JSModuleValue(Name = "MAROON")]
    public static Color Maroon => _maroon;

    [JSModuleValue(Name = "BLUE")]
    public static Color Blue => _blue;

    [JSModuleValue(Name = "SKYBLUE")]
    public static Color SkyBlue => _skyBlue;

    [JSModuleValue(Name = "LIME")]
    public static Color Lime => _lime;

    [JSModuleValue(Name = "GOLD")]
    public static Color Gold => _gold;

    [JSModuleValue(Name = "PURPLE")]
    public static Color Purple => _purple;

    [JSModuleValue(Name = "DARKPURPLE")]
    public static Color DarkPurple => _darkPurple;

    [JSModuleValue(Name = "BLACK")]
    public static Color Black => _black;
}

#endregion

#region JavaScript Bridge Types

[JSClass(Name = "Vector2")]
[method: JSConstructor]
internal partial class Vector2(double x = 0, double y = 0)
{
    private System.Numerics.Vector2 _vec = new((float)x, (float)y);

    [JSProperty(Name = "x")]
    public double X
    {
        get => _vec.X;
        set => _vec.X = (float)value;
    }

    [JSProperty(Name = "y")]
    public double Y
    {
        get => _vec.Y;
        set => _vec.Y = (float)value;
    }

    public static implicit operator System.Numerics.Vector2(Vector2 v) => v._vec;
    public static implicit operator Vector2(System.Numerics.Vector2 v) => new() { _vec = v };
}

[JSClass(Name = "Vector3")]
[method: JSConstructor]
internal partial class Vector3(double x = 0, double y = 0, double z = 0)
{
    private System.Numerics.Vector3 _vec = new((float)x, (float)y, (float)z);

    [JSProperty(Name = "x")]
    public double X
    {
        get => _vec.X;
        set => _vec.X = (float)value;
    }

    [JSProperty(Name = "y")]
    public double Y
    {
        get => _vec.Y;
        set => _vec.Y = (float)value;
    }

    [JSProperty(Name = "z")]
    public double Z
    {
        get => _vec.Z;
        set => _vec.Z = (float)value;
    }

    public static implicit operator System.Numerics.Vector3(Vector3 v) => v._vec;
    public static implicit operator Vector3(System.Numerics.Vector3 v) => new() { _vec = v };
}

[JSClass(Name = "Camera3D")]
[method: JSConstructor]
internal partial class Camera3D()
{
    private Raylib_cs.Camera3D _camera = new();

    [JSProperty(Name = "position")]
    public Vector3 Position
    {
        get => _camera.Position;
        set => _camera.Position = value;
    }

    [JSProperty(Name = "target")]
    public Vector3 Target
    {
        get => _camera.Target;
        set => _camera.Target = value;
    }

    [JSProperty(Name = "up")]
    public Vector3 Up
    {
        get => _camera.Up;
        set => _camera.Up = value;
    }

    [JSProperty(Name = "fovy")]
    public double FovY
    {
        get => _camera.FovY;
        set => _camera.FovY = (float)value;
    }

    [JSProperty(Name = "projection")]
    public int Projection
    {
        get => (int)_camera.Projection;
        set => _camera.Projection = (CameraProjection)value;
    }

    internal void UpdateFrom(Raylib_cs.Camera3D camera)
    {
        _camera = camera;
    }

    public static implicit operator Raylib_cs.Camera3D(Camera3D c) => c._camera;
    public static implicit operator Camera3D(Raylib_cs.Camera3D c) => new() { _camera = c };
}

[JSClass(Name = "Color")]
[method: JSConstructor]
internal partial class Color(int r = 0, int g = 0, int b = 0, int a = 255)
{
    internal Raylib_cs.Color _color = new(r, g, b, a);

    [JSProperty(Name = "r")]
    public int R
    {
        get => _color.R;
        set => _color.R = (byte)value;
    }

    [JSProperty(Name = "g")]
    public int G
    {
        get => _color.G;
        set => _color.G = (byte)value;
    }

    [JSProperty(Name = "b")]
    public int B
    {
        get => _color.B;
        set => _color.B = (byte)value;
    }

    [JSProperty(Name = "a")]
    public int A
    {
        get => _color.A;
        set => _color.A = (byte)value;
    }

    public static implicit operator Raylib_cs.Color(Color c) => c._color;
    public static implicit operator Color(Raylib_cs.Color c) => new() { _color = c };
}

#endregion