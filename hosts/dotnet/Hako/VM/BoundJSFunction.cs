using HakoJS.Extensions;
namespace HakoJS.VM;

/// <summary>
/// Represents a JavaScript function bound to a specific 'this' context.
/// </summary>
public readonly struct BoundJSFunction
{
    private readonly JSValue _function;
    private readonly JSValue _thisArg;

    internal BoundJSFunction(JSValue function, JSValue thisArg)
    {
        _function = function;
        _thisArg = thisArg;
    }

    public JSValue Invoke(params object?[] args) => 
        JSValueExtensions.InvokeInternal(_function, _thisArg, args);

    public TResult Invoke<TResult>(params object?[] args) => 
        JSValueExtensions.InvokeInternal<TResult>(_function, _thisArg, args);

    public Task<JSValue> InvokeAsync(params object?[] args) => 
        JSValueExtensions.InvokeAsyncInternal(_function, _thisArg, args);

    public Task<TResult> InvokeAsync<TResult>(params object?[] args) => 
        JSValueExtensions.InvokeAsyncInternal<TResult>(_function, _thisArg, args);
}