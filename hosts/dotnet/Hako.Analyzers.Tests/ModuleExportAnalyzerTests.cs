namespace HakoJS.Analyzers.Tests;

using System.Threading.Tasks;
using Xunit;
using Verifier =
    Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
        HakoJS.Analyzers.ModuleExportAnalyzer>;


public class ModuleExportAnalyzerTests
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
    public async Task SetExportWithoutAddExport_AlertDiagnostic()
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

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("foo");
        await Verifier.VerifyAnalyzerAsync(text, expected);
    }

    [Fact]
    public async Task SetExportWithAddExport_NoDiagnostic()
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
        })
        .AddExport(""foo"");
    }
}
";

        await Verifier.VerifyAnalyzerAsync(text);
    }

    [Fact]
    public async Task MultipleSetExportsWithPartialAddExports_AlertDiagnostics()
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
        })
        .AddExport(""bar"");
    }
}
";

        var expected1 = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("foo");
        var expected2 = Verifier.Diagnostic()
            .WithLocation(34, 13)
            .WithArguments("baz");
        await Verifier.VerifyAnalyzerAsync(text, expected1, expected2);
    }

    [Fact]
    public async Task SetExportWithAddExports_NoDiagnostic()
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
        })
        .AddExports(""foo"", ""bar"", ""baz"");
    }
}
";

        await Verifier.VerifyAnalyzerAsync(text);
    }

    [Fact]
    public async Task SetFunctionWithoutAddExport_AlertDiagnostic()
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

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("greet");
        await Verifier.VerifyAnalyzerAsync(text, expected);
    }

    [Fact]
    public async Task SetClassWithoutAddExport_AlertDiagnostic()
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

        var expected = Verifier.Diagnostic()
            .WithLocation(32, 13)
            .WithArguments("Calculator");
        await Verifier.VerifyAnalyzerAsync(text, expected);
    }

    [Fact]
    public async Task SetExportInVariableWithLaterAddExport_NoDiagnostic()
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
        module.AddExport(""foo"");
    }
}
";

        await Verifier.VerifyAnalyzerAsync(text);
    }

    [Fact]
    public async Task ChainedAddExports_NoDiagnostic()
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
        .AddExport(""foo"")
        .AddExport(""bar"");
    }
}
";

        await Verifier.VerifyAnalyzerAsync(text);
    }

    [Fact]
    public async Task MixedSetExportMethods_PartialAddExports_AlertDiagnostics()
    {
        var text = HakoStubs + @"
public class Program
{
    public void Main()
    {
        var runtime = new HakoJS.Host.HakoRuntime();
        var module = runtime.CreateCModule(""myModule"", init =>
        {
            init.SetExport(""version"", ""1.0"");
            init.SetFunction(""greet"", null);
            init.SetClass(""Calculator"", null);
        })
        .AddExport(""version"");
    }
}
";

        var expected1 = Verifier.Diagnostic()
            .WithLocation(33, 13)
            .WithArguments("greet");
        var expected2 = Verifier.Diagnostic()
            .WithLocation(34, 13)
            .WithArguments("Calculator");
        await Verifier.VerifyAnalyzerAsync(text, expected1, expected2);
    }
}