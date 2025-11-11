using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HakoJS.SourceGenerator;

public partial class JSBindingGenerator
{
    #region Marshalable Properties Extraction

    private static List<MarshalablePropertyModel> ExtractMarshalableProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new List<MarshalablePropertyModel>();

        foreach (var member in typeSymbol.GetMembers())
        {
            string? jsName = null;
            TypeInfo? typeInfo = null;
            string? documentation = null;

            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } prop)
            {
                jsName = ToCamelCase(prop.Name);
                typeInfo = CreateTypeInfo(prop.Type);
                documentation = ExtractXmlDocumentation(prop);
            }
            else if (member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } field)
            {
                jsName = ToCamelCase(field.Name);
                typeInfo = CreateTypeInfo(field.Type);
                documentation = ExtractXmlDocumentation(field);
            }

            if (jsName != null && typeInfo != null)
                properties.Add(new MarshalablePropertyModel
                {
                    Name = member.Name,
                    JsName = jsName,
                    TypeInfo = typeInfo.Value,
                    Documentation = documentation
                });
        }

        return properties;
    }

    #endregion

    #region TypeScript Definition Generation

    private static string GenerateClassTypeScriptDefinition(
        string jsClassName,
        ConstructorModel? constructor,
        List<PropertyModel> properties,
        List<MethodModel> methods,
        string? classDocumentation = null,
        string? belongsToModule = null,
        INamedTypeSymbol? classSymbol = null)
    {
        var sb = new StringBuilder();

        var dependencies = new HashSet<string>();

        if (constructor != null)
        {
            var ctorDeps = ExtractTypeDependencies(constructor.Parameters);
            dependencies.UnionWith(ctorDeps);
        }

        var propMethodDeps = ExtractTypeDependencies(
            new List<ParameterModel>(),
            null,
            properties,
            methods);
        dependencies.UnionWith(propMethodDeps);

        var simpleClassName = ExtractSimpleTypeName(jsClassName);
        dependencies.Remove(simpleClassName);

        var nestedMarshalables =
            new List<(string TypeName, List<MarshalablePropertyModel> Properties, string? Documentation)>();
        if (classSymbol != null)
            foreach (var nestedType in classSymbol.GetTypeMembers())
                if (ImplementsIJSMarshalable(nestedType))
                {
                    dependencies.Remove(nestedType.Name);
                    var nestedProps = ExtractMarshalableProperties(nestedType);
                    var nestedDoc = ExtractXmlDocumentation(nestedType);
                    nestedMarshalables.Add((nestedType.Name, nestedProps, nestedDoc));
                }

        var imports = GenerateImportStatements(dependencies, belongsToModule);
        if (!string.IsNullOrEmpty(imports))
            sb.Append(imports);

        if (!string.IsNullOrWhiteSpace(classDocumentation))
            sb.Append(FormatTsDoc(classDocumentation, indent: 0));

        sb.AppendLine($"declare class {jsClassName} {{");

        if (constructor != null)
        {
            var ctorDoc = FormatTsDoc(constructor.Documentation, constructor.ParameterDocs);
            if (!string.IsNullOrWhiteSpace(ctorDoc))
                sb.Append(ctorDoc);

            var ctorParams = string.Join(", ", constructor.Parameters.Select(p =>
            {
                var tsType = p is { IsDelegate: true, DelegateInfo: not null }
                    ? GenerateTypeScriptForDelegate(p.DelegateInfo)
                    : MapTypeToTypeScript(p.TypeInfo, p.IsOptional);
                var optional = p.IsOptional ? "?" : "";
                return $"{p.Name}{optional}: {tsType}";
            }));
            sb.AppendLine($"  constructor({ctorParams});");

            if (properties.Any() || methods.Any())
                sb.AppendLine();
        }

        foreach (var prop in properties)
        {
            var propDoc = FormatTsDoc(prop.Documentation);
            if (!string.IsNullOrWhiteSpace(propDoc))
                sb.Append(propDoc);

            var staticModifier = prop.IsStatic ? "static " : "";
            var readonlyModifier = !prop.HasSetter ? "readonly " : "";
            var tsType = MapTypeToTypeScript(prop.TypeInfo);
            sb.AppendLine($"  {staticModifier}{readonlyModifier}{prop.JsName}: {tsType};");
        }

        if (properties.Any() && methods.Any())
            sb.AppendLine();

        foreach (var method in methods)
        {
            var methodDoc = FormatTsDoc(method.Documentation, method.ParameterDocs, method.ReturnDoc);
            if (!string.IsNullOrWhiteSpace(methodDoc))
                sb.Append(methodDoc);

            var staticModifier = method.IsStatic ? "static " : "";
            var methodParams = string.Join(", ", method.Parameters.Select(p =>
            {
                var tsType = p is { IsDelegate: true, DelegateInfo: not null }
                    ? GenerateTypeScriptForDelegate(p.DelegateInfo)
                    : MapTypeToTypeScript(p.TypeInfo, p.IsOptional);
                var optional = p.IsOptional ? "?" : "";
                return $"{p.Name}{optional}: {tsType}";
            }));

            var returnType = method.IsVoid ? "void" : MapTypeToTypeScript(method.ReturnType);
            if (method.IsAsync)
                returnType = $"Promise<{returnType}>";

            sb.AppendLine($"  {staticModifier}{method.JsName}({methodParams}): {returnType};");
        }

        sb.AppendLine("}");

        foreach (var (typeName, nestedProps, nestedDoc) in nestedMarshalables)
        {
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(nestedDoc))
                sb.Append(FormatTsDoc(nestedDoc, indent: 0));

            sb.AppendLine($"interface {typeName} {{");

            foreach (var prop in nestedProps)
            {
                var propDoc = FormatTsDoc(prop.Documentation);
                if (!string.IsNullOrWhiteSpace(propDoc))
                    sb.Append(propDoc);

                var tsType = MapTypeToTypeScript(prop.TypeInfo);
                sb.AppendLine($"  {prop.JsName}: {tsType};");
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }
    
    private static string EscapeTsString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GenerateEnumTypeScriptDefinition(
        string enumName,
        List<EnumValueModel> values,
        bool isFlags,
        string? documentation = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(documentation))
            sb.Append(FormatTsDoc(documentation, indent: 0));

        // export const <Name>: { readonly ... };
        sb.AppendLine($"export const {enumName}: {{");

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value.Documentation))
                sb.Append(FormatTsDoc(value.Documentation, indent: 2));

            // In ambient context we only declare the *type* of the const's properties.
            // Flags -> numeric literal; otherwise -> string literal of the name.
            var typeLiteral = isFlags
                ? value.Value.ToString()
                : $"\"{EscapeTsString(value.Name)}\"";

            sb.AppendLine($"  readonly {value.JsName}: {typeLiteral};");
        }

        sb.AppendLine("};");

        // Also export a union type from the declared const shape
        sb.AppendLine($"export type {enumName} = typeof {enumName}[keyof typeof {enumName}];");

        return sb.ToString();
    }

    private static string GenerateModuleTypeScriptDefinition(
        string moduleName,
        List<ModuleValueModel> values,
        List<ModuleMethodModel> methods,
        List<ModuleClassReference> classReferences,
        List<ModuleInterfaceReference> interfaceReferences,
        List<ModuleEnumReference> enumReferences,
        string? moduleDocumentation = null)
    {
        var sb = new StringBuilder();

        var dependencies = new HashSet<string>();

        foreach (var method in methods)
        {
            var methodDeps = ExtractTypeDependencies(method.Parameters, method.ReturnType);
            dependencies.UnionWith(methodDeps);
        }

        foreach (var value in values)
        {
            var valueDeps = ExtractTypeDependencies(new List<ParameterModel>(), value.TypeInfo);
            dependencies.UnionWith(valueDeps);
        }

        foreach (var classRef in classReferences)
            dependencies.Remove(classRef.SimpleName);

        foreach (var interfaceRef in interfaceReferences)
            dependencies.Remove(interfaceRef.SimpleName);

        foreach (var enumRef in enumReferences)
            dependencies.Remove(enumRef.SimpleName);

        var imports = GenerateImportStatements(dependencies, moduleName);
        if (!string.IsNullOrEmpty(imports))
            sb.Append(imports);

        if (!string.IsNullOrWhiteSpace(moduleDocumentation))
            sb.Append(FormatTsDoc(moduleDocumentation, indent: 0));

        sb.AppendLine($"declare module '{moduleName}' {{");

        // Export enums first
        foreach (var enumRef in enumReferences)
        {
            var lines = enumRef.TypeScriptDefinition.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmedLine = line.TrimEnd();

                if (trimmedLine.StartsWith("export const ") || trimmedLine.StartsWith("export type "))
                    sb.AppendLine("  " + trimmedLine);
                else if (trimmedLine.StartsWith("/**") || trimmedLine.StartsWith(" *") ||
                         trimmedLine.StartsWith(" */"))
                    sb.AppendLine("  " + trimmedLine);
                else
                    sb.AppendLine("  " + trimmedLine);
            }

            if (enumRef != enumReferences.Last() || classReferences.Any() || interfaceReferences.Any() ||
                values.Any() || methods.Any())
                sb.AppendLine();
        }

        // Export classes
        foreach (var classRef in classReferences)
        {
            var lines = classRef.TypeScriptDefinition.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmedLine = line.TrimEnd();

                if (trimmedLine.StartsWith("import "))
                    continue;

                if (trimmedLine.StartsWith("declare class "))
                {
                    trimmedLine = "  export class " + trimmedLine.Substring("declare class ".Length);
                    sb.AppendLine(trimmedLine);
                }
                else if (trimmedLine == "}")
                {
                    sb.AppendLine("  }");
                }
                else if (trimmedLine.StartsWith("/**") || trimmedLine.StartsWith(" *") ||
                         trimmedLine.StartsWith(" */"))
                {
                    sb.AppendLine("  " + trimmedLine);
                }
                else
                {
                    sb.AppendLine("  " + trimmedLine);
                }
            }

            if (classRef != classReferences.Last() || interfaceReferences.Any() || values.Any() || methods.Any())
                sb.AppendLine();
        }

        // Export interfaces
        foreach (var interfaceRef in interfaceReferences)
        {
            var lines = interfaceRef.TypeScriptDefinition.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmedLine = line.TrimEnd();

                if (trimmedLine.StartsWith("import "))
                    continue;

                if (trimmedLine.StartsWith("interface "))
                {
                    var interfaceName = interfaceRef.ExportName;
                    trimmedLine = $"  export interface {interfaceName} {{";
                    sb.AppendLine(trimmedLine);
                }
                else if (trimmedLine == "}")
                {
                    sb.AppendLine("  }");
                }
                else if (trimmedLine.StartsWith("/**") || trimmedLine.StartsWith(" *") ||
                         trimmedLine.StartsWith(" */"))
                {
                    sb.AppendLine("  " + trimmedLine);
                }
                else
                {
                    sb.AppendLine("  " + trimmedLine);
                }
            }

            if (interfaceRef != interfaceReferences.Last() || values.Any() || methods.Any())
                sb.AppendLine();
        }

        // Export values
        foreach (var value in values)
        {
            var valueDoc = FormatTsDoc(value.Documentation);
            if (!string.IsNullOrWhiteSpace(valueDoc))
                sb.Append(valueDoc);

            var tsType = MapTypeToTypeScript(value.TypeInfo);
            sb.AppendLine($"  export const {value.JsName}: {tsType};");
        }

        if (values.Any() && methods.Any())
            sb.AppendLine();

        // Export methods
        foreach (var method in methods)
        {
            var methodDoc = FormatTsDoc(method.Documentation, method.ParameterDocs, method.ReturnDoc);
            if (!string.IsNullOrWhiteSpace(methodDoc))
                sb.Append(methodDoc);

            var methodParams = string.Join(", ", method.Parameters.Select(p =>
            {
                var tsType = p is { IsDelegate: true, DelegateInfo: not null }
                    ? GenerateTypeScriptForDelegate(p.DelegateInfo)
                    : MapTypeToTypeScript(p.TypeInfo, p.IsOptional);
                var optional = p.IsOptional ? "?" : "";
                return $"{p.Name}{optional}: {tsType}";
            }));

            var returnType = method.IsVoid ? "void" : MapTypeToTypeScript(method.ReturnType);
            if (method.IsAsync)
                returnType = $"Promise<{returnType}>";

            sb.AppendLine($"  export function {method.JsName}({methodParams}): {returnType};");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateObjectTypeScriptDefinition(
        string typeName,
        List<RecordParameterModel> parameters,
        string? typeDocumentation = null)
    {
        var sb = new StringBuilder();

        var dependencies = ExtractTypeDependencies(
            new List<ParameterModel>(),
            null,
            null,
            null,
            parameters);

        var imports = GenerateImportStatements(dependencies);
        if (!string.IsNullOrEmpty(imports))
            sb.Append(imports);

        if (!string.IsNullOrWhiteSpace(typeDocumentation))
            sb.Append(FormatTsDoc(typeDocumentation, indent: 0));

        sb.AppendLine($"interface {typeName} {{");

        foreach (var param in parameters)
        {
            var paramDoc = FormatTsDoc(param.Documentation);
            if (!string.IsNullOrWhiteSpace(paramDoc))
                sb.Append(paramDoc);

            var tsType = param is { IsDelegate: true, DelegateInfo: not null }
                ? GenerateTypeScriptForDelegate(param.DelegateInfo)
                : MapTypeToTypeScript(param.TypeInfo, param.IsOptional);

            var optional = param.IsOptional ? "?" : "";
            sb.AppendLine($"  {param.JsName}{optional}: {tsType};");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateMarshalableTypeScriptDefinition(
        string typeName,
        List<MarshalablePropertyModel> properties,
        string? documentation = null)
    {
        var sb = new StringBuilder();

        var dependencies = new HashSet<string>();
        foreach (var prop in properties)
            AddTypeDependency(dependencies, prop.TypeInfo);

        var imports = GenerateImportStatements(dependencies);
        if (!string.IsNullOrEmpty(imports))
            sb.Append(imports);

        if (!string.IsNullOrWhiteSpace(documentation))
            sb.Append(FormatTsDoc(documentation, indent: 0));

        sb.AppendLine($"interface {typeName} {{");

        foreach (var prop in properties)
        {
            var propDoc = FormatTsDoc(prop.Documentation);
            if (!string.IsNullOrWhiteSpace(propDoc))
                sb.Append(propDoc);

            var tsType = MapTypeToTypeScript(prop.TypeInfo);
            sb.AppendLine($"  {prop.JsName}: {tsType};");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateTypeScriptForDelegate(DelegateInfo delegateInfo)
    {
        var parameters = string.Join(", ", delegateInfo.Parameters.Select(p =>
        {
            var tsType = MapTypeToTypeScript(p.TypeInfo, p.IsOptional);
            var optional = p.IsOptional ? "?" : "";
            return $"{p.Name}{optional}: {tsType}";
        }));

        var returnType = delegateInfo.IsVoid ? "void" : MapTypeToTypeScript(delegateInfo.ReturnType);

        if (delegateInfo.IsAsync)
            returnType = $"Promise<{returnType}>";

        return $"({parameters}) => {returnType}";
    }

    #endregion

    #region TypeScript Type Mapping

    private static string MapTypeToTypeScript(TypeInfo type, bool isOptional = false)
    {
        if (type.SpecialType == SpecialType.System_Void)
            return "void";

        if (type is { IsArray: true, ElementType: not null })
        {
            var elementTypeName = type.ElementType.Replace("global::", "");
            if (elementTypeName is "System.Byte" or "byte")
                return "ArrayBuffer";
        }

        if (type.IsEnum)
        {
            var enumName = ExtractSimpleTypeName(type.FullName);

            if (type.UnderlyingType != null || (type.IsNullable && !type.IsValueType))
                return $"{enumName} | null";

            return enumName;
        }

        if (type.UnderlyingType != null)
        {
            var underlyingTs = MapPrimitiveTypeToTypeScript(CreateTypeInfo(type.UnderlyingType));
            return $"{underlyingTs} | null";
        }

        var baseType = MapPrimitiveTypeToTypeScript(type);

        if (type is { IsNullable: true, IsValueType: false })
            return $"{baseType} | null";

        return baseType;
    }

    private static string MapPrimitiveTypeToTypeScript(TypeInfo type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return "string";
            case SpecialType.System_Boolean:
                return "boolean";
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Int16:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_UInt16:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
                return "number";
            case SpecialType.System_DateTime:
                return "Date";
        }

        if (type.FullName == "global::HakoJS.SourceGeneration.Uint8ArrayValue")
            return "Uint8Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Int8ArrayValue")
            return "Int8Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Uint8ClampedArrayValue")
            return "Uint8ClampedArray";
        if (type.FullName == "global::HakoJS.SourceGeneration.Int16ArrayValue")
            return "Int16Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Uint16ArrayValue")
            return "Uint16Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Int32ArrayValue")
            return "Int32Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Uint32ArrayValue")
            return "Uint32Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Float32ArrayValue")
            return "Float32Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.Float64ArrayValue")
            return "Float64Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.BigInt64ArrayValue")
            return "BigInt64Array";
        if (type.FullName == "global::HakoJS.SourceGeneration.BigUint64ArrayValue")
            return "BigUint64Array";

        if (type is { IsArray: true, ElementType: not null })
        {
            var elementTypeName = type.ElementType.Replace("global::", "");
            var tsElementType = elementTypeName switch
            {
                "System.String" or "string" => "string",
                "System.Boolean" or "bool" => "boolean",
                "System.Int32" or "int" => "number",
                "System.Int64" or "long" => "number",
                "System.Int16" or "short" => "number",
                "System.Byte" or "byte" => "number",
                "System.SByte" or "sbyte" => "number",
                "System.UInt32" or "uint" => "number",
                "System.UInt64" or "ulong" => "number",
                "System.UInt16" or "ushort" => "number",
                "System.Double" or "double" => "number",
                "System.Single" or "float" => "number",
                "System.DateTime" or "DateTime" => "Date",
                _ => elementTypeName.Contains('.')
                    ? elementTypeName.Substring(elementTypeName.LastIndexOf('.') + 1)
                    : elementTypeName
            };

            return $"{tsElementType}[]";
        }

        var fullName = type.FullName.Replace("global::", "");
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    #endregion

    #region Type Dependencies

    private static HashSet<string> ExtractTypeDependencies(
        List<ParameterModel> parameters,
        TypeInfo? returnType = null,
        List<PropertyModel>? properties = null,
        List<MethodModel>? methods = null,
        List<RecordParameterModel>? recordParameters = null)
    {
        var dependencies = new HashSet<string>();

        foreach (var param in parameters)
            // Skip delegates - they're inlined in TypeScript
            if (param.IsDelegate)
            {
                // But do add dependencies from delegate parameters and return type
                if (param.DelegateInfo != null)
                {
                    foreach (var delegateParam in param.DelegateInfo.Parameters)
                        AddTypeDependency(dependencies, delegateParam.TypeInfo);

                    if (!param.DelegateInfo.IsVoid)
                        AddTypeDependency(dependencies, param.DelegateInfo.ReturnType);
                }
            }
            else
            {
                AddTypeDependency(dependencies, param.TypeInfo);
            }

        if (returnType != null && returnType.Value.SpecialType != SpecialType.System_Void)
            AddTypeDependency(dependencies, returnType.Value);

        if (properties != null)
            foreach (var prop in properties)
                AddTypeDependency(dependencies, prop.TypeInfo);

        if (methods != null)
            foreach (var method in methods)
            {
                foreach (var param in method.Parameters)
                    // Skip delegates - they're inlined in TypeScript
                    if (param.IsDelegate)
                    {
                        // But do add dependencies from delegate parameters and return type
                        if (param.DelegateInfo != null)
                        {
                            foreach (var delegateParam in param.DelegateInfo.Parameters)
                                AddTypeDependency(dependencies, delegateParam.TypeInfo);

                            if (!param.DelegateInfo.IsVoid)
                                AddTypeDependency(dependencies, param.DelegateInfo.ReturnType);
                        }
                    }
                    else
                    {
                        AddTypeDependency(dependencies, param.TypeInfo);
                    }

                if (method.ReturnType.SpecialType != SpecialType.System_Void)
                    AddTypeDependency(dependencies, method.ReturnType);
            }

        if (recordParameters != null)
            foreach (var param in recordParameters)
                // Skip delegates - they're inlined in TypeScript
                if (param.IsDelegate)
                {
                    // But do add dependencies from delegate parameters and return type
                    if (param.DelegateInfo != null)
                    {
                        foreach (var delegateParam in param.DelegateInfo.Parameters)
                            AddTypeDependency(dependencies, delegateParam.TypeInfo);

                        if (!param.DelegateInfo.IsVoid)
                            AddTypeDependency(dependencies, param.DelegateInfo.ReturnType);
                    }
                }
                else
                {
                    AddTypeDependency(dependencies, param.TypeInfo);
                }

        return dependencies;
    }

    private static void AddTypeDependency(HashSet<string> dependencies, TypeInfo typeInfo)
    {
        if (typeInfo.SpecialType != SpecialType.None)
            return;

        if (typeInfo.FullName == "global::System.Byte[]")
            return;

        if (IsSpecialMarshalingType(typeInfo.FullName))
            return;

        if (typeInfo.IsEnum)
        {
            var simpleName = ExtractSimpleTypeName(typeInfo.FullName);
            dependencies.Add(simpleName);
            return;
        }

        if (typeInfo is { IsArray: true, ElementType: not null })
        {
            var elementTypeName = typeInfo.ElementType.Replace("global::", "");
            if (!IsPrimitiveTypeName(elementTypeName))
            {
                var simpleName = ExtractSimpleTypeName(elementTypeName);
                dependencies.Add(simpleName);
            }

            return;
        }

        if (typeInfo.UnderlyingType != null)
        {
            AddTypeDependency(dependencies, CreateTypeInfo(typeInfo.UnderlyingType));
            return;
        }

        var fullName = typeInfo.FullName.Replace("global::", "");
        if (!IsPrimitiveTypeName(fullName))
        {
            var simpleName = ExtractSimpleTypeName(fullName);
            dependencies.Add(simpleName);
        }
    }

    private static bool IsSpecialMarshalingType(string fullName)
    {
        return fullName switch
        {
            "global::HakoJS.SourceGeneration.Uint8ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Int8ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Uint8ClampedArrayValue" => true,
            "global::HakoJS.SourceGeneration.Int16ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Uint16ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Int32ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Uint32ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Float32ArrayValue" => true,
            "global::HakoJS.SourceGeneration.Float64ArrayValue" => true,
            "global::HakoJS.SourceGeneration.BigInt64ArrayValue" => true,
            "global::HakoJS.SourceGeneration.BigUint64ArrayValue" => true,
            _ => false
        };
    }

    private static bool IsPrimitiveTypeName(string typeName)
    {
        return typeName switch
        {
            "System.String" or "string" => true,
            "System.Boolean" or "bool" => true,
            "System.Int32" or "int" => true,
            "System.Int64" or "long" => true,
            "System.Int16" or "short" => true,
            "System.Byte" or "byte" => true,
            "System.SByte" or "sbyte" => true,
            "System.UInt32" or "uint" => true,
            "System.UInt64" or "ulong" => true,
            "System.UInt16" or "ushort" => true,
            "System.Double" or "double" => true,
            "System.Single" or "float" => true,
            "System.Void" or "void" => true,
            "System.DateTime" or "DateTime" => true,
            _ => false
        };
    }

    private static string GenerateImportStatements(HashSet<string> dependencies, string? currentModuleName = null)
    {
        if (dependencies.Count == 0)
            return "";

        var sb = new StringBuilder();
        var importsByModule = new Dictionary<string, List<string>>();

        foreach (var typeName in dependencies.OrderBy(t => t))
            if (TypeDependencies.TryGetValue(typeName, out var dependency))
            {
                if (dependency.ModuleName == currentModuleName)
                    continue;

                var modulePath = dependency.IsFromModule ? dependency.ModuleName : $"./{typeName.ToLowerInvariant()}";

                if (!importsByModule.ContainsKey(modulePath))
                    importsByModule[modulePath] = new List<string>();

                importsByModule[modulePath].Add(typeName);
            }
            else
            {
                var modulePath = $"./{typeName.ToLowerInvariant()}";

                if (!importsByModule.ContainsKey(modulePath))
                    importsByModule[modulePath] = new List<string>();

                importsByModule[modulePath].Add(typeName);
            }

        foreach (var kvp in importsByModule.OrderBy(k => k.Key))
        {
            var types = string.Join(", ", kvp.Value.OrderBy(t => t));
            sb.AppendLine($"import {{ {types} }} from \"{kvp.Key}\";");
        }

        if (sb.Length > 0)
            sb.AppendLine();

        return sb.ToString();
    }

    #endregion
}