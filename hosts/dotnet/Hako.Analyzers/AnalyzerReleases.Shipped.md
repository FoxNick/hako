; Shipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|----------------------------------------------------------------------------------------
HAKO100 | Usage    | Warning  | Every SetExport call in a module initializer must have a corresponding AddExport call.