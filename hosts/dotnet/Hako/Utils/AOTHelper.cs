using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace HakoJS.Utils;

internal static class AotHelper
{
    public static bool IsAot { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Only used for hint")]
    static AotHelper()
    {
       
#if NET6_0_OR_GREATER
        try
        {
            // In AOT compilation, GetMethod() returns null because method metadata is trimmed
            var stackTrace = new System.Diagnostics.StackTrace(false);
            IsAot = stackTrace.GetFrame(0)?.GetMethod() is null;
        }
        catch
        {
            // If the check throws for any reason, assume non-AOT
            IsAot = false;
        }
#else
        IsAot = false;
#endif
    }
}