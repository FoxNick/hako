using System.Net.Http.Headers;
using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Builders;

/// <summary>
/// Provides a fluent API for defining and registering global variables, functions, and objects in a JavaScript realm.
/// </summary>
/// <remarks>
/// <para>
/// This builder allows you to extend the JavaScript global scope with custom values, functions, and objects
/// from C#. It includes convenient helpers for common patterns like timers and console logging.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// realm.WithGlobals(globals =>
/// {
///     globals.WithValue("appName", "MyApp")
///            .WithFunction("print", (ctx, _, args) => 
///            {
///                Console.WriteLine(args[0].AsString());
///                return null;
///            })
///            .WithTimers()
///            .WithConsole()
///            .Apply();
/// });
/// 
/// // JavaScript can now use:
/// // console.log(appName); // "MyApp"
/// // print("Hello!");
/// // setTimeout(() => console.log("Later"), 1000);
/// </code>
/// </para>
/// </remarks>
public class GlobalsBuilder
{
    private readonly Realm _context;
    private readonly List<(string Name, JSValue Value)> _globals = [];

    private GlobalsBuilder(Realm context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new <see cref="GlobalsBuilder"/> for the specified realm.
    /// </summary>
    /// <param name="context">The realm in which to define global values.</param>
    /// <returns>A new <see cref="GlobalsBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
    public static GlobalsBuilder For(Realm context)
    {
        return new GlobalsBuilder(context);
    }

    /// <summary>
    /// Adds a global object built using the <see cref="JSObjectBuilder"/> fluent API.
    /// </summary>
    /// <param name="name">The name of the global object.</param>
    /// <param name="configure">An action that configures the object using <see cref="JSObjectBuilder"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// This method is useful for creating complex global objects with multiple properties and methods.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// globals.WithObject("math", obj =>
    /// {
    ///     obj.WithFunction("square", (ctx, _, args) => 
    ///     {
    ///         var n = args[0].AsNumber();
    ///         return ctx.NewNumber(n * n);
    ///     })
    ///     .WithProperty("PI", 3.14159);
    /// });
    /// 
    /// // JavaScript: math.square(5); // 25
    /// </code>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithObject(string name, Action<JSObjectBuilder> configure)
    {
        var builder = JSObjectBuilder.Create(_context);
        configure(builder);
        _globals.Add((name, builder.Build()));
        return this;
    }

    /// <summary>
    /// Adds a global function that returns a value.
    /// </summary>
    /// <param name="name">The name of the global function.</param>
    /// <param name="func">The function implementation that receives context, thisArg, and arguments, and returns a <see cref="JSValue"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="func"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The function can return <c>null</c> to return <c>undefined</c> to JavaScript.
    /// </remarks>
    public GlobalsBuilder WithFunction(string name, JSFunction func)
    {
        _globals.Add((name, _context.NewFunction(name, func)));
        return this;
    }

    /// <summary>
    /// Adds a global function that does not return a value (returns <c>undefined</c>).
    /// </summary>
    /// <param name="name">The name of the global function.</param>
    /// <param name="func">The function implementation that receives context, thisArg, and arguments.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="func"/> is <c>null</c>.</exception>
    public GlobalsBuilder WithFunction(string name, JSAction func)
    {
        _globals.Add((name, _context.NewFunction(name, func)));
        return this;
    }

    /// <summary>
    /// Adds a global asynchronous function that returns a Promise.
    /// </summary>
    /// <param name="name">The name of the global function.</param>
    /// <param name="func">The async function implementation that returns a <see cref="Task{JSValue}"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="func"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// The function automatically wraps the Task in a JavaScript Promise. If the Task throws an exception,
    /// the Promise is rejected with that error.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// globals.WithFunctionAsync("fetchData", async (ctx, _, args) =>
    /// {
    ///     var url = args[0].AsString();
    ///     var data = await httpClient.GetStringAsync(url);
    ///     return ctx.NewString(data);
    /// });
    /// 
    /// // JavaScript: await fetchData("https://api.example.com");
    /// </code>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithFunctionAsync(string name, JSAsyncFunction func)
    {
        _globals.Add((name, _context.NewFunctionAsync(name, func)));
        return this;
    }

    /// <summary>
    /// Adds a global asynchronous function that returns a Promise resolving to <c>undefined</c>.
    /// </summary>
    /// <param name="name">The name of the global function.</param>
    /// <param name="func">The async function implementation that returns a <see cref="Task"/>.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="func"/> is <c>null</c>.</exception>
    public GlobalsBuilder WithFunctionAsync(string name, JSAsyncAction func)
    {
        _globals.Add((name, _context.NewFunctionAsync(name, func)));
        return this;
    }

    /// <summary>
    /// Adds a global value of any supported type.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to add. Must be a type supported by <see cref="Realm.NewValue{T}"/>.</typeparam>
    /// <param name="name">The name of the global variable.</param>
    /// <param name="value">The value to assign to the global variable.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// Supported types include primitives (string, int, double, bool), byte arrays, and types implementing IJSMarshalable.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// globals.WithValue("version", "1.0.0")
    ///        .WithValue("debug", true)
    ///        .WithValue("maxConnections", 100);
    /// </code>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithValue<TValue>(string name, TValue? value)
    {
        _globals.Add((name, _context.NewValue(value)));
        return this;
    }

    /// <summary>
    /// Adds the <c>setTimeout</c> function to the global scope.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers a JavaScript-compatible <c>setTimeout</c> function that schedules a callback
    /// to run after a specified delay in milliseconds.
    /// </para>
    /// <para>
    /// JavaScript usage: <c>setTimeout(callback, delayMs)</c>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithSetTimeout()
    {
        return WithFunction("setTimeout", (ctx, _, args) =>
        {
            if (args.Length == 0) throw new ArgumentException("setTimeout requires at least a callback argument");

            var callback = args[0];
            var delay = args.Length > 1 ? Math.Max(0, (int)args[1].AsNumber()) : 0;

            var timerId = ctx.Timers.SetTimeout(callback, delay);
            return ctx.NewNumber(timerId);
        });
    }

    /// <summary>
    /// Adds the <c>setInterval</c> function to the global scope.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers a JavaScript-compatible <c>setInterval</c> function that repeatedly calls a callback
    /// at a specified interval in milliseconds.
    /// </para>
    /// <para>
    /// JavaScript usage: <c>setInterval(callback, intervalMs)</c>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithSetInterval()
    {
        return WithFunction("setInterval", (ctx, _, args) =>
        {
            if (args.Length == 0) throw new ArgumentException("setInterval requires at least a callback argument");

            var callback = args[0];
            var interval = args.Length > 1 ? Math.Max(1, (int)args[1].AsNumber()) : 1;

            var timerId = ctx.Timers.SetInterval(callback, interval);
            return ctx.NewNumber(timerId);
        });
    }

    /// <summary>
    /// Adds the <c>clearTimeout</c> function to the global scope.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers a JavaScript-compatible <c>clearTimeout</c> function that cancels a timer
    /// previously created with <c>setTimeout</c>.
    /// </para>
    /// <para>
    /// JavaScript usage: <c>clearTimeout(timerId)</c>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithClearTimeout()
    {
        return WithFunction("clearTimeout", (ctx, _, args) =>
        {
            if (args.Length > 0 && args[0].IsNumber())
            {
                var timerId = (int)args[0].AsNumber();
                ctx.Timers.ClearTimer(timerId);
            }
        });
    }

    /// <summary>
    /// Adds the <c>clearInterval</c> function to the global scope.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers a JavaScript-compatible <c>clearInterval</c> function that cancels a timer
    /// previously created with <c>setInterval</c>.
    /// </para>
    /// <para>
    /// JavaScript usage: <c>clearInterval(timerId)</c>
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithClearInterval()
    {
        return WithFunction("clearInterval", (ctx, _, args) =>
        {
            if (args.Length > 0 && args[0].IsNumber())
            {
                var timerId = (int)args[0].AsNumber();
                ctx.Timers.ClearTimer(timerId);
            }
        });
    }

    /// <summary>
    /// Adds all timer functions (<c>setTimeout</c>, <c>setInterval</c>, <c>clearTimeout</c>, <c>clearInterval</c>) to the global scope.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// This is a convenience method that calls <see cref="WithSetTimeout"/>, <see cref="WithSetInterval"/>,
    /// <see cref="WithClearTimeout"/>, and <see cref="WithClearInterval"/>.
    /// </remarks>
    public GlobalsBuilder WithTimers()
    {
        return WithSetTimeout()
            .WithSetInterval()
            .WithClearTimeout()
            .WithClearInterval();
    }

    /// <summary>
    /// Adds a basic <c>console</c> object with <c>log</c>, <c>error</c>, and <c>warn</c> methods.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Creates a global <c>console</c> object that outputs to the C# console:
    /// </para>
    /// <list type="bullet">
    /// <item><c>console.log(...)</c> - Writes to standard output</item>
    /// <item><c>console.error(...)</c> - Writes to standard error in red</item>
    /// <item><c>console.warn(...)</c> - Writes to standard output in yellow</item>
    /// </list>
    /// <para>
    /// All methods accept multiple arguments which are joined with spaces.
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithConsole()
    {
        return WithObject("console", console => console
            .WithFunction("log",
                (_, _, args) => { Console.WriteLine(string.Join(" ", args.Select(a => a.AsString()))); })
            .WithFunction("error", (_, _, args) =>
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(string.Join(" ", args.Select(a => a.AsString())));
                Console.ForegroundColor = color;
            })
            .WithFunction("warn", (_, _, args) =>
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(string.Join(" ", args.Select(a => a.AsString())));
                Console.ForegroundColor = color;
            }));
    }

    /// <summary>
    /// Adds a basic <c>fetch</c> function for making HTTP requests.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Provides a simplified implementation of the Fetch API that supports:
    /// </para>
    /// <list type="bullet">
    /// <item>GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS methods</item>
    /// <item>Custom headers</item>
    /// <item>Request body for POST/PUT/PATCH</item>
    /// <item>Response methods: <c>text()</c>, <c>json()</c>, <c>arrayBuffer()</c></item>
    /// <item>Response properties: <c>ok</c>, <c>status</c>, <c>statusText</c>, <c>headers</c></item>
    /// </list>
    /// <para>
    /// Example JavaScript usage:
    /// <code>
    /// const response = await fetch('https://api.example.com/data', {
    ///     method: 'POST',
    ///     headers: { 'Content-Type': 'application/json' },
    ///     body: JSON.stringify({ key: 'value' })
    /// });
    /// const data = await response.json();
    /// </code>
    /// </para>
    /// <para>
    /// Note: This implementation creates a single shared <see cref="HttpClient"/> instance.
    /// </para>
    /// </remarks>
    public GlobalsBuilder WithFetch()
    {
        var httpClient = new HttpClient();

        return WithFunctionAsync("fetch", async (ctx, _, args) =>
        {
            if (args.Length == 0) throw new ArgumentException("fetch requires at least a URL argument");

            var input = args[0];
            var init = args.Length > 1 ? args[1] : null;

            // Parse URL
            string url;
            if (input.IsString())
            {
                url = input.AsString();
            }
            else
            {
                using var urlProp = input.GetProperty("url");
                url = urlProp.AsString();
            }

            // Parse options
            var method = HttpMethod.Get;
            Dictionary<string, string>? headers = null;
            HttpContent? content = null;

            if (init != null && !init.IsNullOrUndefined())
            {
                using var methodProp = init.GetProperty("method");
                if (!methodProp.IsNullOrUndefined())
                    method = methodProp.AsString().ToUpperInvariant() switch
                    {
                        "POST" => HttpMethod.Post,
                        "PUT" => HttpMethod.Put,
                        "DELETE" => HttpMethod.Delete,
                        "PATCH" => HttpMethod.Patch,
                        "HEAD" => HttpMethod.Head,
                        "OPTIONS" => HttpMethod.Options,
                        _ => HttpMethod.Get
                    };

                using var headersProp = init.GetProperty("headers");
                if (!headersProp.IsNullOrUndefined())
                {
                    headers = new Dictionary<string, string>();
                    foreach (var headerName in headersProp.GetOwnPropertyNames())
                    {
                        var key = headerName.AsString();
                        using var value = headersProp.GetProperty(key);
                        headers[key] = value.AsString();
                        headerName.Dispose();
                    }
                }

                using var bodyProp = init.GetProperty("body");
                if (!bodyProp.IsNullOrUndefined())
                {
                    content = new StringContent(bodyProp.AsString());
                    if (headers != null && headers.TryGetValue("Content-Type", out var ct))
                        try
                        {
                            content.Headers.ContentType = new MediaTypeHeaderValue(ct);
                            headers.Remove("Content-Type");
                        }
                        catch
                        {
                            // ignored
                        }
                }
            }

            var request = new HttpRequestMessage(method, url);
            if (headers != null)
                foreach (var (key, value) in headers)
                    request.Headers.TryAddWithoutValidation(key, value);

            if (content != null) request.Content = content;

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            }
            catch
            {
                return CreateErrorResponse(ctx);
            }

            return CreateResponse(ctx, response, url);
        });

        JSValue CreateResponse(Realm ctx, HttpResponseMessage response, string url)
        {
            var builder = JSObjectBuilder.Create(ctx);

            // Create headers first and dispose it properly
            using var headers = CreateHeaders(ctx, response);

            builder
                .WithReadOnly("ok", response.IsSuccessStatusCode)
                .WithReadOnly("status", (int)response.StatusCode)
                .WithReadOnly("statusText", response.ReasonPhrase ?? "")
                .WithReadOnly("url", url)
                .WithReadOnly("redirected", false)
                .WithReadOnly("type", "basic")
                .WithFunctionAsync("text", async (realm, _, __) =>
                {
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return realm.NewString(text);
                })
                .WithFunctionAsync("json", async (realm, _, __) =>
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return realm.ParseJson(json);
                })
                .WithFunctionAsync("arrayBuffer", async (realm, _, __) =>
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return realm.NewArrayBuffer(bytes);
                })
                .WithProperty("headers", headers); // Pass the underlying JSValue

            return builder.Build();
        }

        JSObject CreateHeaders(Realm ctx, HttpResponseMessage response)
        {
            var dict = new Dictionary<string, string>();
            foreach (var h in response.Headers)
                dict[h.Key.ToLowerInvariant()] = string.Join(", ", h.Value);
            if (response.Content?.Headers != null)
                foreach (var h in response.Content.Headers)
                    dict[h.Key.ToLowerInvariant()] = string.Join(", ", h.Value);

            var builder = JSObjectBuilder.Create(ctx);
            builder
                .WithFunction("get", (realm, _, args) =>
                {
                    if (args.Length == 0) return realm.Null();
                    var name = args[0].AsString().ToLowerInvariant();
                    return dict.TryGetValue(name, out var v) ? realm.NewString(v) : realm.Null();
                })
                .WithFunction("has", (realm, _, args) =>
                {
                    if (args.Length == 0) return realm.False();
                    return dict.ContainsKey(args[0].AsString().ToLowerInvariant()) ? realm.True() : realm.False();
                });

            return builder.Build();
        }

        JSValue CreateErrorResponse(Realm ctx)
        {
            var builder = JSObjectBuilder.Create(ctx);
            builder
                .WithReadOnly("ok", false)
                .WithReadOnly("status", 0)
                .WithReadOnly("statusText", "")
                .WithReadOnly("url", "")
                .WithReadOnly("type", "error")
                .WithFunctionAsync("text", (realm, _, __) => Task.FromResult(realm.NewString(""))!)
                .WithFunctionAsync("json", (realm, _, __) => Task.FromResult(realm.Null())!)
                .WithProperty("headers", CreateEmptyHeaders(ctx));

            return builder.Build();
        }

        JSValue CreateEmptyHeaders(Realm ctx)
        {
            var builder = JSObjectBuilder.Create(ctx);
            builder
                .WithFunction("get", (realm, _, __) => realm.Null())
                .WithFunction("has", (realm, _, __) => realm.False());
            return builder.Build();
        }
    }

    /// <summary>
    /// Applies all registered globals to the realm's global object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called to finalize the configuration and make all registered
    /// globals available in JavaScript code.
    /// </para>
    /// <para>
    /// After calling this method, all added values, functions, and objects are set on the
    /// global object and the builder's internal state is cleared.
    /// </para>
    /// </remarks>
    public void Apply()
    {
        using var global = _context.GetGlobalObject();
        foreach (var (name, value) in _globals)
        {
            global.SetProperty(name, value);
            value.Dispose();
        }

        _globals.Clear();
    }
}