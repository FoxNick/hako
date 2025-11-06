using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace HakoJS.SourceGenerator;

internal static class IncrementalGeneratorInitializationContextExtensions
{
    public static void ReportDiagnostics(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ImmutableArray<Diagnostic>> diagnostics)
    {
        context.RegisterSourceOutput(diagnostics, static (context, diagnostics) =>
        {
            foreach (var diagnostic in diagnostics)
                context.ReportDiagnostic(diagnostic);
        });
    }
}