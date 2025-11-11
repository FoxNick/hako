using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace HakoJS.SourceGenerator.Tests;

public class JSBindingGeneratorTests(ITestOutputHelper output)
{
    private const string AttributesAndInterfacesText = @"
namespace HakoJS.SourceGeneration
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class JSClassAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Constructor)]
    public class JSConstructorAttribute : System.Attribute
    {
    }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class JSPropertyAttribute : System.Attribute
    {
        public string? Name { get; set; }
        public bool Static { get; set; }
        public bool ReadOnly { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class JSMethodAttribute : System.Attribute
    {
        public string? Name { get; set; }
        public bool Static { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Method)]
    public class JSIgnoreAttribute : System.Attribute
    {
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class JSModuleAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class JSModuleValueAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class JSModuleMethodAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class JSModuleClassAttribute : System.Attribute
    {
        public System.Type? ClassType { get; set; }
        public string? ExportName { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class JSModuleInterfaceAttribute : System.Attribute
    {
        public System.Type? InterfaceType { get; set; }
        public string? ExportName { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
    public class JSObjectAttribute : System.Attribute
    {
    }

    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public class JSPropertyNameAttribute : System.Attribute
    {
        public string Name { get; }
        public JSPropertyNameAttribute(string name) { Name = name; }
    }

    public interface IJSBindable<TSelf> where TSelf : IJSBindable<TSelf>
    {
        static abstract string TypeKey { get; }
        static abstract HakoJS.VM.JSClass CreatePrototype(HakoJS.VM.Realm realm);
        static abstract TSelf? GetInstanceFromJS(HakoJS.VM.JSValue jsValue);
        static abstract bool RemoveInstance(HakoJS.VM.JSValue jsValue);
    }

    public interface IJSMarshalable<TSelf> where TSelf : IJSMarshalable<TSelf>
    {
        HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm);
        static abstract TSelf FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue);
    }

    public interface IJSModuleBindable
    {
        static abstract string Name { get; }
        static abstract HakoJS.Host.CModule Create(HakoJS.Host.HakoRuntime runtime, HakoJS.VM.Realm? context = null);
    }

    public interface IDefinitelyTyped<TSelf>
    {
        static abstract string TypeDefinition { get; }
    }

    // Typed Array Value Types
    public readonly struct Uint8ArrayValue : IJSMarshalable<Uint8ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Uint8ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Int8ArrayValue : IJSMarshalable<Int8ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Int8ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Uint8ClampedArrayValue : IJSMarshalable<Uint8ClampedArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Uint8ClampedArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Int16ArrayValue : IJSMarshalable<Int16ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Int16ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Uint16ArrayValue : IJSMarshalable<Uint16ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Uint16ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Int32ArrayValue : IJSMarshalable<Int32ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Int32ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Uint32ArrayValue : IJSMarshalable<Uint32ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Uint32ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Float32ArrayValue : IJSMarshalable<Float32ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Float32ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct Float64ArrayValue : IJSMarshalable<Float64ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static Float64ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct BigInt64ArrayValue : IJSMarshalable<BigInt64ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static BigInt64ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }

    public readonly struct BigUint64ArrayValue : IJSMarshalable<BigUint64ArrayValue>
    {
        public int Length { get; }
        public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
        public static BigUint64ArrayValue FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => default;
    }
}

namespace HakoJS.VM
{
    public class Realm 
    { 
        public HakoRuntime Runtime { get; set; }
        public HakoJS.VM.JSValue NewObject() { return new JSValue(); }
        public HakoJS.VM.JSValue NewValue(object value) { return new JSValue(); }
        public HakoJS.VM.JSValue NewFunction(string name, System.Func<Realm, JSValue, JSValue[], JSValue> callback) { return new JSValue(); }
        public HakoJS.VM.JSValue NewFunctionAsync(string name, System.Func<Realm, JSValue, JSValue[], System.Threading.Tasks.Task<JSValue>> callback) { return new JSValue(); }
        public HakoJS.VM.JSValue CallFunction(JSValue func, JSValue? thisArg, params JSValue[] args) { return new JSValue(); }
        public System.Threading.Tasks.Task AwaitPromise(JSValue promise) { return System.Threading.Tasks.Task.CompletedTask; }
        public HakoJS.VM.JSValue NewArrayBuffer(byte[] data) { return new JSValue(); }
        public HakoJS.VM.JSValue NewTypedArrayWithBuffer(JSValue buffer, int offset, int length, TypedArrayType type) { return new JSValue(); }
    }
    
    public class HakoRuntime 
    { 
        public void RegisterJSClass<T>(JSClass jsClass) { }
        public JSClass GetJSClass<T>() { return null; }
        public Realm GetSystemRealm() { return new Realm(); }
        public HakoJS.Host.CModule CreateCModule(string name, System.Action<object> init, Realm realm) { return new HakoJS.Host.CModule(); }
    }
    
    public class JSClass 
    { 
        public JSValue Prototype { get; set; }
        public JSValue CreateInstance() { return new JSValue(); }
        public JSValue CreateInstance(int opaqueId) { return new JSValue(); }
    }
    
    public struct JSValue 
    { 
        public void SetOpaque(int id) { }
        public int GetOpaque() { return 0; }
        public bool IsNullOrUndefined() { return false; }
        public bool IsString() { return false; }
        public bool IsNumber() { return false; }
        public bool IsBoolean() { return false; }
        public bool IsArray() { return false; }
        public bool IsArrayBuffer() { return false; }
        public bool IsTypedArray() { return false; }
        public bool IsObject() { return false; }
        public bool IsFunction() { return false; }
        public bool IsDate() { return false; }
        public string AsString() { return ""; }
        public double AsNumber() { return 0.0; }
        public bool AsBoolean() { return false; }
        public System.DateTime AsDateTime() { return System.DateTime.UtcNow; }
        public byte[] CopyArrayBuffer() { return new byte[0]; }
        public byte[] CopyTypedArray() { return new byte[0]; }
        public TypedArrayType GetTypedArrayType() { return TypedArrayType.Uint8Array; }
        public JSValue GetProperty(string name) { return new JSValue(); }
        public JSValue GetProperty(int index) { return new JSValue(); }
        public void SetProperty(string name, JSValue value) { }
        public void SetProperty<T>(string name, T value) { }
        public void SetProperty(int index, JSValue value) { }
        public JSValue Dup() { return this; }
        public JSValue GetPromiseResult() { return this; }
        public JSValue Value() { return this; }
        public void Dispose() { }
    }
    
    public enum JSErrorType { Type, Reference, Error }
    
    public enum TypedArrayType 
    { 
        Uint8Array, 
        Int8Array, 
        Uint8ClampedArray, 
        Int16Array, 
        Uint16Array, 
        Int32Array, 
        Uint32Array, 
        Float32Array, 
        Float64Array, 
        BigInt64Array, 
        BigUint64Array 
    }
    
    public class JSObject
    {
        public JSValue Value() { return new JSValue(); }
        public void Dispose() { }
    }
}

namespace HakoJS.Host
{
    public class CModule 
    {
        public CModule AddExports(params string[] exports) { return this; }
    }
}

namespace HakoJS.Builders
{
    public class JSClassBuilder
    {
        public JSClassBuilder(HakoJS.VM.Realm realm, string name) { }
        public void SetConstructor(System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue, HakoJS.VM.JSValue?> func) { }
        public void SetFinalizer(System.Action<HakoJS.VM.HakoRuntime, int, int> action) { }
        public void AddReadOnlyProperty(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> getter) { }
        public void AddReadWriteProperty(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> getter, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> setter) { }
        public void AddReadOnlyStaticProperty(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> getter) { }
        public void AddReadWriteStaticProperty(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> getter, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> setter) { }
        public void AddMethod(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> func) { }
        public void AddMethodAsync(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], System.Threading.Tasks.Task<HakoJS.VM.JSValue>> func) { }
        public void AddStaticMethod(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], HakoJS.VM.JSValue> func) { }
        public void AddStaticMethodAsync(string name, System.Func<HakoJS.VM.Realm, HakoJS.VM.JSValue, HakoJS.VM.JSValue[], System.Threading.Tasks.Task<HakoJS.VM.JSValue>> func) { }
        public HakoJS.VM.JSClass Build() { return new HakoJS.VM.JSClass(); }
    }
}

namespace HakoJS.Extensions
{
    public static class JSValueExtensions
    {
        public static HakoJS.VM.JSValue ThrowError(this HakoJS.VM.Realm ctx, HakoJS.VM.JSErrorType type, string message) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue ThrowError(this HakoJS.VM.Realm ctx, System.Exception ex) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue Undefined(this HakoJS.VM.Realm ctx) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue Null(this HakoJS.VM.Realm ctx) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue True(this HakoJS.VM.Realm ctx) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue False(this HakoJS.VM.Realm ctx) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue NewString(this HakoJS.VM.Realm ctx, string value) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue NewNumber(this HakoJS.VM.Realm ctx, double value) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue NewDate(this HakoJS.VM.Realm ctx, System.DateTime value) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue NewArrayBuffer(this HakoJS.VM.Realm ctx, byte[] value) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.JSValue NewArray(this HakoJS.VM.Realm ctx) { return new HakoJS.VM.JSValue(); }
        public static HakoJS.VM.Realm CreatePrototype<T>(this HakoJS.VM.Realm realm) where T : HakoJS.SourceGeneration.IJSBindable<T> { return realm; }
        public static HakoJS.VM.JSValue Unwrap(this HakoJS.VM.JSValue value) { return value; }
    }
}
";

    private GeneratorDriverRunResult RunGenerator(string sourceCode)
    {
        var generator = new JSBindingGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [
                CSharpSyntaxTree.ParseText(AttributesAndInterfacesText,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)),
                CSharpSyntaxTree.ParseText(sourceCode,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Concurrent.ConcurrentDictionary<,>).Assembly
                    .Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        output.WriteLine($"Generated {runResult.GeneratedTrees.Length} files");
        output.WriteLine($"Diagnostics: {runResult.Diagnostics.Length}");

        foreach (var diagnostic in runResult.Diagnostics)
        {
            output.WriteLine($"  [{diagnostic.Severity}] {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return runResult;
    }

    #region Basic Class Generation Tests

    [Fact]
    public void GeneratesBasicClassWithConstructor()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass(Name = ""TestClass"")]
public partial class TestClass
{
    [JSConstructor]
    public TestClass() { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(result.GeneratedTrees);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains(
            "partial class TestClass : global::HakoJS.SourceGeneration.IJSBindable<TestClass>, global::HakoJS.SourceGeneration.IJSMarshalable<TestClass>",
            generatedCode);
        Assert.Contains(
            "static global::HakoJS.VM.JSClass global::HakoJS.SourceGeneration.IJSBindable<TestClass>.CreatePrototype(global::HakoJS.VM.Realm realm)",
            generatedCode);
        Assert.Contains("builder.SetConstructor", generatedCode);
        Assert.Contains("StoreInstance", generatedCode);
        Assert.Contains("GetInstance", generatedCode);
    }

    [Fact]
    public void GeneratesPropertiesCorrectly()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public string Name { get; set; }

    [JSProperty(Name = ""customName"")]
    public int Value { get; set; }

    [JSProperty(ReadOnly = true)]
    public double ReadOnlyProp { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("AddReadWriteProperty(\"name\"", generatedCode);
        Assert.Contains("AddReadWriteProperty(\"customName\"", generatedCode);
        Assert.Contains("AddReadOnlyProperty(\"readOnlyProp\"", generatedCode);
    }

    [Fact]
    public void GeneratesMethodsCorrectly()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public void VoidMethod() { }

    [JSMethod(Name = ""add"")]
    public int Add(int a, int b) => a + b;

    [JSMethod]
    public string Greet(string name) => $""Hello, {name}"";
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("AddMethod(\"voidMethod\"", generatedCode);
        Assert.Contains("AddMethod(\"add\"", generatedCode);
        Assert.Contains("AddMethod(\"greet\"", generatedCode);
    }

    [Fact]
    public void GeneratesStaticMembersCorrectly()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty(Static = true)]
    public static string StaticProp { get; set; }

    [JSMethod(Static = true)]
    public static int StaticMethod() => 42;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("AddReadWriteStaticProperty(\"staticProp\"", generatedCode);
        Assert.Contains("AddStaticMethod(\"staticMethod\"", generatedCode);
    }

    [Fact]
    public void GeneratesAsyncMethodsCorrectly()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public async Task AsyncVoidMethod() 
    { 
        await Task.CompletedTask;
    }

    [JSMethod]
    public async Task<string> AsyncStringMethod() 
    { 
        await Task.CompletedTask;
        return ""result"";
    }

    [JSMethod]
    public Task<int> TaskMethod() 
    { 
        return Task.FromResult(42);
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("AddMethodAsync(\"asyncVoidMethod\"", generatedCode);
        Assert.Contains("AddMethodAsync(\"asyncStringMethod\"", generatedCode);
        Assert.Contains("AddMethodAsync(\"taskMethod\"", generatedCode);
    }

    #endregion

    #region JSObject Basic Tests

    [Fact]
    public void GeneratesBasicRecord()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record UserProfile(string Name, int Age);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(result.GeneratedTrees);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("partial record UserProfile : global::HakoJS.SourceGeneration.IJSMarshalable<UserProfile>",
            generatedCode);
        Assert.Contains("public global::HakoJS.VM.JSValue ToJSValue(global::HakoJS.VM.Realm realm)", generatedCode);
        Assert.Contains(
            "public static UserProfile FromJSValue(global::HakoJS.VM.Realm realm, global::HakoJS.VM.JSValue jsValue)",
            generatedCode);
        Assert.Contains("obj.SetProperty(\"name\"", generatedCode);
        Assert.Contains("obj.SetProperty(\"age\"", generatedCode);
    }

    [Fact]
    public void GeneratesRecordWithOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record UserProfile(
    string Name,
    int Age,
    string? Email = null,
    bool IsActive = true
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should handle optional parameters with default values
        Assert.Contains("if (emailProp.IsNullOrUndefined())", generatedCode);
        Assert.Contains("email = null", generatedCode);
        Assert.Contains("isActive = true", generatedCode);
    }

    [Fact]
    public void GeneratesRecordWithCustomPropertyNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record ApiRequest(
    [JSPropertyName(""api_key"")] string ApiKey,
    [JSPropertyName(""user_id"")] int UserId
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("\"api_key\"", generatedCode);
        Assert.Contains("\"user_id\"", generatedCode);
    }

    [Fact]
    public void GeneratesRecordWithDelegates()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record EventConfig(
    string EventName,
    Action<string> OnEvent
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should implement IDisposable for delegate tracking
        Assert.Contains(
            ": global::HakoJS.SourceGeneration.IJSMarshalable<EventConfig>, global::HakoJS.SourceGeneration.IDefinitelyTyped<EventConfig>, global::System.IDisposable",
            generatedCode);
        Assert.Contains("private global::HakoJS.VM.JSValue? _capturedOnEvent;", generatedCode);
        Assert.Contains("public void Dispose()", generatedCode);

        // Should generate function wrapper in ToJSValue
        Assert.Contains("realm.NewFunction(\"onEvent\"", generatedCode);
        Assert.Contains("OnEvent(arg0)", generatedCode);

        // Should generate delegate wrapper in FromJSValue
        Assert.Contains("new global::System.Action<", generatedCode);
        Assert.Contains("capturedOnEvent = onEventProp.Dup()", generatedCode);
    }

    [Fact]
    public void GeneratesRecordWithFuncDelegate()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Calculator(
    Func<int, int, int> Add,
    Func<int, bool>? Validator = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should generate Func wrapper
        Assert.Contains("new global::System.Func<", generatedCode);

        // Should handle return value marshaling
        Assert.Contains("var result = Add(", generatedCode);
        Assert.Contains("return", generatedCode);
    }

    [Fact]
    public void GeneratesRecordWithAsyncDelegates()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;
using System.Threading.Tasks;

namespace TestNamespace;

[JSObject]
public partial record AsyncConfig(
    Func<int, Task<string>> FetchData,
    Func<Task> Initialize
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // ToJSValue: Should generate async function wrappers for C# delegates
        Assert.Contains("NewFunctionAsync", generatedCode);
        Assert.Contains("async (ctx, thisArg, args)", generatedCode);
        Assert.Contains("await FetchData", generatedCode);
        Assert.Contains("await Initialize", generatedCode);

        // FromJSValue: Should handle promise awaiting when calling JS functions from C#
        Assert.Contains("await result.Await()", generatedCode);
    }

    [Fact]
    public void GeneratesRecordWithNestedRecord()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Address(string Street, string City);

[JSObject]
public partial record Person(string Name, Address HomeAddress);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(13, result.GeneratedTrees.Length);

        var personCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Person")).GetText().ToString();

        // Should marshal nested record using ToJSValue/FromJSValue
        Assert.Contains("HomeAddress.ToJSValue(realm)", personCode);
        Assert.Contains("global::TestNamespace.Address.FromJSValue", personCode);
    }

    [Fact]
    public void GeneratesRecordWithAllPrimitiveTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record AllTypes(
    string StringVal,
    int IntVal,
    long LongVal,
    double DoubleVal,
    float FloatVal,
    bool BoolVal,
    byte[] BufferVal,
    int[] IntArrayVal,
    string[] StringArrayVal,
    int? NullableIntVal = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should handle all types correctly
        Assert.Contains("stringVal", generatedCode);
        Assert.Contains("intVal", generatedCode);
        Assert.Contains("longVal", generatedCode);
        Assert.Contains("doubleVal", generatedCode);
        Assert.Contains("floatVal", generatedCode);
        Assert.Contains("boolVal", generatedCode);
        Assert.Contains("bufferVal", generatedCode);
        Assert.Contains("intArrayVal", generatedCode);
        Assert.Contains("stringArrayVal", generatedCode);
        Assert.Contains("nullableIntVal", generatedCode);
    }

    #endregion

    #region JSObject Error Tests

    [Fact]
    public void ReportsErrorForNonPartialRecord()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public record NonPartialRecord(string Name);
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO016");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("must be declared as partial", error.GetMessage());
    }

    [Fact]
    public void ReportsErrorForJSObjectOnClass()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial class NotARecord
{
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO017");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("can only be applied to record types", error.GetMessage());
    }

    [Fact]
    public void ReportsErrorForBothJSObjectAndJSClass()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
[JSClass]
public partial record ConflictingRecord(string Name);
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO018");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("can only have one of these attributes", error.GetMessage());
    }

    [Fact]
    public void ReportsErrorForUnmarshalableRecordParameter()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Collections.Generic;

namespace TestNamespace;

[JSObject]
public partial record InvalidRecord(
    string Name,
    Dictionary<string, int> Data
);
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO019");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("cannot be marshaled", error.GetMessage());
    }

    [Fact]
    public void AllowsJSClassTypesInRecords()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Vector2
{
    [JSProperty]
    public double X { get; set; }

    [JSProperty]
    public double Y { get; set; }

    [JSConstructor]
    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }
}

[JSObject]
public partial record Transform(
    Vector2 Position,
    Vector2 Scale
);
";

        var result = RunGenerator(source);

        // Should allow JSClass types in records
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(13, result.GeneratedTrees.Length);

        var transformCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Transform")).GetText().ToString();

        // Should use ToJSValue/FromJSValue for Vector2
        Assert.Contains("Position.ToJSValue(realm)", transformCode);
        Assert.Contains("global::TestNamespace.Vector2.FromJSValue", transformCode);
    }

    #endregion

    #region Error Tests - HAKO001

    [Fact]
    public void ReportsErrorForNonPartialClass()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public class NonPartialClass
{
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO001");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("must be declared as partial", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO002

    [Fact]
    public void ReportsErrorForNonPartialModule()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule]
public class NonPartialModule
{
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO002");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
    }

    #endregion

    #region Error Tests - HAKO005 (Duplicate Method Names)

    [Fact]
    public void ReportsErrorForDuplicateMethodNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod(Name = ""test"")]
    public void Method1() { }

    [JSMethod(Name = ""test"")]
    public void Method2() { }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO005");
        Assert.NotNull(error);
        Assert.Contains("same JavaScript name", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO006 (Method Static Mismatch)

    [Fact]
    public void ReportsErrorForMethodStaticMismatch()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod(Static = true)]
    public void InstanceMethod() { }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO006");
        Assert.NotNull(error);
        Assert.Contains("Static attribute must match", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO007 (Duplicate Property Names)

    [Fact]
    public void ReportsErrorForDuplicatePropertyNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty(Name = ""value"")]
    public int Prop1 { get; set; }

    [JSProperty(Name = ""value"")]
    public string Prop2 { get; set; }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO007");
        Assert.NotNull(error);
        Assert.Contains("same JavaScript name", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO008 (Property Static Mismatch)

    [Fact]
    public void ReportsErrorForPropertyStaticMismatch()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty(Static = true)]
    public int InstanceProperty { get; set; }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO008");
        Assert.NotNull(error);
        Assert.Contains("Static attribute must match", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO012 (Unmarshalable Property Type)

    [Fact]
    public void ReportsErrorForUnmarshalablePropertyType()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Collections.Generic;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public Dictionary<string, int> Data { get; set; }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO012");
        Assert.NotNull(error);
        Assert.Contains("cannot be marshaled", error.GetMessage());
    }

    [Fact]
    public void AllowsMarshalablePropertyTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public string Name { get; set; }

    [JSProperty]
    public int Value { get; set; }

    [JSProperty]
    public int? NullableInt { get; set; }

    [JSProperty]
    public double? NullableDouble { get; set; }

    [JSProperty]
    public byte[] Buffer { get; set; }

    [JSProperty]
    public int[] Numbers { get; set; }

    [JSProperty]
    public string[] Strings { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Error Tests - HAKO013 (Unmarshalable Return Type)

    [Fact]
    public void ReportsErrorForUnmarshalableReturnType()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Collections.Generic;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public Dictionary<string, int> GetData() => null;
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO013");
        Assert.NotNull(error);
        Assert.Contains("cannot be marshaled", error.GetMessage());
    }

    [Fact]
    public void AllowsMarshalableReturnTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public void VoidMethod() { }

    [JSMethod]
    public string StringMethod() => """";

    [JSMethod]
    public int IntMethod() => 0;

    [JSMethod]
    public int? NullableIntMethod() => null;

    [JSMethod]
    public byte[] ByteArrayMethod() => null;

    [JSMethod]
    public int[] IntArrayMethod() => null;

    [JSMethod]
    public Task TaskMethod() => Task.CompletedTask;

    [JSMethod]
    public Task<string> TaskStringMethod() => Task.FromResult("""");
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Module Tests

    [Fact]
    public void GeneratesBasicModule()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule(Name = ""TestModule"")]
public partial class TestModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";

    [JSModuleMethod]
    public static int Add(int a, int b) => a + b;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(result.GeneratedTrees);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("partial class TestModule : global::HakoJS.SourceGeneration.IJSModuleBindable", generatedCode);
        Assert.Contains("public static string Name => \"TestModule\"", generatedCode);
        Assert.Contains("SetExport(\"version\"", generatedCode);
        Assert.Contains("SetFunction(\"add\"", generatedCode);
    }

    [Fact]
    public void GeneratesModuleWithAsyncMethod()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSModule]
public partial class TestModule
{
    [JSModuleMethod]
    public static async Task<string> FetchData()
    {
        await Task.CompletedTask;
        return ""data"";
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("async (ctx, thisArg, args)", generatedCode);
        Assert.Contains("await TestModule.FetchData", generatedCode);
    }

    #endregion

    #region Error Tests - HAKO009 (Duplicate Module Method Names)

    [Fact]
    public void ReportsErrorForDuplicateModuleMethodNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule]
public partial class TestModule
{
    [JSModuleMethod(Name = ""test"")]
    public static void Method1() { }

    [JSModuleMethod(Name = ""test"")]
    public static void Method2() { }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO009");
        Assert.NotNull(error);
    }

    #endregion

    #region Error Tests - HAKO010 (Duplicate Module Value Names)

    [Fact]
    public void ReportsErrorForDuplicateModuleValueNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule]
public partial class TestModule
{
    [JSModuleValue(Name = ""value"")]
    public static string Value1 = ""test"";

    [JSModuleValue(Name = ""value"")]
    public static int Value2 = 42;
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO010");
        Assert.NotNull(error);
    }

    #endregion

    #region Error Tests - HAKO011 (Duplicate Module Export Names)

    [Fact]
    public void ReportsErrorForDuplicateModuleExportNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule]
public partial class TestModule
{
    [JSModuleValue(Name = ""test"")]
    public static string Value = ""test"";

    [JSModuleMethod(Name = ""test"")]
    public static void Method() { }
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO011");
        Assert.NotNull(error);
        Assert.Contains("Export names must be unique", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO014 (Unmarshalable Module Method Return Type)

    [Fact]
    public void ReportsErrorForUnmarshalableModuleMethodReturnType()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Collections.Generic;

namespace TestNamespace;

[JSModule]
public partial class TestModule
{
    [JSModuleMethod]
    public static Dictionary<string, int> GetData() => null;
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO014");
        Assert.NotNull(error);
        Assert.Contains("cannot be marshaled", error.GetMessage());
    }

    #endregion

    #region Error Tests - HAKO015 (Unmarshalable Module Value Type)

    [Fact]
    public void ReportsErrorForUnmarshalableModuleValueType()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Collections.Generic;

namespace TestNamespace;

[JSModule]
public partial class TestModule
{
    [JSModuleValue]
    public static Dictionary<string, int> Data = new();
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO015");
        Assert.NotNull(error);
        Assert.Contains("cannot be marshaled", error.GetMessage());
    }

    #endregion

    #region Nullable Types Tests

    [Fact]
    public void HandlesNullableValueTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public int? NullableInt { get; set; }

    [JSProperty]
    public double? NullableDouble { get; set; }

    [JSProperty]
    public bool? NullableBool { get; set; }

    [JSMethod]
    public int? GetNullableInt(int? value) => value;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Check that nullable marshaling is generated
        Assert.Contains("HasValue", generatedCode);
        Assert.Contains(".Value", generatedCode);
    }

    [Fact]
    public void HandlesNullableReferenceTypes()
    {
        var source = @"
#nullable enable
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public string? NullableString { get; set; }

    [JSMethod]
    public string? GetString(string? input) => input;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Array Tests

    [Fact]
    public void HandlesArrayTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public int[] Numbers { get; set; }

    [JSProperty]
    public string[] Strings { get; set; }

    [JSProperty]
    public byte[] Buffer { get; set; }

    [JSMethod]
    public int[] GetNumbers() => new[] { 1, 2, 3 };

    [JSMethod]
    public void ProcessArray(int[] values) { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Check for array marshaling helpers
        Assert.Contains("ToJSArray", generatedCode);
        Assert.Contains("ToArray", generatedCode);
    }

    #endregion

    #region Optional Parameters Tests

    [Fact]
    public void HandlesOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public int Add(int a, int b = 10) => a + b;

    [JSMethod]
    public string Greet(string name = ""World"") => $""Hello, {name}"";

    [JSMethod]
    public void Process(string required, int optional = 0, bool flag = false) { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Check for optional parameter handling
        Assert.Contains("args.Length >", generatedCode);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void HandlesConstructorWithParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    public string Name { get; }
    public int Value { get; }

    [JSConstructor]
    public TestClass(string name, int value)
    {
        Name = name;
        Value = value;
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("builder.SetConstructor", generatedCode);
        Assert.Contains("new TestClass(name, value)", generatedCode);
    }

    [Fact]
    public void UsesDefaultConstructorWhenNoAttributePresent()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    public TestClass() { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("builder.SetConstructor", generatedCode);
        Assert.Contains("new TestClass()", generatedCode);
    }

    #endregion

    #region Marshaling Tests

    [Fact]
    public void GeneratesToJSValueMethod()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("public global::HakoJS.VM.JSValue ToJSValue(global::HakoJS.VM.Realm realm)", generatedCode);
        Assert.Contains("FromJSValue", generatedCode);
        Assert.Contains("global::HakoJS.SourceGeneration.IJSMarshalable<TestClass>", generatedCode);
    }

    [Fact]
    public void GeneratesInstanceTracking()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
}
";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("global::System.Collections.Concurrent.ConcurrentDictionary<int, TestClass>", generatedCode);
        Assert.Contains("global::System.Collections.Concurrent.ConcurrentDictionary<TestClass, int>", generatedCode);
        Assert.Contains("StoreInstance", generatedCode);
        Assert.Contains("GetInstance", generatedCode);
        Assert.Contains("SetFinalizer", generatedCode);
    }

    #endregion

    #region JSIgnore Tests

    [Fact]
    public void IgnoresMembersWithJSIgnoreAttribute()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    [JSIgnore]
    public string IgnoredProperty { get; set; }

    [JSMethod]
    [JSIgnore]
    public void IgnoredMethod() { }

    [JSProperty]
    public string IncludedProperty { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.DoesNotContain("ignoredProperty", generatedCode);
        Assert.DoesNotContain("ignoredMethod", generatedCode);
        Assert.Contains("includedProperty", generatedCode);
    }

    #endregion

    #region Custom Type Marshaling Tests

    [Fact]
    public void AllowsCustomTypesThatImplementIJSMarshalable()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class CustomType : IJSMarshalable<CustomType>
{
    public HakoJS.VM.JSValue ToJSValue(HakoJS.VM.Realm realm) => default;
    public static CustomType FromJSValue(HakoJS.VM.Realm realm, HakoJS.VM.JSValue jsValue) => null;
}

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public CustomType CustomProp { get; set; }

    [JSMethod]
    public CustomType GetCustom() => null;
}
";

        var result = RunGenerator(source);

        // Should have no errors because CustomType implements IJSMarshalable
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void AllowsJSClassTypesAsReturnTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Vector2
{
    [JSProperty]
    public double X { get; set; }

    [JSProperty]
    public double Y { get; set; }

    [JSConstructor]
    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }
}

[JSClass]
public partial class Transform
{
    [JSProperty]
    public Vector2 Position { get; set; }

    [JSMethod]
    public Vector2 GetPosition() => Position;

    [JSMethod]
    public Vector2 CreateVector(double x, double y) => new Vector2(x, y);
}
";

        var result = RunGenerator(source);

        // Should have no errors because Vector2 has [JSClass] and will implement IJSMarshalable
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Should generate code for both classes
        Assert.Equal(13, result.GeneratedTrees.Length);

        // Verify Vector2 generation
        var vector2Code = result.GeneratedTrees.First(t => t.FilePath.Contains("Vector2")).GetText().ToString();
        Assert.Contains(
            "partial class Vector2 : global::HakoJS.SourceGeneration.IJSBindable<Vector2>, global::HakoJS.SourceGeneration.IJSMarshalable<Vector2>",
            vector2Code);
        Assert.Contains("public global::HakoJS.VM.JSValue ToJSValue(global::HakoJS.VM.Realm realm)", vector2Code);
        Assert.Contains(
            "public static Vector2 FromJSValue(global::HakoJS.VM.Realm realm, global::HakoJS.VM.JSValue jsValue)",
            vector2Code);

        // Verify Transform generation with Vector2 marshaling
        var transformCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Transform")).GetText().ToString();
        Assert.Contains("partial class Transform : global::HakoJS.SourceGeneration.IJSBindable<Transform>",
            transformCode);
        // Should marshal Vector2 using .ToJSValue()
        Assert.Contains(".ToJSValue(ctx)", transformCode);
    }

    [Fact]
    public void AllowsJSClassTypesAsParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Vector2
{
    [JSProperty]
    public double X { get; set; }

    [JSProperty]
    public double Y { get; set; }

    [JSMethod]
    public Vector2 Add(Vector2 other)
    {
        return new Vector2 { X = X + other.X, Y = Y + other.Y };
    }

    [JSMethod]
    public void SetFrom(Vector2 source)
    {
        X = source.X;
        Y = source.Y;
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should use FromJSValue for unmarshaling parameters
        Assert.Contains("FromJSValue", generatedCode);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void GeneratesComplexClassWithAllFeatures()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSClass(Name = ""ComplexClass"")]
public partial class ComplexClass
{
    // Properties
    [JSProperty]
    public string Name { get; set; }

    [JSProperty(ReadOnly = true)]
    public int ReadOnlyValue { get; private set; }

    [JSProperty(Static = true)]
    public static string StaticProp { get; set; }

    // Constructor
    [JSConstructor]
    public ComplexClass(string name, int value)
    {
        Name = name;
        ReadOnlyValue = value;
    }

    // Instance methods
    [JSMethod]
    public string Echo(string message) => message;

    [JSMethod]
    public int Add(int a, int b = 10) => a + b;

    // Static methods
    [JSMethod(Static = true)]
    public static string StaticMethod() => ""static"";

    // Async methods
    [JSMethod]
    public async Task<string> AsyncMethod()
    {
        await Task.CompletedTask;
        return ""async result"";
    }

    // Array handling
    [JSMethod]
    public int[] GetNumbers() => new[] { 1, 2, 3 };

    [JSMethod]
    public void ProcessNumbers(int[] numbers) { }

    // Nullable types
    [JSMethod]
    public int? GetNullable(int? value) => value;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(result.GeneratedTrees);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Verify all features are present
        Assert.Contains("partial class ComplexClass", generatedCode);
        Assert.Contains("global::HakoJS.SourceGeneration.IJSBindable<ComplexClass>", generatedCode);
        Assert.Contains("global::HakoJS.SourceGeneration.IJSMarshalable<ComplexClass>", generatedCode);
        Assert.Contains("AddReadWriteProperty(\"name\"", generatedCode);
        Assert.Contains("AddReadOnlyProperty(\"readOnlyValue\"", generatedCode);
        Assert.Contains("AddReadWriteStaticProperty(\"staticProp\"", generatedCode);
        Assert.Contains("AddMethod(\"echo\"", generatedCode);
        Assert.Contains("AddStaticMethod(\"staticMethod\"", generatedCode);
        Assert.Contains("AddMethodAsync(\"asyncMethod\"", generatedCode);
        Assert.Contains("ToJSArray", generatedCode);
        Assert.Contains("ToArray", generatedCode);
    }

    [Fact]
    public void GeneratesComplexRecordWithAllFeatures()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;
using System.Threading.Tasks;

namespace TestNamespace;

[JSObject]
public partial record ComplexConfig(
    string Name,
    Action<string> OnEvent,
    Func<int, bool> Validator,
    Func<string, Task<int>> AsyncProcessor,
    int? OptionalValue = null,
    [JSPropertyName(""custom_field"")] string CustomField = ""default""
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(result.GeneratedTrees);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Verify record generation
        Assert.Contains("partial record ComplexConfig", generatedCode);
        Assert.Contains("global::HakoJS.SourceGeneration.IJSMarshalable<ComplexConfig>", generatedCode);
        Assert.Contains("global::System.IDisposable", generatedCode);

        // Verify delegate handling
        Assert.Contains("NewFunction", generatedCode);
        Assert.Contains("NewFunctionAsync", generatedCode);
        Assert.Contains("global::System.Action<", generatedCode);
        Assert.Contains("global::System.Func<", generatedCode);

        // Verify custom property name
        Assert.Contains("\"custom_field\"", generatedCode);

        // Verify disposal
        Assert.Contains("public void Dispose()", generatedCode);
        Assert.Contains("_capturedOnEvent", generatedCode);
        Assert.Contains("_capturedValidator", generatedCode);
        Assert.Contains("_capturedAsyncProcessor", generatedCode);
    }

    #endregion

    #region CamelCase Conversion Tests

    [Fact]
    public void ConvertsMemberNamesToCamelCase()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public string MyProperty { get; set; }

    [JSMethod]
    public void MyMethod() { }
}
";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("\"myProperty\"", generatedCode);
        Assert.Contains("\"myMethod\"", generatedCode);
    }

    [Fact]
    public void AllowsCustomJavaScriptNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty(Name = ""custom_prop"")]
    public string MyProperty { get; set; }

    [JSMethod(Name = ""custom_method"")]
    public void MyMethod() { }
}
";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("\"custom_prop\"", generatedCode);
        Assert.Contains("\"custom_method\"", generatedCode);
    }

    #endregion

    #region Delegate Parameter Naming Tests

    [Fact]
    public void UsesNamedDelegateParameterNames()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

public delegate int Adder(int x, int y);
public delegate string Formatter(string firstName, string lastName);

[JSObject]
public partial record Calculator(
    Adder Add,
    Formatter Format
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should use actual parameter names from named delegate
        Assert.Contains("var x =", generatedCode);
        Assert.Contains("var y =", generatedCode);
        Assert.Contains("Add(x, y)", generatedCode);

        Assert.Contains("var firstName =", generatedCode);
        Assert.Contains("var lastName =", generatedCode);
        Assert.Contains("Format(firstName, lastName)", generatedCode);

        // Should NOT use generic arg0, arg1 for named delegates
        Assert.DoesNotContain("Add(arg0, arg1)", generatedCode);
        Assert.DoesNotContain("Format(arg0, arg1)", generatedCode);
    }

    [Fact]
    public void UsesFuncActionGenericParameterNames()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Calculator(
    Func<int, int, int> Add,
    Action<string, int> Log,
    Func<double, double, double, double> Calculate
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should use generic arg0, arg1, etc. for Func/Action
        Assert.Contains("var arg0 =", generatedCode);
        Assert.Contains("var arg1 =", generatedCode);
        Assert.Contains("Add(arg0, arg1)", generatedCode);

        Assert.Contains("Log(arg0, arg1)", generatedCode);

        Assert.Contains("var arg2 =", generatedCode);
        Assert.Contains("Calculate(arg0, arg1, arg2)", generatedCode);
    }

    [Fact]
    public void UsesNamedDelegateParameterNamesInFromJSValue()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

public delegate bool Validator(int value, string context);

[JSObject]
public partial record Config(
    Validator Validate
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // In FromJSValue, should create delegate with actual parameter names
        Assert.Contains("int value", generatedCode);
        Assert.Contains("string context", generatedCode);
        Assert.Contains("using var valueJs = realm.NewValue(value)", generatedCode);
        Assert.Contains("using var contextJs = realm.NewValue(context)", generatedCode);

        // Should NOT use generic names
        Assert.DoesNotContain("int arg0", generatedCode);
        Assert.DoesNotContain("string arg1", generatedCode);
    }

    [Fact]
    public void UsesFuncActionGenericParameterNamesInFromJSValue()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Config(
    Func<string, int, bool> Process
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // In FromJSValue, should use generic arg0, arg1 for Func
        Assert.Contains("string arg0", generatedCode);
        Assert.Contains("int arg1", generatedCode);
        Assert.Contains("using var arg0Js = realm.NewValue(arg0)", generatedCode);
        Assert.Contains("using var arg1Js = realm.NewValue(arg1)", generatedCode);
    }

    [Fact]
    public void UsesNamedDelegateParameterNamesWithOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

public delegate int Calculator(int x, int y = 10, int z = 20);

[JSObject]
public partial record Config(
    Calculator Calculate
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should use actual parameter names with defaults
        Assert.Contains("var x =", generatedCode);
        Assert.Contains("var y =", generatedCode);
        Assert.Contains("var z =", generatedCode);
        Assert.Contains("Calculate(x, y, z)", generatedCode);

        // Should handle optional parameters
        Assert.Contains("args.Length > 1", generatedCode);
        Assert.Contains("args.Length > 2", generatedCode);
    }

    [Fact]
    public void MixesNamedDelegatesAndFuncAction()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;
using System.Threading.Tasks;

namespace TestNamespace;

public delegate void Logger(string message, int level);
public delegate Task<string> AsyncFetcher(string url, int timeout);

[JSObject]
public partial record MixedConfig(
    Logger Log,
    Func<int, int, int> Add,
    AsyncFetcher Fetch,
    Action<string> Notify
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Named delegate Logger should use actual names
        Assert.Contains("var message =", generatedCode);
        Assert.Contains("var level =", generatedCode);
        Assert.Contains("Log(message, level)", generatedCode);

        // Func should use generic names
        Assert.Contains("Add(arg0, arg1)", generatedCode);

        // Named async delegate should use actual names
        Assert.Contains("var url =", generatedCode);
        Assert.Contains("var timeout =", generatedCode);
        Assert.Contains("await Fetch(url, timeout)", generatedCode);

        // Action should use generic name
        Assert.Contains("Notify(arg0)", generatedCode);
    }

    [Fact]
    public void HandlesNamedDelegateWithComplexParameterTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

public delegate bool Validator(int? value, string[] tags, double threshold);

[JSObject]
public partial record Config(
    Validator Validate
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should use actual parameter names even with complex types
        Assert.Contains("int? value", generatedCode);
        Assert.Contains("string[] tags", generatedCode);
        Assert.Contains("double threshold", generatedCode);
        Assert.Contains("Validate(value, tags, threshold)", generatedCode);
    }

    [Fact]
    public void ValidatesNamedDelegateParameterNamesInErrorMessages()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

public delegate int Adder(int firstNumber, int secondNumber);

[JSObject]
public partial record Calculator(
    Adder Add
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Error messages should use the actual parameter names
        Assert.Contains("\"Parameter 'firstNumber'", generatedCode);
        Assert.Contains("\"Parameter 'secondNumber'", generatedCode);
    }

    [Fact]
    public void ValidatesFuncActionParameterNamesInErrorMessages()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Calculator(
    Func<int, int, int> Add
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Error messages should use generic parameter names for Func/Action
        Assert.Contains("\"Parameter 'arg0'", generatedCode);
        Assert.Contains("\"Parameter 'arg1'", generatedCode);
    }

    #endregion

// Add these tests to the JSBindingGeneratorTests class

    #region TypeScript Definition Tests - Classes

    [Fact]
    public void GeneratesTypeScriptDefinitionForBasicClass()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass(Name = ""MyClass"")]
public partial class TestClass
{
    [JSConstructor]
    public TestClass() { }
    
    [JSProperty]
    public string Name { get; set; }
    
    [JSMethod]
    public int GetValue() => 42;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should implement IDefinitelyTyped
        Assert.Contains("global::HakoJS.SourceGeneration.IDefinitelyTyped<TestClass>", generatedCode);

        // Should have TypeDefinition property
        Assert.Contains("public static string TypeDefinition", generatedCode);

        // Should contain class declaration
        Assert.Contains("declare class MyClass {", generatedCode);
        Assert.Contains("constructor();", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("getValue(): number;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithReadOnlyProperties()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty(ReadOnly = true)]
    public string ReadOnlyProp { get; set; }
    
    [JSProperty]
    public int WritableProp { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("readonly readOnlyProp: string;", generatedCode);
        Assert.Contains("writableProp: number;", generatedCode);
        Assert.DoesNotContain("readonly writableProp", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithStaticMembers()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty(Static = true)]
    public static string StaticProp { get; set; }
    
    [JSMethod(Static = true)]
    public static int StaticMethod() => 0;
    
    [JSProperty]
    public string InstanceProp { get; set; }
    
    [JSMethod]
    public void InstanceMethod() { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("static staticProp: string;", generatedCode);
        Assert.Contains("static staticMethod(): number;", generatedCode);
        Assert.DoesNotContain("static instanceProp", generatedCode);
        Assert.DoesNotContain("static instanceMethod", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithAsyncMethods()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public async Task AsyncVoid()
    {
        await Task.CompletedTask;
    }
    
    [JSMethod]
    public async Task<string> AsyncString()
    {
        await Task.CompletedTask;
        return ""result"";
    }
    
    [JSMethod]
    public Task<int> TaskInt()
    {
        return Task.FromResult(42);
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("asyncVoid(): Promise<void>;", generatedCode);
        Assert.Contains("asyncString(): Promise<string>;", generatedCode);
        Assert.Contains("taskInt(): Promise<number>;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSMethod]
    public int Add(int a, int b = 10) => a + b;
    
    [JSMethod]
    public string Format(string text, bool uppercase = false, string prefix = "">"")
    {
        return text;
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("add(a: number, b?: number): number;", generatedCode);
        Assert.Contains("format(text: string, uppercase?: boolean, prefix?: string): string;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithArrayTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public int[] Numbers { get; set; }
    
    [JSProperty]
    public string[] Strings { get; set; }
    
    [JSMethod]
    public double[] GetDoubles() => null;
    
    [JSMethod]
    public void ProcessArray(bool[] flags) { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("numbers: number[];", generatedCode);
        Assert.Contains("strings: string[];", generatedCode);
        Assert.Contains("getDoubles(): number[];", generatedCode);
        Assert.Contains("processArray(flags: boolean[]): void;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithNullableTypes()
    {
        var source = @"
#nullable enable
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public string? NullableString { get; set; }
    
    [JSProperty]
    public int? NullableInt { get; set; }
    
    [JSProperty]
    public double? NullableDouble { get; set; }
    
    [JSMethod]
    public string? GetNullableString(int? value) => null;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("nullableString: string | null;", generatedCode);
        Assert.Contains("nullableInt: number | null;", generatedCode);
        Assert.Contains("nullableDouble: number | null;", generatedCode);
        Assert.Contains("getNullableString(value: number | null): string | null;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithByteArray()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public byte[] Buffer { get; set; }
    
    [JSMethod]
    public byte[] GetBuffer() => null;
    
    [JSMethod]
    public void SetBuffer(byte[] data) { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("buffer: ArrayBuffer;", generatedCode);
        Assert.Contains("getBuffer(): ArrayBuffer;", generatedCode);
        Assert.Contains("setBuffer(data: ArrayBuffer): void;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithTypedArrays()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class TestClass
{
    [JSProperty]
    public HakoJS.SourceGeneration.Uint8ArrayValue Uint8Data { get; set; }
    
    [JSProperty]
    public HakoJS.SourceGeneration.Int32ArrayValue Int32Data { get; set; }
    
    [JSProperty]
    public HakoJS.SourceGeneration.Float64ArrayValue Float64Data { get; set; }
    
    [JSMethod]
    public HakoJS.SourceGeneration.Uint16ArrayValue GetUint16() => default;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("uint8Data: Uint8Array;", generatedCode);
        Assert.Contains("int32Data: Int32Array;", generatedCode);
        Assert.Contains("float64Data: Float64Array;", generatedCode);
        Assert.Contains("getUint16(): Uint16Array;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionWithCustomJSClassType()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Vector2
{
    [JSProperty]
    public double X { get; set; }
    
    [JSProperty]
    public double Y { get; set; }
}

[JSClass]
public partial class Transform
{
    [JSProperty]
    public Vector2 Position { get; set; }
    
    [JSMethod]
    public Vector2 GetPosition() => null;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var transformCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Transform")).GetText().ToString();

        // Should reference the custom type by its simple name
        Assert.Contains("position: Vector2;", transformCode);
        Assert.Contains("getPosition(): Vector2;", transformCode);
    }

    #endregion

    #region TypeScript Definition Tests - Modules

    [Fact]
    public void GeneratesTypeScriptDefinitionForBasicModule()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule(Name = ""myModule"")]
public partial class MyModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";
    
    [JSModuleMethod]
    public static int Add(int a, int b) => a + b;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should implement IDefinitelyTyped
        Assert.Contains("global::HakoJS.SourceGeneration.IDefinitelyTyped<MyModule>", generatedCode);

        // Should have module declaration
        Assert.Contains("declare module 'myModule' {", generatedCode);
        Assert.Contains("export const version: string;", generatedCode);
        Assert.Contains("export function add(a: number, b: number): number;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForModuleWithAsyncMethods()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSModule]
public partial class MyModule
{
    [JSModuleMethod]
    public static async Task<string> FetchData()
    {
        await Task.CompletedTask;
        return ""data"";
    }
    
    [JSModuleMethod]
    public static Task<int> GetCount()
    {
        return Task.FromResult(42);
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("export function fetchData(): Promise<string>;", generatedCode);
        Assert.Contains("export function getCount(): Promise<number>;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForModuleWithOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule]
public partial class MyModule
{
    [JSModuleMethod]
    public static string Format(string text, bool uppercase = false)
    {
        return text;
    }
    
    [JSModuleMethod]
    public static int Calculate(int x, int y = 10, int z = 20)
    {
        return x + y + z;
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("export function format(text: string, uppercase?: boolean): string;", generatedCode);
        Assert.Contains("export function calculate(x: number, y?: number, z?: number): number;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForModuleWithExportedClasses()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class MyClass
{
    [JSProperty]
    public string Name { get; set; }
}

[JSModule]
[JSModuleClass(ClassType = typeof(MyClass), ExportName = ""MyClass"")]
public partial class MyModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        Assert.Contains("declare module 'MyModule' {", moduleCode);
        Assert.Contains("export class MyClass {", moduleCode);
        Assert.Contains("constructor();", moduleCode);
        Assert.Contains("name: string;", moduleCode);
        Assert.Contains("}", moduleCode);
        Assert.Contains("export const version: string;", moduleCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForModuleWithNullableTypes()
    {
        var source = @"
#nullable enable
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule]
public partial class MyModule
{
    [JSModuleValue]
    public static string? NullableValue = null;
    
    [JSModuleMethod]
    public static string? GetNullable(int? input) => null;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("export const nullableValue: string | null;", generatedCode);
        Assert.Contains("export function getNullable(input: number | null): string | null;", generatedCode);
    }

    #endregion

    #region TypeScript Definition Tests - Objects/Records

    [Fact]
    public void GeneratesTypeScriptDefinitionForBasicRecord()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record UserProfile(string Name, int Age, string Email);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should implement IDefinitelyTyped
        Assert.Contains("global::HakoJS.SourceGeneration.IDefinitelyTyped<UserProfile>", generatedCode);

        // Should have interface definition
        Assert.Contains("interface UserProfile {", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("age: number;", generatedCode);
        Assert.Contains("email: string;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config(
    string Name,
    int Port = 8080,
    string? Host = null,
    bool Enabled = true
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("port?: number;", generatedCode);
        Assert.Contains("host?: string | null;", generatedCode);
        Assert.Contains("enabled?: boolean;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithCustomPropertyNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record ApiRequest(
    [JSPropertyName(""api_key"")] string ApiKey,
    [JSPropertyName(""user_id"")] int UserId
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("api_key: string;", generatedCode);
        Assert.Contains("user_id: number;", generatedCode);
        Assert.DoesNotContain("apiKey:", generatedCode);
        Assert.DoesNotContain("userId:", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithActionDelegate()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record EventConfig(
    string EventName,
    Action<string> OnEvent,
    Action<int, bool> OnData
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("eventName: string;", generatedCode);
        Assert.Contains("onEvent: (arg0: string) => void;", generatedCode);
        Assert.Contains("onData: (arg0: number, arg1: boolean) => void;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithFuncDelegate()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Calculator(
    Func<int, int, int> Add,
    Func<string, bool> Validate,
    Func<double> GetValue
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("add: (arg0: number, arg1: number) => number;", generatedCode);
        Assert.Contains("validate: (arg0: string) => boolean;", generatedCode);
        Assert.Contains("getValue: () => number;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithNamedDelegate()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

public delegate bool Validator(string input, int maxLength);
public delegate string Formatter(string firstName, string lastName);

[JSObject]
public partial record Config(
    Validator Validate,
    Formatter Format
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("validate: (input: string, maxLength: number) => boolean;", generatedCode);
        Assert.Contains("format: (firstName: string, lastName: string) => string;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithAsyncDelegates()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;
using System.Threading.Tasks;

namespace TestNamespace;

[JSObject]
public partial record AsyncConfig(
    Func<string, Task<int>> FetchData,
    Func<Task> Initialize,
    Func<int, int, Task<bool>> Validate
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("fetchData: (arg0: string) => Promise<number>;", generatedCode);
        Assert.Contains("initialize: () => Promise<void>;", generatedCode);
        Assert.Contains("validate: (arg0: number, arg1: number) => Promise<boolean>;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithOptionalDelegates()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Config(
    string Name,
    Action<string>? OnChange = null,
    Func<int, bool>? Validator = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("onChange?: (arg0: string) => void;", generatedCode);
        Assert.Contains("validator?: (arg0: number) => boolean;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithArrayTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record DataSet(
    int[] Numbers,
    string[] Tags,
    byte[] Buffer,
    double[]? OptionalValues = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("numbers: number[];", generatedCode);
        Assert.Contains("tags: string[];", generatedCode);
        Assert.Contains("buffer: ArrayBuffer;", generatedCode);
        Assert.Contains("optionalValues?: number[] | null;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithTypedArrays()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record TypedArrayData(
    HakoJS.SourceGeneration.Uint8ArrayValue Uint8,
    HakoJS.SourceGeneration.Float32ArrayValue Float32,
    HakoJS.SourceGeneration.Int32ArrayValue? OptionalInt32 = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("uint8: Uint8Array;", generatedCode);
        Assert.Contains("float32: Float32Array;", generatedCode);
        Assert.Contains("optionalInt32?: Int32Array | null;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithNestedRecord()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Address(string Street, string City);

[JSObject]
public partial record Person(string Name, Address HomeAddress);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var addressCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Address")).GetText().ToString();
        var personCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Person")).GetText().ToString();

        // Address interface
        Assert.Contains("interface Address {", addressCode);
        Assert.Contains("street: string;", addressCode);
        Assert.Contains("city: string;", addressCode);

        // Person interface with Address type
        Assert.Contains("interface Person {", personCode);
        Assert.Contains("name: string;", personCode);
        Assert.Contains("homeAddress: Address;", personCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForRecordWithAllPrimitiveTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record AllTypes(
    string StringVal,
    int IntVal,
    long LongVal,
    short ShortVal,
    byte ByteVal,
    double DoubleVal,
    float FloatVal,
    bool BoolVal,
    int? NullableInt,
    string? NullableString
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("stringVal: string;", generatedCode);
        Assert.Contains("intVal: number;", generatedCode);
        Assert.Contains("longVal: number;", generatedCode);
        Assert.Contains("shortVal: number;", generatedCode);
        Assert.Contains("byteVal: number;", generatedCode);
        Assert.Contains("doubleVal: number;", generatedCode);
        Assert.Contains("floatVal: number;", generatedCode);
        Assert.Contains("boolVal: boolean;", generatedCode);
        Assert.Contains("nullableInt: number | null;", generatedCode);
        Assert.Contains("nullableString: string | null;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForComplexRecord()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;
using System.Threading.Tasks;

namespace TestNamespace;

public delegate void Logger(string message, int level);

[JSObject]
public partial record ComplexConfig(
    string Name,
    int Port,
    Action<string> OnStart,
    Func<int, Task<bool>> Validator,
    Logger Log,
    int[] AllowedPorts,
    string? Host = null,
    bool Enabled = true
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("interface ComplexConfig {", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("port: number;", generatedCode);
        Assert.Contains("onStart: (arg0: string) => void;", generatedCode);
        Assert.Contains("validator: (arg0: number) => Promise<boolean>;", generatedCode);
        Assert.Contains("log: (message: string, level: number) => void;", generatedCode);
        Assert.Contains("allowedPorts: number[];", generatedCode);
        Assert.Contains("host?: string | null;", generatedCode);
        Assert.Contains("enabled?: boolean;", generatedCode);
    }

    #endregion

    #region TypeScript Definition Tests - Edge Cases

    [Fact]
    public void GeneratesTypeScriptDefinitionWithEscapedStrings()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule(Name = ""my-module"")]
public partial class MyModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should properly escape module name in declare module statement
        Assert.Contains("declare module 'my-module' {", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForEmptyClass()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class EmptyClass
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should still generate TypeDefinition with empty class
        Assert.Contains("declare class EmptyClass {", generatedCode);
        Assert.Contains("}", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDefinitionForEmptyModule()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSModule(Name = ""emptyModule"")]
public partial class EmptyModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should still generate TypeDefinition with empty module
        Assert.Contains("declare module 'emptyModule' {", generatedCode);
    }

    #endregion

    #region XML Documentation / TSDoc Tests

    [Fact]
    public void GeneratesTsDocForClassWithSummary()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

/// <summary>
/// Represents a mathematical vector in 2D space.
/// </summary>
[JSClass]
public partial class Vector2
{
    [JSProperty]
    public double X { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain TSDoc comment
        Assert.Contains("/**", generatedCode);
        Assert.Contains("* Represents a mathematical vector in 2D space.", generatedCode);
        Assert.Contains("*/", generatedCode);
        Assert.Contains("declare class Vector2", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForMethodWithParamAndReturns()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Calculator
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    /// <param name=""a"">The first number</param>
    /// <param name=""b"">The second number</param>
    /// <returns>The sum of a and b</returns>
    [JSMethod]
    public int Add(int a, int b) => a + b;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain method documentation
        Assert.Contains("/**", generatedCode);
        Assert.Contains("* Adds two numbers together.", generatedCode);
        Assert.Contains("* @param a The first number", generatedCode);
        Assert.Contains("* @param b The second number", generatedCode);
        Assert.Contains("* @returns The sum of a and b", generatedCode);
        Assert.Contains("*/", generatedCode);
        Assert.Contains("add(a: number, b: number): number;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForPropertyWithSummary()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Person
{
    /// <summary>
    /// Gets or sets the person's full name.
    /// </summary>
    [JSProperty]
    public string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the person's age in years.
    /// </summary>
    [JSProperty]
    public int Age { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain property documentation
        Assert.Contains("* Gets or sets the person's full name.", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("* Gets or sets the person's age in years.", generatedCode);
        Assert.Contains("age: number;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForConstructorWithParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Rectangle
{
    /// <summary>
    /// Creates a new rectangle with the specified dimensions.
    /// </summary>
    /// <param name=""width"">The width of the rectangle</param>
    /// <param name=""height"">The height of the rectangle</param>
    [JSConstructor]
    public Rectangle(double width, double height)
    {
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain constructor documentation
        Assert.Contains("* Creates a new rectangle with the specified dimensions.", generatedCode);
        Assert.Contains("* @param width The width of the rectangle", generatedCode);
        Assert.Contains("* @param height The height of the rectangle", generatedCode);
        Assert.Contains("constructor(width: number, height: number);", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForModuleWithSummary()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

/// <summary>
/// Provides utility functions for mathematical operations.
/// </summary>
[JSModule(Name = ""math"")]
public partial class MathModule
{
    /// <summary>
    /// The value of PI (approximately 3.14159).
    /// </summary>
    [JSModuleValue]
    public static double Pi = 3.14159;
    
    /// <summary>
    /// Calculates the square of a number.
    /// </summary>
    /// <param name=""x"">The number to square</param>
    /// <returns>The square of x</returns>
    [JSModuleMethod]
    public static double Square(double x) => x * x;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain module documentation
        Assert.Contains("* Provides utility functions for mathematical operations.", generatedCode);
        Assert.Contains("declare module 'math'", generatedCode);

        // Should contain value documentation
        Assert.Contains("* The value of PI (approximately 3.14159).", generatedCode);
        Assert.Contains("export const pi: number;", generatedCode);

        // Should contain method documentation
        Assert.Contains("* Calculates the square of a number.", generatedCode);
        Assert.Contains("* @param x The number to square", generatedCode);
        Assert.Contains("* @returns The square of x", generatedCode);
        Assert.Contains("export function square(x: number): number;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForRecordWithParameterDocs()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

/// <summary>
/// Represents a user's profile information.
/// </summary>
/// <param name=""Name"">The user's full name</param>
/// <param name=""Email"">The user's email address</param>
/// <param name=""Age"">The user's age in years</param>
[JSObject]
public partial record UserProfile(string Name, string Email, int Age);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain record documentation
        Assert.Contains("* Represents a user's profile information.", generatedCode);
        Assert.Contains("interface UserProfile", generatedCode);

        // Should contain parameter documentation
        Assert.Contains("* The user's full name", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("* The user's email address", generatedCode);
        Assert.Contains("email: string;", generatedCode);
        Assert.Contains("* The user's age in years", generatedCode);
        Assert.Contains("age: number;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForRecordWithDelegateParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

/// <summary>
/// Configuration for event handling.
/// </summary>
/// <param name=""EventName"">The name of the event to listen for</param>
/// <param name=""OnEvent"">Callback function invoked when the event occurs</param>
[JSObject]
public partial record EventConfig(string EventName, Action<string> OnEvent);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain record and parameter documentation
        Assert.Contains("* Configuration for event handling.", generatedCode);
        Assert.Contains("* The name of the event to listen for", generatedCode);
        Assert.Contains("eventName: string;", generatedCode);
        Assert.Contains("* Callback function invoked when the event occurs", generatedCode);
        Assert.Contains("onEvent: (arg0: string) => void;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocWithRemarksSection()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class DataProcessor
{
    /// <summary>
    /// Processes the input data and returns the result.
    /// </summary>
    /// <remarks>
    /// This method may take a long time for large datasets.
    /// Consider using the async version for better performance.
    /// </remarks>
    /// <param name=""data"">The data to process</param>
    /// <returns>The processed result</returns>
    [JSMethod]
    public string Process(string data) => data;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should contain both summary and remarks
        Assert.Contains("* Processes the input data and returns the result.", generatedCode);
        Assert.Contains("* This method may take a long time for large datasets.", generatedCode);
        Assert.Contains("* Consider using the async version for better performance.", generatedCode);
        Assert.Contains("* @param data The data to process", generatedCode);
        Assert.Contains("* @returns The processed result", generatedCode);
    }

    [Fact]
    public void HandlesMultilineDocumentation()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class ComplexCalculator
{
    /// <summary>
    /// Performs a complex mathematical calculation.
    /// This operation involves multiple steps:
    /// 1. Validation
    /// 2. Transformation
    /// 3. Computation
    /// </summary>
    /// <param name=""input"">
    /// The input value to process.
    /// Must be a positive number.
    /// </param>
    /// <returns>
    /// The calculated result.
    /// Returns null if validation fails.
    /// </returns>
    [JSMethod]
    public double? Calculate(double input) => input;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should preserve multiline documentation
        Assert.Contains("* Performs a complex mathematical calculation.", generatedCode);
        Assert.Contains("* This operation involves multiple steps:", generatedCode);
        Assert.Contains("* 1. Validation", generatedCode);
        Assert.Contains("* 2. Transformation", generatedCode);
        Assert.Contains("* 3. Computation", generatedCode);

        Assert.Contains("* @param input", generatedCode);
        Assert.Contains("The input value to process.", generatedCode);
        Assert.Contains("Must be a positive number.", generatedCode);

        Assert.Contains("* @returns", generatedCode);
        Assert.Contains("The calculated result.", generatedCode);
        Assert.Contains("Returns null if validation fails.", generatedCode);
    }

    [Fact]
    public void WorksWhenDocumentationIsMissing()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class UndocumentedClass
{
    [JSProperty]
    public string Name { get; set; }
    
    [JSMethod]
    public int Calculate(int x) => x * 2;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should still generate valid TypeScript without documentation
        Assert.Contains("declare class UndocumentedClass {", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("calculate(x: number): number;", generatedCode);

        // Should not have empty TSDoc comments
        Assert.DoesNotContain("/**\n  */", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForStaticMembers()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Config
{
    /// <summary>
    /// The current application version.
    /// </summary>
    [JSProperty(Static = true)]
    public static string Version { get; set; }
    
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    /// <returns>A new Config instance with default values</returns>
    [JSMethod(Static = true)]
    public static Config GetDefault() => null;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should document static members
        Assert.Contains("* The current application version.", generatedCode);
        Assert.Contains("static version: string;", generatedCode);

        Assert.Contains("* Gets the default configuration.", generatedCode);
        Assert.Contains("* @returns A new Config instance with default values", generatedCode);
        Assert.Contains("static getDefault(): Config;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForAsyncMethods()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSClass]
public partial class ApiClient
{
    /// <summary>
    /// Fetches data from the remote server.
    /// </summary>
    /// <param name=""endpoint"">The API endpoint to call</param>
    /// <returns>A task that resolves to the fetched data</returns>
    [JSMethod]
    public async Task<string> FetchData(string endpoint)
    {
        await Task.CompletedTask;
        return ""data"";
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should document async methods
        Assert.Contains("* Fetches data from the remote server.", generatedCode);
        Assert.Contains("* @param endpoint The API endpoint to call", generatedCode);
        Assert.Contains("* @returns A task that resolves to the fetched data", generatedCode);
        Assert.Contains("fetchData(endpoint: string): Promise<string>;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForModuleExportedClasses()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

/// <summary>
/// A simple counter class.
/// </summary>
[JSClass]
public partial class Counter
{
    /// <summary>
    /// Gets or sets the current count.
    /// </summary>
    [JSProperty]
    public int Count { get; set; }
    
    /// <summary>
    /// Increments the counter by one.
    /// </summary>
    [JSMethod]
    public void Increment() { }
}

/// <summary>
/// Provides counter utilities.
/// </summary>
[JSModule]
[JSModuleClass(ClassType = typeof(Counter))]
public partial class CounterModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Module should have documentation
        Assert.Contains("* Provides counter utilities.", moduleCode);

        // Exported class should have documentation
        Assert.Contains("* A simple counter class.", moduleCode);
        Assert.Contains("export class Counter {", moduleCode);

        // Class members should have documentation
        Assert.Contains("* Gets or sets the current count.", moduleCode);
        Assert.Contains("count: number;", moduleCode);
        Assert.Contains("* Increments the counter by one.", moduleCode);
        Assert.Contains("increment(): void;", moduleCode);
    }

    [Fact]
    public void GeneratesTsDocWithComplexMarkup()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Formatter
{
    /// <summary>
    /// Formats text with special characters: &lt;, &gt;, &amp;, &quot;, &apos;
    /// </summary>
    /// <param name=""text"">The text to format (e.g., ""Hello, World!"")</param>
    /// <returns>The formatted text</returns>
    [JSMethod]
    public string Format(string text) => text;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should handle XML entities in documentation
        Assert.Contains("/**", generatedCode);
        Assert.Contains("*/", generatedCode);
        Assert.Contains("format(text: string): string;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForOptionalParametersWithDocs()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class Logger
{
    /// <summary>
    /// Logs a message with optional severity level.
    /// </summary>
    /// <param name=""message"">The message to log</param>
    /// <param name=""level"">The severity level (default: 0)</param>
    /// <param name=""category"">The log category (default: ""General"")</param>
    [JSMethod]
    public void Log(string message, int level = 0, string category = ""General"")
    {
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("* Logs a message with optional severity level.", generatedCode);
        Assert.Contains("* @param message The message to log", generatedCode);
        Assert.Contains("* @param level The severity level (default: 0)", generatedCode);
        Assert.Contains("* @param category The log category (default: \"\"General\"\")", generatedCode);
        Assert.Contains("log(message: string, level?: number, category?: string): void;", generatedCode);
    }


    [Fact]
    public void GeneratesTSDocWithAdvancedMarkdownFormatting()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

/// <summary>
/// A utility class for data processing.
/// </summary>
[JSClass]
public partial class DataUtils
{
    /// <summary>
    /// Processes data with <b>advanced</b> features and <i>custom</i> options.
    /// Use <c>ProcessAsync</c> for better performance.
    /// </summary>
    /// <remarks>
    /// This method supports the following operations:
    /// <list type=""bullet"">
    /// <item><term>Validation</term><description>Checks data integrity</description></item>
    /// <item><term>Transformation</term><description>Converts data format</description></item>
    /// <item><term>Compression</term><description>Reduces data size</description></item>
    /// </list>
    /// 
    /// For more information, see <see href=""https://example.com/docs"">the documentation</see>.
    /// </remarks>
    /// <param name=""data"">The input data to process. Must not be <c>null</c>.</param>
    /// <param name=""options"">Processing options. See <see cref=""ProcessingOptions""/> for details.</param>
    /// <returns>
    /// The processed data as a string.
    /// Returns <c>null</c> if processing fails.
    /// </returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// var utils = new DataUtils();
    /// var result = utils.Process(""data"", options);
    /// </code>
    /// </example>
    [JSMethod]
    public string? Process(string data, string options) => data;

    /// <summary>
    /// Validates input using the <paramref name=""validator""/> function.
    /// </summary>
    /// <param name=""input"">The input to validate</param>
    /// <param name=""validator"">The validation function</param>
    /// <returns><see langword=""true""/> if valid, <see langword=""false""/> otherwise</returns>
    [JSMethod]
    public bool Validate(string input, Func<string, bool> validator) => true;
}

[JSClass]
public partial class ProcessingOptions
{
    [JSProperty]
    public string Mode { get; set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees.First(t => t.FilePath.Contains("DataUtils")).GetText().ToString();

        var typeDefStart = generatedCode.IndexOf("return @\"") + 9;
        var typeDefEnd = generatedCode.IndexOf("\";", typeDefStart);
        var typeDef = generatedCode.Substring(typeDefStart, typeDefEnd - typeDefStart);

        Assert.Contains("**advanced**", typeDef);
        Assert.Contains("*custom*", typeDef);
        Assert.Contains("`ProcessAsync`", typeDef);
        Assert.Contains("`null`", typeDef);
        Assert.Contains("`ProcessingOptions`", typeDef);
        Assert.Contains("- **Validation**: Checks data integrity", typeDef);
        Assert.Contains("- **Transformation**: Converts data format", typeDef);
        Assert.Contains("- **Compression**: Reduces data size", typeDef);
        Assert.Contains("[the documentation](https://example.com/docs)", typeDef);
        Assert.Contains("```", typeDef);
        Assert.Contains("var utils = new DataUtils();", typeDef);
        Assert.Contains("**Example:**", typeDef);
        Assert.Contains("`validator`", typeDef);
        Assert.Contains("`true`", typeDef);
        Assert.Contains("`false`", typeDef);
        Assert.Contains("@param data", typeDef);
        Assert.Contains("@param options", typeDef);
        Assert.Contains("@returns", typeDef);
        Assert.Contains("process(data: string, options: string): string | null;", typeDef);
        Assert.Contains("validate(input: string, validator: (arg0: string) => boolean): boolean;", typeDef);
        Assert.Contains("declare class DataUtils", typeDef);
    }

    #endregion

    #region DateTime Tests

    [Fact]
    public void HandlesDateTimeProperties()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class Event
{
    [JSProperty]
    public DateTime StartTime { get; set; }

    [JSProperty]
    public DateTime? EndTime { get; set; }

    [JSProperty(ReadOnly = true)]
    public DateTime CreatedAt { get; private set; }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should marshal DateTime using NewDate
        Assert.Contains("ctx.NewDate(", generatedCode);

        // Should unmarshal DateTime using AsDateTime
        Assert.Contains("AsDateTime()", generatedCode);

        // Should check IsDate for validation
        Assert.Contains("IsDate()", generatedCode);
    }

    [Fact]
    public void HandlesDateTimeMethodParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class Calendar
{
    [JSMethod]
    public bool IsWeekend(DateTime date) => true;

    [JSMethod]
    public void Schedule(DateTime start, DateTime? end = null) { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should validate date parameter is a Date
        Assert.Contains("IsDate()", generatedCode);
        Assert.Contains("AsDateTime()", generatedCode);
        Assert.Contains("\"Parameter 'date' must be a Date\"", generatedCode);
    }

    [Fact]
    public void HandlesDateTimeReturnTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class TimeService
{
    [JSMethod]
    public DateTime Now() => DateTime.Now;

    [JSMethod]
    public DateTime? FindEvent(string name) => null;

    [JSMethod(Static = true)]
    public static DateTime GetUtcNow() => DateTime.UtcNow;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should marshal return values with NewDate
        Assert.Contains("ctx.NewDate(", generatedCode);

        // Should handle nullable DateTime
        Assert.Contains("ctx.Null()", generatedCode);
    }

    [Fact]
    public void HandlesDateTimeArrays()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class EventLog
{
    [JSProperty]
    public DateTime[] Timestamps { get; set; }

    [JSMethod]
    public DateTime[] GetEventTimes() => null;

    [JSMethod]
    public void ProcessDates(DateTime[] dates) { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should use ToJSArray for marshaling arrays
        Assert.Contains("ToJSArray", generatedCode);

        // Should use ToArray for unmarshaling arrays
        Assert.Contains("ToArrayOf<global::System.DateTime>();", generatedCode);
    }

    [Fact]
    public void HandlesDateTimeInRecords()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Appointment(
    string Title,
    DateTime StartTime,
    DateTime? EndTime = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // ToJSValue should marshal DateTime
        Assert.Contains("realm.NewDate(StartTime)", generatedCode);

        // FromJSValue should unmarshal DateTime
        Assert.Contains("IsDate()", generatedCode);
        Assert.Contains("AsDateTime()", generatedCode);
    }

    [Fact]
    public void HandlesDateTimeInModules()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSModule]
public partial class TimeModule
{
    [JSModuleValue]
    public static DateTime ServerStartTime = DateTime.Now;

    [JSModuleMethod]
    public static DateTime GetCurrentTime() => DateTime.Now;

    [JSModuleMethod]
    public static bool IsExpired(DateTime expiryDate) => false;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("ctx.NewDate(", generatedCode);
        Assert.Contains("AsDateTime()", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDateForDateTime()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class DateHandler
{
    [JSProperty]
    public DateTime Date { get; set; }

    [JSProperty]
    public DateTime? OptionalDate { get; set; }

    [JSMethod]
    public DateTime GetDate(DateTime input) => input;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should map DateTime to Date in TypeScript
        Assert.Contains("date: Date;", generatedCode);
        Assert.Contains("optionalDate: Date | null;", generatedCode);
        Assert.Contains("getDate(input: Date): Date;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDateArrayForDateTimeArray()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class Timeline
{
    [JSProperty]
    public DateTime[] Events { get; set; }

    [JSMethod]
    public DateTime[] GetDates() => null;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should map DateTime[] to Date[] in TypeScript
        Assert.Contains("events: Date[];", generatedCode);
        Assert.Contains("getDates(): Date[];", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDateForRecordDateTime()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record Event(
    string Name,
    DateTime StartDate,
    DateTime? EndDate = null
);
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should map DateTime to Date in TypeScript interface
        Assert.Contains("interface Event {", generatedCode);
        Assert.Contains("name: string;", generatedCode);
        Assert.Contains("startDate: Date;", generatedCode);
        Assert.Contains("endDate?: Date | null;", generatedCode);
    }

    [Fact]
    public void GeneratesTypeScriptDateForModuleDateTime()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSModule]
public partial class TimeModule
{
    [JSModuleValue]
    public static DateTime StartTime = DateTime.Now;

    [JSModuleMethod]
    public static DateTime AddDays(DateTime date, int days) => date;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should map DateTime to Date in module exports
        Assert.Contains("export const startTime: Date;", generatedCode);
        Assert.Contains("export function addDays(date: Date, days: number): Date;", generatedCode);
    }

    [Fact]
    public void GeneratesTsDocForDateTimeParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class Scheduler
{
    /// <summary>
    /// Checks if a date is available for booking.
    /// </summary>
    /// <param name=""date"">The date to check</param>
    /// <param name=""endDate"">Optional end date for range checking</param>
    /// <returns>True if available, false otherwise</returns>
    [JSMethod]
    public bool IsAvailable(DateTime date, DateTime? endDate = null) => true;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should have proper TSDoc with Date types
        Assert.Contains("* Checks if a date is available for booking.", generatedCode);
        Assert.Contains("* @param date The date to check", generatedCode);
        Assert.Contains("* @param endDate Optional end date for range checking", generatedCode);
        Assert.Contains("* @returns True if available, false otherwise", generatedCode);
        Assert.Contains("isAvailable(date: Date, endDate?: Date | null): boolean;", generatedCode);
    }

    [Fact]
    public void HandlesMixedDateTimeAndOtherTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSClass]
public partial class Booking
{
    [JSProperty]
    public string Id { get; set; }

    [JSProperty]
    public DateTime BookingDate { get; set; }

    [JSProperty]
    public int DurationMinutes { get; set; }

    [JSMethod]
    public bool Reserve(string customerId, DateTime date, int duration) => true;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Should handle mixed types correctly
        Assert.Contains("id: string;", generatedCode);
        Assert.Contains("bookingDate: Date;", generatedCode);
        Assert.Contains("durationMinutes: number;", generatedCode);
        Assert.Contains("reserve(customerId: string, date: Date, duration: number): boolean;", generatedCode);

        // Should have proper marshaling
        Assert.Contains("ctx.NewString(", generatedCode);
        Assert.Contains("ctx.NewDate(", generatedCode);
        Assert.Contains("ctx.NewNumber(", generatedCode);
    }

    #endregion

    #region JSModuleInterface Tests

    [Fact]
    public void GeneratesModuleWithInterface()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config(string Name, int Port);

[JSModule(Name = ""myModule"")]
[JSModuleInterface(InterfaceType = typeof(Config), ExportName = ""Config"")]
public partial class MyModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should include interface in module declaration
        Assert.Contains("declare module 'myModule' {", moduleCode);
        Assert.Contains("export interface Config {", moduleCode);
        Assert.Contains("name: string;", moduleCode);
        Assert.Contains("port: number;", moduleCode);
        Assert.Contains("export const version: string;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleWithMultipleInterfaces()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record UserProfile(string Name, string Email);

[JSObject]
public partial record Settings(bool DarkMode, string Language);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(UserProfile))]
[JSModuleInterface(InterfaceType = typeof(Settings))]
public partial class AppModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should include both interfaces
        Assert.Contains("export interface UserProfile {", moduleCode);
        Assert.Contains("name: string;", moduleCode);
        Assert.Contains("email: string;", moduleCode);

        Assert.Contains("export interface Settings {", moduleCode);
        Assert.Contains("darkMode: boolean;", moduleCode);
        Assert.Contains("language: string;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleWithBothClassAndInterface()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class MyClass
{
    [JSProperty]
    public string Name { get; set; }
}

[JSObject]
public partial record MyInterface(int Id, string Value);

[JSModule]
[JSModuleClass(ClassType = typeof(MyClass))]
[JSModuleInterface(InterfaceType = typeof(MyInterface))]
public partial class MyModule
{
    [JSModuleMethod]
    public static int Add(int a, int b) => a + b;
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should include both class and interface
        Assert.Contains("export class MyClass {", moduleCode);
        Assert.Contains("constructor();", moduleCode);
        Assert.Contains("name: string;", moduleCode);

        Assert.Contains("export interface MyInterface {", moduleCode);
        Assert.Contains("id: number;", moduleCode);
        Assert.Contains("value: string;", moduleCode);

        Assert.Contains("export function add(a: number, b: number): number;", moduleCode);
    }

    [Fact]
    public void AddsInterfaceToModuleExports()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config(string Name);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(Config), ExportName = ""Config"")]
public partial class MyModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should add interface name to exports list
        Assert.Contains(".AddExports(\"version\", \"Config\")", moduleCode);
    }

    [Fact]
    public void UsesCustomExportNameForInterface()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record UserProfile(string Name);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(UserProfile), ExportName = ""Profile"")]
public partial class MyModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should use custom export name
        Assert.Contains("export interface Profile {", moduleCode);
        Assert.Contains(".AddExports(\"Profile\")", moduleCode);
    }

    [Fact]
    public void ReportsErrorForInvalidModuleInterface()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

// Not a JSObject
public partial record InvalidRecord(string Name);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(InvalidRecord))]
public partial class MyModule
{
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO020");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("does not have the [JSObject] attribute", error.GetMessage());
    }

    [Fact]
    public void ReportsErrorForInterfaceInMultipleModules()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record SharedConfig(string Name);

[JSModule(Name = ""module1"")]
[JSModuleInterface(InterfaceType = typeof(SharedConfig))]
public partial class Module1
{
}

[JSModule(Name = ""module2"")]
[JSModuleInterface(InterfaceType = typeof(SharedConfig))]
public partial class Module2
{
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO021");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("referenced by multiple modules", error.GetMessage());
    }

    [Fact]
    public void ReportsErrorForDuplicateInterfaceExportName()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config1(string Name);

[JSObject]
public partial record Config2(int Value);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(Config1), ExportName = ""Config"")]
[JSModuleInterface(InterfaceType = typeof(Config2), ExportName = ""Config"")]
public partial class MyModule
{
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO011");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Export names must be unique", error.GetMessage());
    }

    [Fact]
    public void ReportsErrorForInterfaceNameConflictWithMethod()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config(string Name);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(Config), ExportName = ""getData"")]
public partial class MyModule
{
    [JSModuleMethod(Name = ""getData"")]
    public static string GetData() => ""data"";
}
";

        var result = RunGenerator(source);

        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "HAKO011");
        Assert.NotNull(error);
        Assert.Contains("Export names must be unique", error.GetMessage());
    }

    [Fact]
    public void GeneratesModuleInterfaceWithOptionalParameters()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config(
    string Name,
    int Port = 8080,
    string? Host = null
);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(Config))]
public partial class MyModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        Assert.Contains("export interface Config {", moduleCode);
        Assert.Contains("name: string;", moduleCode);
        Assert.Contains("port?: number;", moduleCode);
        Assert.Contains("host?: string | null;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleInterfaceWithDelegates()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System;

namespace TestNamespace;

[JSObject]
public partial record EventConfig(
    string EventName,
    Action<string> OnEvent,
    Func<int, bool> Validator
);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(EventConfig))]
public partial class MyModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        Assert.Contains("export interface EventConfig {", moduleCode);
        Assert.Contains("eventName: string;", moduleCode);
        Assert.Contains("onEvent: (arg0: string) => void;", moduleCode);
        Assert.Contains("validator: (arg0: number) => boolean;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleInterfaceWithCustomPropertyNames()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record ApiRequest(
    [JSPropertyName(""api_key"")] string ApiKey,
    [JSPropertyName(""user_id"")] int UserId
);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(ApiRequest))]
public partial class ApiModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        Assert.Contains("export interface ApiRequest {", moduleCode);
        Assert.Contains("api_key: string;", moduleCode);
        Assert.Contains("user_id: number;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleInterfaceWithArrayTypes()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record DataSet(
    int[] Numbers,
    string[] Tags,
    byte[] Buffer
);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(DataSet))]
public partial class DataModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        Assert.Contains("export interface DataSet {", moduleCode);
        Assert.Contains("numbers: number[];", moduleCode);
        Assert.Contains("tags: string[];", moduleCode);
        Assert.Contains("buffer: ArrayBuffer;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleInterfaceWithDocumentation()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

/// <summary>
/// Configuration settings for the application.
/// </summary>
/// <param name=""Name"">The application name</param>
/// <param name=""Port"">The server port</param>
[JSObject]
public partial record AppConfig(string Name, int Port);

/// <summary>
/// Application configuration module.
/// </summary>
[JSModule]
[JSModuleInterface(InterfaceType = typeof(AppConfig))]
public partial class ConfigModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should include interface documentation
        Assert.Contains("* Configuration settings for the application.", moduleCode);
        Assert.Contains("export interface AppConfig {", moduleCode);
        Assert.Contains("* The application name", moduleCode);
        Assert.Contains("name: string;", moduleCode);
        Assert.Contains("* The server port", moduleCode);
        Assert.Contains("port: number;", moduleCode);
    }

    [Fact]
    public void GeneratesModuleWithInterfaceOrderedBeforeValues()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSClass]
public partial class MyClass
{
    [JSProperty]
    public string Name { get; set; }
}

[JSObject]
public partial record MyInterface(string Value);

[JSModule]
[JSModuleClass(ClassType = typeof(MyClass))]
[JSModuleInterface(InterfaceType = typeof(MyInterface))]
public partial class MyModule
{
    [JSModuleValue]
    public static string Version = ""1.0.0"";
    
    [JSModuleMethod]
    public static void DoSomething() { }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Check the order: classes, then interfaces, then values, then methods
        var classPos = moduleCode.IndexOf("export class MyClass");
        var interfacePos = moduleCode.IndexOf("export interface MyInterface");
        var valuePos = moduleCode.IndexOf("export const version");
        var methodPos = moduleCode.IndexOf("export function doSomething");

        Assert.True(classPos < interfacePos, "Class should come before interface");
        Assert.True(interfacePos < valuePos, "Interface should come before values");
        Assert.True(valuePos < methodPos, "Values should come before methods");
    }

    [Fact]
    public void InterfaceDoesNotRequireRuntimeRegistration()
    {
        var source = @"
using HakoJS.SourceGeneration;

namespace TestNamespace;

[JSObject]
public partial record Config(string Name);

[JSModule]
[JSModuleInterface(InterfaceType = typeof(Config))]
public partial class MyModule
{
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should not have any CreatePrototype or CompleteClassExport calls for interfaces
        Assert.DoesNotContain("realm.CreatePrototype<TestNamespace.Config>", moduleCode);
        Assert.DoesNotContain("CompleteClassExport", moduleCode);

        // But should still be in exports list
        Assert.Contains(".AddExports(\"Config\")", moduleCode);
    }

    [Fact]
    public void GeneratesComplexModuleWithMixedExports()
    {
        var source = @"
using HakoJS.SourceGeneration;
using System.Threading.Tasks;

namespace TestNamespace;

[JSClass]
public partial class Logger
{
    [JSProperty]
    public string Name { get; set; }
    
    [JSMethod]
    public void Log(string message) { }
}

[JSObject]
public partial record LogConfig(
    string Level,
    bool Timestamps = true
);

[JSObject]
public partial record LogEntry(
    string Message,
    string Level,
    System.DateTime Timestamp
);

/// <summary>
/// Logging utilities module.
/// </summary>
[JSModule(Name = ""logging"")]
[JSModuleClass(ClassType = typeof(Logger), ExportName = ""Logger"")]
[JSModuleInterface(InterfaceType = typeof(LogConfig), ExportName = ""LogConfig"")]
[JSModuleInterface(InterfaceType = typeof(LogEntry), ExportName = ""LogEntry"")]
public partial class LoggingModule
{
    /// <summary>
    /// Default log level.
    /// </summary>
    [JSModuleValue]
    public static string DefaultLevel = ""info"";
    
    /// <summary>
    /// Configures the logging system.
    /// </summary>
    /// <param name=""config"">The configuration settings</param>
    [JSModuleMethod]
    public static void Configure(LogConfig config) { }
    
    /// <summary>
    /// Gets recent log entries.
    /// </summary>
    /// <param name=""count"">Number of entries to retrieve</param>
    /// <returns>Array of log entries</returns>
    [JSModuleMethod]
    public static async Task<LogEntry[]> GetRecent(int count)
    {
        await Task.CompletedTask;
        return null;
    }
}
";

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var moduleCode = result.GeneratedTrees.First(t => t.FilePath.Contains("Module")).GetText().ToString();

        // Should have module declaration with all exports
        Assert.Contains("declare module 'logging' {", moduleCode);

        // Class export
        Assert.Contains("export class Logger {", moduleCode);
        Assert.Contains("name: string;", moduleCode);
        Assert.Contains("log(message: string): void;", moduleCode);

        // Interface exports
        Assert.Contains("export interface LogConfig {", moduleCode);
        Assert.Contains("level: string;", moduleCode);
        Assert.Contains("timestamps?: boolean;", moduleCode);

        Assert.Contains("export interface LogEntry {", moduleCode);
        Assert.Contains("message: string;", moduleCode);
        Assert.Contains("level: string;", moduleCode);
        Assert.Contains("timestamp: Date;", moduleCode);

        // Value export
        Assert.Contains("* Default log level.", moduleCode);
        Assert.Contains("export const defaultLevel: string;", moduleCode);

        // Method exports
        Assert.Contains("* Configures the logging system.", moduleCode);
        Assert.Contains("export function configure(config: LogConfig): void;", moduleCode);

        Assert.Contains("* Gets recent log entries.", moduleCode);
        Assert.Contains("export function getRecent(count: number): Promise<LogEntry[]>;", moduleCode);

        // All exports in AddExports
        Assert.Contains(
            ".AddExports(\"defaultLevel\", \"configure\", \"getRecent\", \"Logger\", \"LogConfig\", \"LogEntry\")",
            moduleCode);
    }

    #endregion
}