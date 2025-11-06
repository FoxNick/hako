using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for TypeScript type stripping functionality.
/// </summary>
public class TypeScriptTests : TestBase
{
    public TypeScriptTests(HakoFixture fixture) : base(fixture)
    {
    }

    #region Basic Type Stripping Tests

    [Fact]
    public void StripTypes_BasicTypeAnnotation_ShouldRemoveTypes()
    {
        if (!IsAvailable) return;

        var typescript = "let x: number = 42;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("let x", javascript);
        Assert.DoesNotContain(": number", javascript);
        Assert.Contains("= 42", javascript);
    }

    [Fact]
    public void StripTypes_FunctionWithTypes_ShouldRemoveTypes()
    {
        if (!IsAvailable) return;

        var typescript = "function add(a: number, b: number): number { return a + b; }";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("function add(a", javascript);
        Assert.Contains(", b", javascript);
        Assert.DoesNotContain(": number", javascript);
        Assert.Contains("return a + b", javascript);
    }

    [Fact]
    public void StripTypes_Interface_ShouldRemoveCompletely()
    {
        if (!IsAvailable) return;

        var typescript = "interface User { name: string; age: number; }\nconst x = 1;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.DoesNotContain("interface", javascript);
        Assert.DoesNotContain("User", javascript);
        Assert.Contains("const x = 1", javascript);
    }

    [Fact]
    public void StripTypes_TypeAlias_ShouldRemoveCompletely()
    {
        if (!IsAvailable) return;

        var typescript = "type Point = { x: number; y: number; };\nconst p = { x: 1, y: 2 };";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.DoesNotContain("type Point", javascript);
        Assert.Contains("const p = { x: 1, y: 2 }", javascript);
    }

    [Fact]
    public void StripTypes_AsExpression_ShouldRemove()
    {
        if (!IsAvailable) return;

        var typescript = "const x = foo as string;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("const x = foo", javascript);
        Assert.DoesNotContain("as string", javascript);
    }

    [Fact]
    public void StripTypes_SatisfiesExpression_ShouldRemove()
    {
        if (!IsAvailable) return;

        var typescript = "const x = foo satisfies string;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("const x = foo", javascript);
        Assert.DoesNotContain("satisfies", javascript);
    }

    [Fact]
    public void StripTypes_NonNullAssertion_ShouldRemove()
    {
        if (!IsAvailable) return;

        var typescript = "const x = foo!;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("const x = foo", javascript);
        Assert.DoesNotContain("foo!", javascript);
    }

    [Fact]
    public void StripTypes_GenericFunction_ShouldRemoveGenerics()
    {
        if (!IsAvailable) return;

        var typescript = "function identity<T>(arg: T): T { return arg; }";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("function identity", javascript);
        Assert.Contains("(arg", javascript);
        Assert.DoesNotContain("<T>", javascript);
        Assert.DoesNotContain(": T", javascript);
    }

    #endregion

    #region Pure JavaScript Tests

    [Fact]
    public void StripTypes_PureJavaScript_ShouldBeUnchanged()
    {
        if (!IsAvailable) return;

        var javascript = "const x = 1; const y = 2; console.log(x + y);";
        var result = Hako.Runtime.StripTypes(javascript);

        Assert.Equal(javascript, result);
    }

    [Fact]
    public void StripTypes_ArrowFunction_ShouldBeUnchanged()
    {
        if (!IsAvailable) return;

        var javascript = "const fn = (x) => x * 2;";
        var result = Hako.Runtime.StripTypes(javascript);

        Assert.Equal(javascript, result);
    }

    [Fact]
    public void StripTypes_ClassDeclaration_ShouldBeUnchanged()
    {
        if (!IsAvailable) return;

        var javascript = @"
class MyClass {
    constructor(value) {
        this.value = value;
    }
    getValue() {
        return this.value;
    }
}";
        var result = Hako.Runtime.StripTypes(javascript);

        Assert.Equal(javascript, result);
    }

    #endregion

    #region Error Case Tests

    [Fact]
    public void StripTypes_Enum_ShouldThrowUnsupported()
    {
        if (!IsAvailable) return;

        var typescript = "enum Color { Red, Green, Blue }";

        var exception = Assert.Throws<HakoException>(() => Hako.Runtime.StripTypes(typescript));
        // Enums return unsupported status but still return output
        // The test just verifies it throws as expected
    }

    [Fact]
    public void StripTypes_ParameterProperties_ShouldThrowUnsupported()
    {
        if (!IsAvailable) return;

        var typescript = "class C { constructor(public x: number) {} }";

        var exception = Assert.Throws<HakoException>(() => Hako.Runtime.StripTypes(typescript));
    }

    [Fact]
    public void StripTypes_Namespace_ShouldThrowUnsupported()
    {
        if (!IsAvailable) return;

        var typescript = "namespace MyNamespace { export const x = 1; }";

        var exception = Assert.Throws<HakoException>(() => Hako.Runtime.StripTypes(typescript));
    }

    #endregion

    #region Integration Tests with Realm

    [Fact]
    public async Task EvalAsync_TypeScriptWithStripFlag_ShouldExecute()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var typescript = "const greet = (name: string): string => `Hello, ${name}!`; greet('World');";
        
        var result = await realm.EvalAsync<string>(typescript, new RealmEvalOptions
        {
            StripTypes = true
        });

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public async Task EvalAsync_TypeScriptFile_ShouldAutoStripByExtension()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var typescript = "function add(a: number, b: number): number { return a + b; } add(2, 3);";
        
        var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
        {
            FileName = "test.ts"  // .ts extension should auto-enable stripping
        });

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task EvalAsync_ComplexTypeScript_ShouldExecuteCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var typescript = @"
            interface Point {
                x: number;
                y: number;
            }

            type Distance = number;

            function distance(p1: Point, p2: Point): Distance {
                const dx = p2.x - p1.x;
                const dy = p2.y - p1.y;
                return Math.sqrt(dx * dx + dy * dy);
            }

            const p1: Point = { x: 0, y: 0 };
            const p2: Point = { x: 3, y: 4 };
            distance(p1, p2);
        ";
        
        var result = await realm.EvalAsync<double>(typescript, new RealmEvalOptions
        {
            StripTypes = true
        });

        Assert.Equal(5.0, result, precision: 10);
    }

    [Fact]
    public async Task EvalAsync_TypeScriptWithGenerics_ShouldExecute()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var typescript = @"
            function identity<T>(arg: T): T {
                return arg;
            }

            const result = identity<number>(42);
            result;
        ";
        
        var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
        {
            StripTypes = true
        });

        Assert.Equal(42, result);
    }

    #endregion
    #region TSX Tests

    [Fact]
    public void StripTypes_TSX_ShouldPreserveJSX()
    {
        if (!IsAvailable) return;

        var typescript = "const elm = <div>{x as string}</div>;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.Contains("<div>", javascript);
        Assert.Contains("</div>", javascript);
        Assert.Contains("{x", javascript);
        Assert.DoesNotContain("as string", javascript);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void StripTypes_EmptyString_ShouldReturnEmpty()
    {
        if (!IsAvailable) return;

        var result = Hako.Runtime.StripTypes("");
        Assert.Equal("", result);
    }

    [Fact]
    public void StripTypes_OnlyWhitespace_ShouldPreserveWhitespace()
    {
        if (!IsAvailable) return;

        var typescript = "   \n\n   ";
        var result = Hako.Runtime.StripTypes(typescript);
        
        Assert.Equal(typescript, result);
    }

    [Fact]
    public void StripTypes_DeclareAmbient_ShouldRemoveCompletely()
    {
        if (!IsAvailable) return;

        var typescript = "declare const x: number;\nconst y = 42;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        Assert.DoesNotContain("declare", javascript);
        Assert.Contains("const y = 42", javascript);
    }

    [Fact]
    public void StripTypes_PreservesLineNumbers()
    {
        if (!IsAvailable) return;

        var typescript = "let x: number = 1;\nlet y: string = 'hello';\nlet z = x + y;";
        var javascript = Hako.Runtime.StripTypes(typescript);

        // Count newlines - should be preserved
        var inputLines = typescript.Split('\n').Length;
        var outputLines = javascript.Split('\n').Length;
        
        Assert.Equal(inputLines, outputLines);
    }

    #endregion

    #region Module Tests

    [Fact]
    public async Task EvalAsync_TypeScriptModule_ShouldWork()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        var typescript = @"
            export function multiply(a: number, b: number): number {
                return a * b;
            }
            
            export const result: number = multiply(6, 7);
        ";
        
        using var module = await realm.EvalAsync(typescript, new RealmEvalOptions
        {
            Type = EvalType.Module,
            StripTypes = true,
            FileName = "test.ts"
        });

        using var resultProp = module.GetProperty("result");
        Assert.Equal(42, resultProp.AsNumber());
    }

    #endregion
    
    #region Benchmark Sample Tests

[Fact]
public async Task EvalAsync_SimpleTypeAnnotation_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        const x: number = 42;
        const y: string = 'hello';
        x + y.length
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(47, result); // 42 + 5 (length of 'hello')
}

[Fact]
public async Task EvalAsync_InterfaceDefinition_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        interface User {
            name: string;
            age: number;
            email?: string;
        }
        const user: User = { name: 'John', age: 30 };
        user.name + user.age
    ";
    
    var result = await realm.EvalAsync<string>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal("John30", result);
}

[Fact]
public async Task EvalAsync_GenericFunctionBenchmark_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        function identity<T>(arg: T): T {
            return arg;
        }
        identity<number>(123)
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(123, result);
}

[Fact]
public async Task EvalAsync_ComplexUnionTypes_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        type Status = 'active' | 'inactive' | 'pending';
        interface Config {
            timeout: number;
            retries: number;
            status: Status;
        }
        const config: Config = { timeout: 5000, retries: 3, status: 'active' };
        config.timeout + config.retries
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(5003, result);
}

[Fact]
public async Task EvalAsync_TypeAssertion_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        const data: unknown = { value: 100 };
        const result = (data as { value: number }).value;
        result * 2
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(200, result);
}

[Fact]
public async Task EvalAsync_MultipleTypeFeatures_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        // Mix of type features
        type ID = string | number;
        
        interface Product {
            id: ID;
            name: string;
            price: number;
        }
        
        function calculateTotal<T extends Product>(items: T[]): number {
            return items.reduce((sum, item) => sum + item.price, 0);
        }
        
        const products: Product[] = [
            { id: 1, name: 'Widget', price: 10.50 },
            { id: '2', name: 'Gadget', price: 25.75 },
            { id: 3, name: 'Doohickey', price: 5.25 }
        ];
        
        calculateTotal(products)
    ";
    
    var result = await realm.EvalAsync<double>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(41.5, result, precision: 2);
}

[Fact]
public async Task EvalAsync_OptionalChainingWithTypes_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        interface Address {
            street?: string;
            city?: string;
        }
        
        interface Person {
            name: string;
            address?: Address;
        }
        
        const person: Person = { name: 'Alice' };
        const city: string | undefined = person.address?.city;
        city ?? 'Unknown'
    ";
    
    var result = await realm.EvalAsync<string>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal("Unknown", result);
}

[Fact]
public async Task EvalAsync_ReadonlyAndModifiers_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        interface Point {
            readonly x: number;
            readonly y: number;
        }
        
        const point: Readonly<Point> = { x: 10, y: 20 };
        point.x + point.y
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(30, result);
}

[Fact]
public async Task EvalAsync_TupleTypes_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        type Coordinate = [number, number, number];
        
        const point: Coordinate = [1, 2, 3];
        const [x, y, z]: Coordinate = point;
        
        x + y + z
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(6, result);
}

[Fact]
public async Task EvalAsync_IntersectionTypes_ShouldExecute()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();

    var typescript = @"
        type Named = { name: string };
        type Aged = { age: number };
        type Person = Named & Aged;
        
        const person: Person = { name: 'Bob', age: 25 };
        person.name.length + person.age
    ";
    
    var result = await realm.EvalAsync<int>(typescript, new RealmEvalOptions
    {
        StripTypes = true
    });

    Assert.Equal(28, result); // 3 + 25
}

#endregion
}