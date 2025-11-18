using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HakoJS.SourceGenerator;

internal static class TypeSymbolExtensions
{
    public static uint HashString(this string s)
    {
        const uint fnvPrime = 16777619;
        const uint fnvOffsetBasis = 2166136261;

        var hash = fnvOffsetBasis;
        var data = Encoding.UTF8.GetBytes(s);

        foreach (var b in data)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }

    private static bool HasAttribute(this ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeName
                                               || a.AttributeClass?.Name == attributeName.Split('.').Last());
    }

    public static bool HasJSObjectAttributeInHierarchy(this INamedTypeSymbol typeSymbol)
    {
        var current = typeSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (HasAttribute(current, JSBindingGenerator.JSObjectAttributeName))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsAttributeInherited(INamedTypeSymbol attributeClass)
    {
        var attributeUsage = attributeClass.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute");

        if (attributeUsage == null)
            return false;

        var inheritedArg = attributeUsage.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "Inherited");

        return inheritedArg.Value.Value is true;
    }

    private static bool ShouldSearchHierarchy(ISymbol symbol, string attributeName)
    {
        // Try to find the attribute type definition
        var attributeType = symbol.ContainingAssembly.Modules.First().ReferencedAssemblySymbols
            .SelectMany(a => new[] { a })
            .Append(symbol.ContainingAssembly)
            .Select(a => a.GetTypeByMetadataName(attributeName))
            .FirstOrDefault(t => t != null);

        if (attributeType == null)
            return false;

        return IsAttributeInherited(attributeType);
    }

    public static AttributeData? GetAttributeFromHierarchy(this ISymbol symbol, string attributeName)
    {
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == attributeName);

        if (attr != null)
            return attr;
        
        if (symbol is IMethodSymbol method)
        {
            var baseMethod = method.OverriddenMethod;
            while (baseMethod != null)
            {
                attr = baseMethod.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == attributeName);
                if (attr != null)
                    return attr;
                baseMethod = baseMethod.OverriddenMethod;
            }
        }
        else if (symbol is IPropertySymbol property)
        {
            var baseProperty = property.OverriddenProperty;
            while (baseProperty != null)
            {
                attr = baseProperty.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == attributeName);
                if (attr != null)
                    return attr;
                baseProperty = baseProperty.OverriddenProperty;
            }
        }

        return null;
    }

    public static AttributeData? GetJSObjectAttributeFromHierarchy(this INamedTypeSymbol typeSymbol)
    {
        var current = typeSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            var attr = current.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "JSObjectAttribute");

            if (attr != null)
                return attr;

            current = current.BaseType;
        }

        return null;
    }

    public static bool HasJSObjectBase(this ITypeSymbol? typeSymbol)
    {
        return typeSymbol is not null && typeSymbol.BaseType != null &&
               typeSymbol.BaseType.SpecialType != SpecialType.System_Object &&
               typeSymbol.BaseType.HasAttribute(JSBindingGenerator.JSObjectAttributeName);
    }

    public static bool IsNullableValueType(this ITypeSymbol typeSymbol)
    {
        return typeSymbol is { IsValueType: true, NullableAnnotation: NullableAnnotation.Annotated }
                   and INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.ConstructUnboundGenericType() is { Name: "Nullable" } genericType &&
               genericType.ContainingNamespace.Name == "System";
    }

    public static IEnumerable<IMethodSymbol> GetMethodsInHierarchy(this INamedTypeSymbol typeSymbol)
    {
        var processedMethods = new HashSet<string>();
        var current = typeSymbol;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;

                var signature =
                    $"{method.Name}({string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString()))})";
                if (!processedMethods.Add(signature))
                    continue;

                yield return method;
            }

            current = current.BaseType;

            if (current != null && !HasAttribute(current, JSBindingGenerator.JSObjectAttributeName))
                break;
        }
    }

    public static IEnumerable<IPropertySymbol> GetPropertiesInHierarchy(this INamedTypeSymbol typeSymbol)
    {
        var processedProperties = new HashSet<string>();
        var current = typeSymbol;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!processedProperties.Add(property.Name))
                    continue;

                yield return property;
            }

            current = current.BaseType;

            if (current != null && !HasAttribute(current, JSBindingGenerator.JSObjectAttributeName))
                break;
        }
    }
    
    public static bool IsJSEnum(this ITypeSymbol typeSymbol)
    {
        return typeSymbol.TypeKind == TypeKind.Enum &&
               HasAttribute(typeSymbol, "HakoJS.SourceGeneration.JSEnumAttribute");
    }

    public static bool IsJSEnumFlags(this ITypeSymbol typeSymbol)
    {
        return typeSymbol.IsJSEnum() &&
               typeSymbol.GetAttributes()
                   .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
    }
}