using HakoJS.VM;
using Xunit.Abstractions;

namespace HakoJS.Tests;

/// <summary>
/// Tests for bytecode compilation and evaluation.
/// </summary>
public class BytecodeTests : TestBase
{
    private readonly ITestOutputHelper _testOutputHelper;
    public BytecodeTests(HakoFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void CompileToByteCode_SimpleCode_Compiles()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();
        using var result = realm.CompileToByteCode("40 + 2");

        Assert.True(result.IsSuccess);
        var bytecode = result.Unwrap();
        Assert.NotNull(bytecode);
        Assert.True(bytecode.Length > 0);
    }

    [Fact]
    public void EvalByteCode_CompiledCode_Executes()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        using var compileResult = realm.CompileToByteCode("40 + 2");
        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode);
        using var value = evalResult.Unwrap();
        Assert.Equal(42, value.AsNumber());
    }

    [Fact]
    public void CompileAndEval_Function_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code = @"
            function add(a, b) {
                return a + b;
            }
            add(10, 15);
        ";

        using var compileResult = realm.CompileToByteCode(code);
        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode);
        using var result = evalResult.Unwrap();

        Assert.Equal(25, result.AsNumber());
    }

    [Fact]
    public void CompileToByteCode_WithClosures_PreservesScope()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code = @"
            function createCounter(start) {
                let count = start;
                return function() {
                    return ++count;
                };
            }

            const counter = createCounter(5);
            [counter(), counter(), counter()];
        ";

        using var compileResult = realm.CompileToByteCode(code);
        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode);
        using var arrayResult = evalResult.Unwrap();

        Assert.True(arrayResult.IsArray());

        using var first = arrayResult.GetProperty(0);
        using var second = arrayResult.GetProperty(1);
        using var third = arrayResult.GetProperty(2);

        Assert.Equal(6, first.AsNumber());
        Assert.Equal(7, second.AsNumber());
        Assert.Equal(8, third.AsNumber());
    }

    [Fact]
    public void CompileToByteCode_EmptyCode_ReturnsEmptyBytecode()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();
        using var result = realm.CompileToByteCode("");

        Assert.True(result.IsSuccess);
        var bytecode = result.Unwrap();
        Assert.Equal(0, bytecode.Length);
    }

    [Fact]
    public void EvalByteCode_EmptyBytecode_ReturnsUndefined()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();
        var emptyBytecode = Array.Empty<byte>();

        using var result = realm.EvalByteCode(emptyBytecode);
        using var value = result.Unwrap();

        Assert.True(value.IsUndefined());
    }

    [Fact]
    public void CompileToByteCode_WithSyntaxError_ReturnsFailure()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();
        using var result = realm.CompileToByteCode("let x = ;");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EvalByteCode_WithRuntimeError_HandlesGracefully()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code = @"
            function throwError() {
                throw new Error('Runtime error from bytecode');
            }
            throwError();
        ";

        using var compileResult = realm.CompileToByteCode(code);
        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode);

        Assert.True(evalResult.IsFailure);
    }

    [Fact]
    public void CompileAndEval_WithComplexObjects_PreservesStructure()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code = @"
            const data = {
                numbers: [1, 2, 3],
                nested: {
                    value: 42
                }
            };
            data;
        ";

        using var compileResult = realm.CompileToByteCode(code);
        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode);
        using var result = evalResult.Unwrap();

        Assert.True(result.IsObject());

        using var numbers = result.GetProperty("numbers");
        Assert.True(numbers.IsArray());

        using var nested = result.GetProperty("nested");
        using var value = nested.GetProperty("value");
        Assert.Equal(42, value.AsNumber());
    }

    [Fact]
    public void BytecodeEval_MatchesDirectEval()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code1 = "const a = 10; const b = 20; a * b + 5;";
        var code2 = "const x = 10; const y = 20; x * y + 5;";

        // Direct eval
        using var directResult = realm.EvalCode(code1);
        using var directValue = directResult.Unwrap();

        // Bytecode eval
        using var compileResult = realm.CompileToByteCode(code2);
        var bytecode = compileResult.Unwrap();
        using var bytecodeResult = realm.EvalByteCode(bytecode);
        using var bytecodeValue = bytecodeResult.Unwrap();

        Assert.Equal(directValue.AsNumber(), bytecodeValue.AsNumber());
        Assert.Equal(205, bytecodeValue.AsNumber());
    }

    [Fact]
    public void EvalByteCode_WithLoadOnly_DoesNotExecute()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code = "throw new Error('Should not execute'); 42;";

        using var compileResult = realm.CompileToByteCode(code);
        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode, loadOnly: true);
        using var loadedObject = evalResult.Unwrap();

        // Should not throw since we didn't execute
        Assert.NotNull(loadedObject);
    }

    [Fact]
    public async Task CompileToByteCode_Module_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var code = @"
        export const value = 42;
        export function multiply(x) {
            return x * 2;
        }
    ";

        using var compileResult = realm.CompileToByteCode(code, new RealmEvalOptions
        {
            Type = EvalType.Module,
            FileName = "test.mjs"
        });

        var bytecode = compileResult.Unwrap();

        using var moduleNamespace = realm.EvalByteCode(bytecode).Unwrap();
        Assert.Equal(JSType.Object, moduleNamespace.Type);
        using var valueExport = moduleNamespace.GetProperty("value");
        using var multiplyExport = moduleNamespace.GetProperty("multiply");
        Assert.Equal(42, valueExport.AsNumber());
        Assert.True(multiplyExport.IsFunction());
    }

    [Fact]
    public void CompileToByteCode_DetectModule_AutoDetects()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime!.CreateRealm();

        var moduleCode = "export const greeting = 'Hello, World!';";

        using var compileResult = realm.CompileToByteCode(moduleCode, new RealmEvalOptions
        {
            DetectModule = true
        });

        var bytecode = compileResult.Unwrap();

        using var evalResult = realm.EvalByteCode(bytecode);
        using var result = evalResult.Unwrap();

        Assert.True(result.IsObject());
        using var greetingProp = result.GetProperty("greeting");
        Assert.Equal("Hello, World!", greetingProp.AsString());
    }
}
