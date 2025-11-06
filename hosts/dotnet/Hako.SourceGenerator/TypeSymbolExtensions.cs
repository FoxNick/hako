using Microsoft.CodeAnalysis;

namespace HakoJS.SourceGenerator;

internal static class TypeSymbolExtensions
{
    public static bool IsNullableValueType(this ITypeSymbol typeSymbol)
    {
        return typeSymbol is { IsValueType: true, NullableAnnotation: NullableAnnotation.Annotated }
                   and INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.ConstructUnboundGenericType() is { Name: "Nullable" } genericType &&
               genericType.ContainingNamespace.Name == "System";
    }
}