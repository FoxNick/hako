using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.SourceGeneration;

var runtime = Hako.Initialize<WasmtimeEngine>();

runtime.RegisterObjectConverters();

var realm = runtime.CreateRealm().WithGlobals(g => g.WithConsole());


var redRanger = new RedRanger("Tyrannosaurus");
var jsRanger = redRanger.ToJSValue(realm);

Console.WriteLine($"Ranger morphed to JS: {jsRanger.IsObject()}");
jsRanger.Dispose();

var blueRanger = new BlueRanger("Triceratops");
var jsBlueRanger = realm.NewValue(blueRanger);

var morphinTime = await realm.EvalAsync(@"
    function morphinTime(ranger) { 
        ranger.morph();
        ranger.callForBackup();
    } 
    morphinTime
");

var invoke = await morphinTime.InvokeAsync(jsBlueRanger);
invoke.Dispose();

Console.WriteLine("\n--- Switching Rangers ---\n");

var jsRedRanger = realm.NewValue(redRanger);
var invoke2 = await morphinTime.InvokeAsync(jsRedRanger);
invoke2.Dispose();
jsRedRanger.Dispose();

morphinTime.Dispose();
jsBlueRanger.Dispose();

realm.Dispose();
runtime.Dispose();
await Hako.ShutdownAsync();


[JSObject]
internal abstract partial record PowerRanger(DateTime MorphTime)
{
    [JSMethod]
    public abstract void Morph();

    // Shared base implementation - can be overridden or used as-is
    [JSMethod]
    public virtual void CallForBackup()
    {
        Console.WriteLine($"⚡ Power Rangers, we need reinforcements! [{MorphTime:T}]");
    }
}

internal partial record RedRanger(string Zord) : PowerRanger(DateTime.Now)
{
    public override void Morph()
    {
        Console.WriteLine($"🔴 It's morphin time! {Zord} Dinozord, power up! [{MorphTime:T}]");
    }

    // RedRanger overrides the base implementation
    public override void CallForBackup()
    {
        Console.WriteLine($"🔴 Red Ranger calling Command Center: We need {Zord} backup NOW!");
    }
}

internal partial record BlueRanger(string Zord) : PowerRanger(DateTime.Now)
{
    public override void Morph()
    {
        Console.WriteLine($"🔵 {Zord} Dinozord, engage! Morphological transformation complete! [{MorphTime:T}]");
    }
}