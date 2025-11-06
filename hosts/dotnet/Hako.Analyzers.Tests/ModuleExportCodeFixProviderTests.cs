namespace HakoJS.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
        HakoJS.Analyzers.ModuleExportAnalyzer,
        HakoJS.Analyzers.ModuleExportCodeFixProvider>;



public class ModuleExportCodeFixProviderTests
{
    private const string HakoStubs = @"
namespace HakoJS.Host
{
    public class HakoRuntime
    {
        public CModule CreateCModule(string name, System.Action<CModuleInitializer> initializer) => null!;
    }

    public class CModule
    {
        public CModule AddExport(string exportName) => this;
        public CModule AddExports(params string[] exportNames) => this;
    }

    public class CModuleInitializer
    {
        public void SetExport(string name, object value) { }
        public void SetExport<T>(string name, T value) { }
        public void SetFunction(string name, object fn) { }
        public void SetClass(string name, object ctor) { }
        public void CompleteClassExport(object classObj) { }
    }
}
";

    [Fact]
    public async Task SetExportWithoutAddExport_AddsMissingAddExport()
    {
        var text = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""foo"", 42);
        });
    }
}
";

        var fixedText = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""foo"", 42);
        }).AddExport(""foo"");
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("foo");
        await Verifier.VerifyCodeFixAsync(text, expected, fixedText);
    }

    [Fact]
    public async Task SetExportWithExistingAddExport_AddsToChain()
    {
        var text = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""foo"", 42);
            init.SetExport(""bar"", ""hello"");
        })
        .AddExport(""bar"");
    }
}
";

        var fixedText = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""foo"", 42);
            init.SetExport(""bar"", ""hello"");
        })
        .AddExport(""bar"").AddExport(""foo"");
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("foo");
        await Verifier.VerifyCodeFixAsync(text, expected, fixedText);
    }

    [Fact]
    public async Task SetFunctionWithoutAddExport_AddsMissingAddExport()
    {
        var text = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetFunction(""greet"", null);
        });
    }
}
";

        var fixedText = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetFunction(""greet"", null);
        }).AddExport(""greet"");
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("greet");
        await Verifier.VerifyCodeFixAsync(text, expected, fixedText);
    }

    [Fact]
    public async Task MultipleSetExportsWithoutAddExport_FixAllAddsAllMissing()
    {
        var text = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""foo"", 42);
            init.SetExport(""bar"", ""hello"");
            init.SetExport(""baz"", true);
        });
    }
}
";

        var fixedText = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""foo"", 42);
            init.SetExport(""bar"", ""hello"");
            init.SetExport(""baz"", true);
        }).AddExport(""foo"").AddExport(""bar"").AddExport(""baz"");
    }
}
";

        var expected1 = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("foo");
        var expected2 = Verifier.Diagnostic()
            .WithLocation(33, 13)
            .WithArguments("bar");
        var expected3 = Verifier.Diagnostic()
            .WithLocation(34, 13)
            .WithArguments("baz");
        await Verifier.VerifyCodeFixAsync(text, new[] { expected1, expected2, expected3 }, fixedText);
    }

    [Fact]
    public async Task SetClassWithoutAddExport_AddsMissingAddExport()
    {
        var text = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetClass(""Calculator"", null);
        });
    }
}
";

        var fixedText = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetClass(""Calculator"", null);
        }).AddExport(""Calculator"");
    }
}
";

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("Calculator");
        await Verifier.VerifyCodeFixAsync(text, expected, fixedText);
    }
}