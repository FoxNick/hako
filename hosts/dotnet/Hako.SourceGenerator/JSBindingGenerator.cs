using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HakoJS.SourceGenerator;

[Generator]
public partial class JSBindingGenerator : IIncrementalGenerator
{
    public const string Namespace = "HakoJS.SourceGeneration";
    public const string JSClassAttributeName = $"{Namespace}.JSClassAttribute";
    public const string JSModuleAttributeName = $"{Namespace}.JSModuleAttribute";
    public const string JSObjectAttributeName = $"{Namespace}.JSObjectAttribute";

    private static readonly Dictionary<string, TypeDependency> TypeDependencies = new();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var settings = context.CompilationProvider
            .Select((c, _) =>
            {
                // Assuming this is a C# project, this should be true!
                LanguageVersion? csharpVersion = c is CSharpCompilation comp
                    ? comp.LanguageVersion
                    : null;

                return (
                    c.Options.Platform,
                    c.Options.OptimizationLevel,
                    c.AssemblyName,
                    LanguageVersion: csharpVersion);
            });

        var classProviderWithDiagnostics = context.SyntaxProvider
            .ForAttributeWithMetadataName(JSClassAttributeName, (node, _) => node is ClassDeclarationSyntax,
                GetClassModel);

        context.ReportDiagnostics(classProviderWithDiagnostics.Select((result, _) => result.Diagnostics));

        var validClassModels = classProviderWithDiagnostics
            .Where(result => result.Model != null)
            .Select((result, _) => result.Model!);

        context.RegisterSourceOutput(validClassModels, GenerateClassSource);

        var moduleProviderWithDiagnostics = context.SyntaxProvider
            .ForAttributeWithMetadataName(JSModuleAttributeName, (node, _) => node is ClassDeclarationSyntax,
                GetModuleModel);

        context.ReportDiagnostics(moduleProviderWithDiagnostics.Select((result, _) => result.Diagnostics));

        var validModuleModels = moduleProviderWithDiagnostics
            .Where(result => result.Model != null)
            .Select((result, _) => result.Model!);

        var allModules = validModuleModels.Collect();

        context.RegisterSourceOutput(allModules, (ctx, modules) =>
        {
            TypeDependencies.Clear();

            foreach (var module in modules)
            {
                foreach (var classRef in module.ClassReferences)
                    TypeDependencies[classRef.SimpleName] = new TypeDependency
                    {
                        TypeName = classRef.SimpleName,
                        ModuleName = module.ModuleName,
                        IsFromModule = true
                    };

                foreach (var interfaceRef in module.InterfaceReferences)
                    TypeDependencies[interfaceRef.SimpleName] = new TypeDependency
                    {
                        TypeName = interfaceRef.SimpleName,
                        ModuleName = module.ModuleName,
                        IsFromModule = true
                    };

                foreach (var enumRef in module.EnumReferences)
                    TypeDependencies[enumRef.SimpleName] = new TypeDependency
                    {
                        TypeName = enumRef.SimpleName,
                        ModuleName = module.ModuleName,
                        IsFromModule = true
                    };
            }

            var classToModules = new Dictionary<string, List<(string ModuleName, Location Location)>>();
            var interfaceToModules = new Dictionary<string, List<(string ModuleName, Location Location)>>();

            foreach (var module in modules)
            {
                foreach (var classRef in module.ClassReferences)
                {
                    if (!classToModules.ContainsKey(classRef.FullTypeName))
                        classToModules[classRef.FullTypeName] = new List<(string, Location)>();

                    classToModules[classRef.FullTypeName].Add((module.ClassName, module.Location));
                }

                foreach (var interfaceRef in module.InterfaceReferences)
                {
                    if (!interfaceToModules.ContainsKey(interfaceRef.FullTypeName))
                        interfaceToModules[interfaceRef.FullTypeName] = new List<(string, Location)>();

                    interfaceToModules[interfaceRef.FullTypeName].Add((module.ClassName, module.Location));
                }
            }

            foreach (var kvp in classToModules)
                if (kvp.Value.Count > 1)
                {
                    var firstModule = kvp.Value[0].ModuleName;
                    for (var i = 1; i < kvp.Value.Count; i++)
                    {
                        var diagnostic = Diagnostic.Create(
                            DuplicateModuleClassError,
                            kvp.Value[i].Location,
                            kvp.Key,
                            firstModule,
                            kvp.Value[i].ModuleName);
                        ctx.ReportDiagnostic(diagnostic);
                    }
                }

            foreach (var kvp in interfaceToModules)
                if (kvp.Value.Count > 1)
                {
                    var firstModule = kvp.Value[0].ModuleName;
                    for (var i = 1; i < kvp.Value.Count; i++)
                    {
                        var diagnostic = Diagnostic.Create(
                            DuplicateModuleInterfaceError,
                            kvp.Value[i].Location,
                            kvp.Key,
                            firstModule,
                            kvp.Value[i].ModuleName);
                        ctx.ReportDiagnostic(diagnostic);
                    }
                }

            foreach (var module in modules)
            {
                var hasClassErrors = module.ClassReferences.Any(c =>
                    classToModules.TryGetValue(c.FullTypeName, out var moduleList) && moduleList.Count > 1);

                var hasInterfaceErrors = module.InterfaceReferences.Any(i =>
                    interfaceToModules.TryGetValue(i.FullTypeName, out var moduleList) && moduleList.Count > 1);

                if (!hasClassErrors && !hasInterfaceErrors)
                    GenerateModuleSource(ctx, module);
            }
        });

        var objectProviderWithDiagnostics = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is RecordDeclarationSyntax or ClassDeclarationSyntax,
                (context, ct) =>
                {
                    var symbol = context.Node switch
                    {
                        ClassDeclarationSyntax classDecl => context.SemanticModel.GetDeclaredSymbol(classDecl, ct),
                        RecordDeclarationSyntax recordDecl => context.SemanticModel.GetDeclaredSymbol(recordDecl, ct),
                        _ => null
                    };

                    if (symbol is not INamedTypeSymbol typeSymbol || !typeSymbol.HasJSObjectAttributeInHierarchy())
                        return new ObjectResult(null, ImmutableArray<Diagnostic>.Empty);

                    return GetObjectModel(typeSymbol, ct);
                });

        context.ReportDiagnostics(objectProviderWithDiagnostics.Select((result, _) => result.Diagnostics));

        var validObjectModels = objectProviderWithDiagnostics
            .Where(result => result.Model != null)
            .Select((result, _) => result.Model!);

        context.RegisterSourceOutput(validObjectModels, GenerateObjectSource);

        var marshalableProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
                GetMarshalableModel)
            .Where(result => result.Model != null)
            .Select((result, _) => result.Model!);

        context.RegisterSourceOutput(marshalableProvider, GenerateMarshalableSource);

        var enumProviderWithDiagnostics = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "HakoJS.SourceGeneration.JSEnumAttribute",
                (node, _) => node is EnumDeclarationSyntax,
                GetEnumModel);

        context.ReportDiagnostics(enumProviderWithDiagnostics.Select((result, _) => result.Diagnostics));

        var validEnumModels = enumProviderWithDiagnostics
            .Where(result => result.Model != null)
            .Select((result, _) => result.Model!);

        var enumModelsWithSettings = validEnumModels.Combine(settings);

        context.RegisterSourceOutput(enumModelsWithSettings, static (ctx, data) =>
            GenerateEnumSource(ctx, data.Left, data.Right));

        var allObjects = validObjectModels.Collect();
        var allClasses = validClassModels.Collect();

        var combinedModels = allObjects
            .Combine(allClasses)
            .Select((data, _) => (data.Left, data.Right));

        context.RegisterSourceOutput(combinedModels, GenerateRegistry);
    }

    #region Diagnostic Descriptors

    private static readonly DiagnosticDescriptor NonPartialClassError = new(
        "HAKO001", "Class must be partial",
        "Class '{0}' has the [JSClass] attribute but is not declared as partial. Classes with [JSClass] must be declared as partial.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NonPartialModuleError = new(
        "HAKO002", "Module class must be partial",
        "Class '{0}' has the [JSModule] attribute but is not declared as partial. Classes with [JSModule] must be declared as partial.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidModuleClassError = new(
        "HAKO003", "Invalid module class reference",
        "Class '{0}' referenced in [JSModuleClass] does not have the [JSClass] attribute or does not implement IJSBindable<T>",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateModuleClassError = new(
        "HAKO004", "Class used in multiple modules",
        "Class '{0}' is referenced by multiple modules ('{1}' and '{2}'). A class can only belong to one module.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateMethodNameError = new(
        "HAKO005", "Duplicate method name",
        "Multiple methods in class '{0}' have the same JavaScript name '{1}'. Each method must have a unique JavaScript name.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MethodStaticMismatchError = new(
        "HAKO006", "Method static modifier mismatch",
        "Method '{0}' in class '{1}' has [JSMethod(Static={2})] but the method is {3}. The Static attribute must match the method's actual static modifier.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicatePropertyNameError = new(
        "HAKO007", "Duplicate property name",
        "Multiple properties in class '{0}' have the same JavaScript name '{1}'. Each property must have a unique JavaScript name.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor PropertyStaticMismatchError = new(
        "HAKO008", "Property static modifier mismatch",
        "Property '{0}' in class '{1}' has [JSProperty(Static={2})] but the property is {3}. The Static attribute must match the property's actual static modifier.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateModuleMethodNameError = new(
        "HAKO009", "Duplicate module method name",
        "Multiple methods in module '{0}' have the same JavaScript name '{1}'. Each method must have a unique JavaScript name.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateModuleValueNameError = new(
        "HAKO010", "Duplicate module value name",
        "Multiple values in module '{0}' have the same JavaScript name '{1}'. Each value must have a unique JavaScript name.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateModuleExportNameError = new(
        "HAKO011", "Duplicate module export name",
        "Module '{0}' has multiple exports with the same name '{1}'. Export names must be unique across values, methods, and classes.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnmarshalablePropertyTypeError = new(
        "HAKO012", "Property type cannot be marshaled",
        "Property '{0}' in class '{1}' has type '{2}' which cannot be marshaled to JavaScript. Only primitive types, byte[], arrays of primitives, and types implementing IJSMarshalable<T> are supported.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnmarshalableReturnTypeError = new(
        "HAKO013", "Method return type cannot be marshaled",
        "Method '{0}' in class '{1}' has return type '{2}' which cannot be marshaled to JavaScript. Only void, primitive types, byte[], arrays of primitives, Task, Task<T>, and types implementing IJSMarshalable<T> are supported.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnmarshalableModuleMethodReturnTypeError = new(
        "HAKO014", "Module method return type cannot be marshaled",
        "Method '{0}' in module '{1}' has return type '{2}' which cannot be marshaled to JavaScript. Only void, primitive types, byte[], arrays of primitives, Task, Task<T>, and types implementing IJSMarshalable<T> are supported.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnmarshalableModuleValueTypeError = new(
        "HAKO015", "Module value type cannot be marshaled",
        "Value '{0}' in module '{1}' has type '{2}' which cannot be marshaled to JavaScript. Only primitive types, byte[], arrays of primitives, and types implementing IJSMarshalable<T> are supported.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor NonPartialRecordError = new(
        "HAKO016", "Record must be partial",
        "Record '{0}' has the [JSObject] attribute but is not declared as partial. Records with [JSObject] must be declared as partial.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor JSObjectOnlyForRecordsError = new(
        "HAKO017", "[JSObject] can only be used on record types",
        "Type '{0}' has the [JSObject] attribute but is not a record. [JSObject] can only be applied to record types.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor JSObjectAndJSClassConflictError = new(
        "HAKO018", "Cannot combine [JSObject] and [JSClass]",
        "Type '{0}' has both [JSObject] and [JSClass] attributes. A type can only have one of these attributes.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnmarshalableRecordParameterTypeError = new(
        "HAKO019", "Record parameter type cannot be marshaled",
        "Parameter '{0}' in record '{1}' has type '{2}' which cannot be marshaled to JavaScript. Only primitive types, byte[], arrays of primitives, delegates, and types implementing IJSMarshalable<T> are supported.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidModuleInterfaceError = new(
        "HAKO020", "Invalid module interface reference",
        "Type '{0}' referenced in [JSModuleInterface] does not have the [JSObject] attribute or is not a record type",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);


    private static readonly DiagnosticDescriptor DuplicateModuleInterfaceError = new(
        "HAKO021", "Interface used in multiple modules",
        "Interface '{0}' is referenced by multiple modules ('{1}' and '{2}'). An interface can only belong to one module.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidModuleEnumError = new(
        "HAKO022", "Invalid module enum reference",
        "Type '{0}' referenced in [JSModuleEnum] is not an enum or does not have the [JSEnum] attribute",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor AbstractClassNotSupportedError = new(
        "HAKO023", "Abstract classes not supported with [JSClass]",
        "Class '{0}' has the [JSClass] attribute but is declared as abstract. Abstract classes are not currently supported with [JSClass]. Use [JSObject] on abstract record types instead.",
        "HakoJS.SourceGenerator", DiagnosticSeverity.Error, true);

    #endregion

    #region Model Extraction Methods

    private static Result GetClassModel(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
            return new Result(null, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = classSymbol.Locations.FirstOrDefault() ?? Location.None;

        if (!IsPartialClass(classSymbol, ct))
        {
            diagnostics.Add(Diagnostic.Create(NonPartialClassError, location, classSymbol.Name));
            return new Result(null, diagnostics.ToImmutable());
        }

        if (classSymbol.IsAbstract)
        {
            diagnostics.Add(Diagnostic.Create(AbstractClassNotSupportedError, location, classSymbol.Name));
            return new Result(null, diagnostics.ToImmutable());
        }

        var properties = FindProperties(classSymbol, diagnostics);
        var methods = FindMethods(classSymbol, diagnostics);

        if (diagnostics.Count > 0)
            return new Result(null, diagnostics.ToImmutable());

        var jsClassName = GetJsClassName(context.Attributes[0], classSymbol.Name);
        var constructor = FindConstructor(classSymbol);
        var documentation = ExtractXmlDocumentation(classSymbol);

        var typeScriptDefinition = GenerateClassTypeScriptDefinition(
            jsClassName, constructor, properties, methods, documentation, null, classSymbol);

        var model = new ClassModel
        {
            ClassName = classSymbol.Name,
            SourceNamespace = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : classSymbol.ContainingNamespace.ToDisplayString(),
            JsClassName = jsClassName,
            Constructor = constructor,
            Properties = properties,
            Methods = methods,
            TypeScriptDefinition = typeScriptDefinition,
            Documentation = documentation,
            TypeSymbol = classSymbol
        };

        return new Result(model, ImmutableArray<Diagnostic>.Empty);
    }

    private static ModuleResult GetModuleModel(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
            return new ModuleResult(null, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = classSymbol.Locations.FirstOrDefault() ?? Location.None;

        if (!IsPartialClass(classSymbol, ct))
        {
            diagnostics.Add(Diagnostic.Create(NonPartialModuleError, location, classSymbol.Name));
            return new ModuleResult(null, diagnostics.ToImmutable());
        }

        var moduleAttr = context.Attributes[0];
        var moduleName = GetModuleName(moduleAttr, classSymbol.Name);

        var values = FindModuleValues(classSymbol, moduleName, diagnostics);
        var methods = FindModuleMethods(classSymbol, moduleName, diagnostics);
        var classReferences = FindModuleClassReferences(classSymbol, diagnostics);
        var interfaceReferences = FindModuleInterfaceReferences(classSymbol, diagnostics);
        var enumReferences = FindModuleEnumReferences(classSymbol, diagnostics);

        // Auto-detect nested types and add them to the module
        foreach (var nestedType in classSymbol.GetTypeMembers())
        {
            var fullTypeName = nestedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Auto-detect nested JSClass
            if (HasAttribute(nestedType, JSClassAttributeName))
                if (classReferences.All(c => c.FullTypeName != fullTypeName))
                {
                    var jsClassName = GetJsClassNameFromSymbol(nestedType);
                    var nestedProperties = FindProperties(nestedType, ImmutableArray.CreateBuilder<Diagnostic>());
                    var nestedMethods = FindMethods(nestedType, ImmutableArray.CreateBuilder<Diagnostic>());
                    var nestedConstructor = FindConstructor(nestedType);
                    var nestedClassDoc = ExtractXmlDocumentation(nestedType);

                    var classTypeScriptDef = GenerateClassTypeScriptDefinition(
                        jsClassName, nestedConstructor, nestedProperties, nestedMethods, nestedClassDoc, moduleName,
                        nestedType);

                    classReferences.Add(new ModuleClassReference
                    {
                        FullTypeName = fullTypeName,
                        SimpleName = nestedType.Name,
                        ExportName = nestedType.Name,
                        TypeScriptDefinition = classTypeScriptDef,
                        Documentation = nestedClassDoc,
                        Constructor = nestedConstructor,
                        Properties = nestedProperties,
                        Methods = nestedMethods
                    });
                }

            // Auto-detect nested JSObject
            if (HasAttribute(nestedType, JSObjectAttributeName))
                if (interfaceReferences.All(i => i.FullTypeName != fullTypeName))
                {
                    var nestedParameters = FindRecordParameters(nestedType, ImmutableArray.CreateBuilder<Diagnostic>());
                    var nestedObjectDoc = ExtractXmlDocumentation(nestedType);

                    var objectAttribute = nestedType.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == "JSObjectAttribute");
                    var isReadOnly = true;
                    if (objectAttribute != null)
                        foreach (var arg in objectAttribute.NamedArguments)
                            if (arg is { Key: "ReadOnly", Value.Value: bool readOnlyValue })
                            {
                                isReadOnly = readOnlyValue;
                                break;
                            }

                    var interfaceTypeScriptDef = GenerateObjectTypeScriptDefinition(
                        nestedType.Name,
                        nestedParameters,
                        nestedObjectDoc,
                        isReadOnly,
                        nestedType);

                    interfaceReferences.Add(new ModuleInterfaceReference
                    {
                        FullTypeName = fullTypeName,
                        SimpleName = nestedType.Name,
                        ExportName = nestedType.Name,
                        TypeScriptDefinition = interfaceTypeScriptDef,
                        Documentation = nestedObjectDoc,
                        Parameters = nestedParameters
                    });
                }

            // Auto-detect nested JSEnum
            if (nestedType.TypeKind == TypeKind.Enum &&
                HasAttribute(nestedType, "HakoJS.SourceGeneration.JSEnumAttribute"))
                if (enumReferences.All(e => e.FullTypeName != fullTypeName))
                {
                    var jsEnumName = GetJsEnumName(nestedType);
                    var isFlags = nestedType.GetAttributes()
                        .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");

                    var enumValues = new List<EnumValueModel>();
                    foreach (var member in nestedType.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (member.IsImplicitlyDeclared || !member.HasConstantValue)
                            continue;

                        enumValues.Add(new EnumValueModel
                        {
                            Name = member.Name,
                            JsName = member.Name,
                            Value = member.ConstantValue ?? 0,
                            Documentation = ExtractXmlDocumentation(member)
                        });
                    }

                    var nestedEnumDoc = ExtractXmlDocumentation(nestedType);
                    var enumTypeScriptDef =
                        GenerateEnumTypeScriptDefinition(jsEnumName, enumValues, isFlags, nestedEnumDoc);

                    enumReferences.Add(new ModuleEnumReference
                    {
                        FullTypeName = fullTypeName,
                        SimpleName = nestedType.Name,
                        ExportName = jsEnumName,
                        TypeScriptDefinition = enumTypeScriptDef,
                        Documentation = nestedEnumDoc,
                        Values = enumValues,
                        IsFlags = isFlags
                    });
                }
        }

        ValidateModuleExports(moduleName, location, values, methods, classReferences, interfaceReferences,
            enumReferences, diagnostics);

        if (diagnostics.Count > 0)
            return new ModuleResult(null, diagnostics.ToImmutable());

        var documentation = ExtractXmlDocumentation(classSymbol);
        var typeScriptDefinition =
            GenerateModuleTypeScriptDefinition(moduleName, values, methods, classReferences, interfaceReferences,
                enumReferences,
                documentation);

        var model = new ModuleModel
        {
            ClassName = classSymbol.Name,
            SourceNamespace = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : classSymbol.ContainingNamespace.ToDisplayString(),
            ModuleName = moduleName,
            Location = location,
            Values = values,
            Methods = methods,
            ClassReferences = classReferences,
            InterfaceReferences = interfaceReferences,
            EnumReferences = enumReferences,
            TypeScriptDefinition = typeScriptDefinition,
            Documentation = documentation
        };

        return new ModuleResult(model, diagnostics.ToImmutable());
    }

    private static ObjectResult GetObjectModel(INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;

        if (HasAttribute(typeSymbol, JSClassAttributeName))
        {
            diagnostics.Add(Diagnostic.Create(JSObjectAndJSClassConflictError, location, typeSymbol.Name));
            return new ObjectResult(null, diagnostics.ToImmutable());
        }

        if (!IsRecord(typeSymbol, ct))
        {
            diagnostics.Add(Diagnostic.Create(JSObjectOnlyForRecordsError, location, typeSymbol.Name));
            return new ObjectResult(null, diagnostics.ToImmutable());
        }

        if (!IsPartialRecord(typeSymbol, ct))
        {
            diagnostics.Add(Diagnostic.Create(NonPartialRecordError, location, typeSymbol.Name));
            return new ObjectResult(null, diagnostics.ToImmutable());
        }

        var objectAttribute = typeSymbol.GetJSObjectAttributeFromHierarchy();
        if (objectAttribute is null) return new ObjectResult(null, ImmutableArray<Diagnostic>.Empty);

        var isReadOnly = true;
        foreach (var arg in objectAttribute.NamedArguments)
            if (arg is { Key: "ReadOnly", Value.Value: bool readOnlyValue })
            {
                isReadOnly = readOnlyValue;
                break;
            }

        var parameters = FindRecordParameters(typeSymbol, diagnostics);
        var constructorParameters = FindRecordParameters(typeSymbol, diagnostics, false);
        var properties = FindObjectProperties(typeSymbol, diagnostics);
        var methods = FindObjectMethods(typeSymbol, diagnostics);

        if (diagnostics.Count > 0)
            return new ObjectResult(null, diagnostics.ToImmutable());

        var documentation = ExtractXmlDocumentation(typeSymbol);
        var typeScriptDefinition =
            GenerateObjectTypeScriptDefinition(typeSymbol.Name, parameters, documentation, isReadOnly, typeSymbol,
                properties, methods);

        var model = new ObjectModel
        {
            TypeName = typeSymbol.Name,
            SourceNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            Parameters = parameters,
            ConstructorParameters = constructorParameters,
            Properties = properties,
            Methods = methods,
            TypeScriptDefinition = typeScriptDefinition,
            Documentation = documentation,
            ReadOnly = isReadOnly,
            TypeSymbol = typeSymbol
        };

        return new ObjectResult(model, diagnostics.ToImmutable());
    }

    private static List<PropertyModel> FindObjectProperties(INamedTypeSymbol typeSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var properties = new List<PropertyModel>();
        var jsNames = new Dictionary<string, string>();

        foreach (var member in typeSymbol.GetPropertiesInHierarchy())
        {
            var jsAttr = member.GetAttributeFromHierarchy("JSPropertyAttribute");
            if (jsAttr == null || HasAttribute(member, "JSIgnoreAttribute"))
                continue;

            var location = member.Locations.FirstOrDefault() ?? Location.None;

            if (!CanMarshalType(member.Type))
            {
                diagnostics.Add(Diagnostic.Create(UnmarshalablePropertyTypeError, location,
                    member.Name, typeSymbol.Name, member.Type.ToDisplayString()));
                continue;
            }

            var jsName = ToCamelCase(member.Name);
            var isStatic = member.IsStatic;
            var readOnly = false;

            foreach (var arg in jsAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                {
                    jsName = name;
                }
                else if (arg is { Key: "Static", Value.Value: bool s })
                {
                    if (s != member.IsStatic)
                    {
                        diagnostics.Add(Diagnostic.Create(PropertyStaticMismatchError, location,
                            member.Name, typeSymbol.Name, s, member.IsStatic ? "static" : "instance"));
                        continue;
                    }

                    isStatic = s;
                }
                else if (arg is { Key: "ReadOnly", Value.Value: bool ro })
                {
                    readOnly = ro;
                }

            if (jsNames.ContainsKey(jsName))
            {
                diagnostics.Add(Diagnostic.Create(DuplicatePropertyNameError, location, typeSymbol.Name, jsName));
                continue;
            }

            jsNames[jsName] = member.Name;

            properties.Add(new PropertyModel
            {
                Name = member.Name,
                JsName = jsName,
                TypeInfo = CreateTypeInfo(member.Type),
                HasSetter = member.SetMethod != null && !readOnly,
                IsStatic = isStatic,
                Documentation = ExtractXmlDocumentation(member)
            });
        }

        return properties;
    }

    private static List<MethodModel> FindObjectMethods(INamedTypeSymbol typeSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var methods = new List<MethodModel>();
        var jsNames = new Dictionary<string, string>();

        foreach (var member in typeSymbol.GetMethodsInHierarchy())
        {
            if (member.IsStatic)
                continue;

            var jsAttr = member.GetAttributeFromHierarchy("JSMethodAttribute");
            if (jsAttr == null || HasAttribute(member, "JSIgnoreAttribute"))
                continue;

            var location = member.Locations.FirstOrDefault() ?? Location.None;
            var isAsync = member.IsAsync || IsTaskType(member.ReturnType);
            var returnTypeInfo = isAsync ? GetTaskReturnType(member.ReturnType) : CreateTypeInfo(member.ReturnType);

            if (!member.ReturnsVoid && returnTypeInfo.SpecialType != SpecialType.System_Void)
            {
                var typeToCheck = isAsync ? GetTaskInnerType(member.ReturnType) : member.ReturnType;
                if (typeToCheck != null && !CanMarshalType(typeToCheck))
                {
                    diagnostics.Add(Diagnostic.Create(UnmarshalableReturnTypeError, location,
                        member.Name, typeSymbol.Name, typeToCheck.ToDisplayString()));
                    continue;
                }
            }

            var jsName = ToCamelCase(member.Name);

            foreach (var arg in jsAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                    jsName = name;

            if (jsNames.ContainsKey(jsName))
            {
                diagnostics.Add(Diagnostic.Create(DuplicateMethodNameError, location, typeSymbol.Name, jsName));
                continue;
            }

            jsNames[jsName] = member.Name;

            methods.Add(new MethodModel
            {
                Name = member.Name,
                JsName = jsName,
                ReturnType = returnTypeInfo,
                IsVoid = member.ReturnsVoid,
                IsAsync = isAsync,
                IsStatic = false,
                Parameters = member.Parameters.Select(CreateParameterModel).ToList(),
                Documentation = ExtractXmlDocumentation(member),
                ParameterDocs = ExtractParameterDocs(member),
                ReturnDoc = ExtractReturnDoc(member)
            });
        }

        return methods;
    }

    private static MarshalableResult GetMarshalableModel(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var symbol = context.Node switch
        {
            ClassDeclarationSyntax classDecl => context.SemanticModel.GetDeclaredSymbol(classDecl, ct),
            StructDeclarationSyntax structDecl => context.SemanticModel.GetDeclaredSymbol(structDecl, ct),
            RecordDeclarationSyntax recordDecl => context.SemanticModel.GetDeclaredSymbol(recordDecl, ct),
            _ => null
        };

        if (symbol is null ||
            HasAttribute(symbol, JSClassAttributeName) ||
            HasAttribute(symbol, JSObjectAttributeName) ||
            HasAttribute(symbol, JSModuleAttributeName) ||
            !ImplementsIJSMarshalable(symbol))
            return new MarshalableResult(null, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var isNested = symbol.ContainingType != null;
        var parentClassName = isNested ? symbol.ContainingType?.Name : null;

        var typeKind = symbol.IsRecord
            ? symbol.IsValueType ? "record struct" : "record"
            : symbol.IsValueType
                ? "struct"
                : "class";

        var properties = ExtractMarshalableProperties(symbol);
        var documentation = ExtractXmlDocumentation(symbol);
        var objectAttribute = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "JSObjectAttribute");
        var isReadOnly = false;
        if (objectAttribute != null)
            foreach (var arg in objectAttribute.NamedArguments)
                if (arg is { Key: "ReadOnly", Value.Value: bool readOnlyValue })
                {
                    isReadOnly = readOnlyValue;
                    break;
                }

        var typeScriptDefinition =
            GenerateMarshalableTypeScriptDefinition(symbol.Name, properties, documentation, isReadOnly, symbol);


        var model = new MarshalableModel
        {
            TypeName = symbol.Name,
            SourceNamespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(),
            Properties = properties,
            TypeScriptDefinition = typeScriptDefinition,
            Documentation = documentation,
            IsNested = isNested,
            ParentClassName = parentClassName,
            TypeKind = typeKind,
            ReadOnly = isReadOnly
        };

        return new MarshalableResult(model, diagnostics.ToImmutable());
    }

    #endregion

    #region Find Methods (Properties, Methods, Constructors, etc.)

    private static ConstructorModel? FindConstructor(INamedTypeSymbol classSymbol)
    {
        foreach (var ctor in classSymbol.Constructors)
            if (ctor.GetAttributes().Any(a => a.AttributeClass?.Name == "JSConstructorAttribute"))
                return new ConstructorModel
                {
                    Parameters = ctor.Parameters.Select(CreateParameterModel).ToList(),
                    Documentation = ExtractXmlDocumentation(ctor),
                    ParameterDocs = ExtractParameterDocs(ctor)
                };

        var defaultCtor = classSymbol.Constructors.FirstOrDefault(c => c.Parameters.Length == 0 && !c.IsStatic);
        if (defaultCtor != null)
            return new ConstructorModel
            {
                Parameters = new List<ParameterModel>(),
                Documentation = ExtractXmlDocumentation(defaultCtor),
                ParameterDocs = new Dictionary<string, string>()
            };

        return null;
    }

    private static List<PropertyModel> FindProperties(INamedTypeSymbol classSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var properties = new List<PropertyModel>();
        var jsNames = new Dictionary<string, string>();

        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var jsAttr = member.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "JSPropertyAttribute");
            if (jsAttr == null || HasAttribute(member, "JSIgnoreAttribute"))
                continue;

            var location = member.Locations.FirstOrDefault() ?? Location.None;

            if (!CanMarshalType(member.Type))
            {
                diagnostics.Add(Diagnostic.Create(UnmarshalablePropertyTypeError, location,
                    member.Name, classSymbol.Name, member.Type.ToDisplayString()));
                continue;
            }

            var jsName = ToCamelCase(member.Name);
            var isStatic = member.IsStatic;
            var readOnly = false;

            foreach (var arg in jsAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                {
                    jsName = name;
                }
                else if (arg is { Key: "Static", Value.Value: bool s })
                {
                    if (s != member.IsStatic)
                    {
                        diagnostics.Add(Diagnostic.Create(PropertyStaticMismatchError, location,
                            member.Name, classSymbol.Name, s, member.IsStatic ? "static" : "instance"));
                        continue;
                    }

                    isStatic = s;
                }
                else if (arg is { Key: "ReadOnly", Value.Value: bool ro })
                {
                    readOnly = ro;
                }

            if (jsNames.ContainsKey(jsName))
            {
                diagnostics.Add(Diagnostic.Create(DuplicatePropertyNameError, location, classSymbol.Name, jsName));
                continue;
            }

            jsNames[jsName] = member.Name;

            properties.Add(new PropertyModel
            {
                Name = member.Name,
                JsName = jsName,
                TypeInfo = CreateTypeInfo(member.Type),
                HasSetter = member.SetMethod != null && !readOnly,
                IsStatic = isStatic,
                Documentation = ExtractXmlDocumentation(member)
            });
        }

        return properties;
    }

    private static List<MethodModel> FindMethods(INamedTypeSymbol classSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var methods = new List<MethodModel>();
        var jsNames = new Dictionary<string, string>();

        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary)
                continue;

            var jsAttr = member.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "JSMethodAttribute");
            if (jsAttr == null || HasAttribute(member, "JSIgnoreAttribute"))
                continue;

            var location = member.Locations.FirstOrDefault() ?? Location.None;
            var isAsync = member.IsAsync || IsTaskType(member.ReturnType);
            var returnTypeInfo = isAsync ? GetTaskReturnType(member.ReturnType) : CreateTypeInfo(member.ReturnType);

            if (!member.ReturnsVoid && returnTypeInfo.SpecialType != SpecialType.System_Void)
            {
                var typeToCheck = isAsync ? GetTaskInnerType(member.ReturnType) : member.ReturnType;
                if (typeToCheck != null && !CanMarshalType(typeToCheck))
                {
                    diagnostics.Add(Diagnostic.Create(UnmarshalableReturnTypeError, location,
                        member.Name, classSymbol.Name, typeToCheck.ToDisplayString()));
                    continue;
                }
            }

            var jsName = ToCamelCase(member.Name);
            var isStatic = member.IsStatic;

            foreach (var arg in jsAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                {
                    jsName = name;
                }
                else if (arg is { Key: "Static", Value.Value: bool s })
                {
                    if (s != member.IsStatic)
                    {
                        diagnostics.Add(Diagnostic.Create(MethodStaticMismatchError, location,
                            member.Name, classSymbol.Name, s, member.IsStatic ? "static" : "instance"));
                        continue;
                    }

                    isStatic = s;
                }

            if (jsNames.ContainsKey(jsName))
            {
                diagnostics.Add(Diagnostic.Create(DuplicateMethodNameError, location, classSymbol.Name, jsName));
                continue;
            }

            jsNames[jsName] = member.Name;

            methods.Add(new MethodModel
            {
                Name = member.Name,
                JsName = jsName,
                ReturnType = returnTypeInfo,
                IsVoid = member.ReturnsVoid,
                IsAsync = isAsync,
                IsStatic = isStatic,
                Parameters = member.Parameters.Select(CreateParameterModel).ToList(),
                Documentation = ExtractXmlDocumentation(member),
                ParameterDocs = ExtractParameterDocs(member),
                ReturnDoc = ExtractReturnDoc(member)
            });
        }

        return methods;
    }

    private static string GetJsEnumName(INamedTypeSymbol enumSymbol)
    {
        var jsEnumAttr = enumSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "HakoJS.SourceGeneration.JSEnumAttribute");

        if (jsEnumAttr != null)
            foreach (var arg in jsEnumAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                    return name;

        return enumSymbol.Name;
    }

    private static List<ModuleEnumReference> FindModuleEnumReferences(INamedTypeSymbol classSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var references = new List<ModuleEnumReference>();
        var location = classSymbol.Locations.FirstOrDefault() ?? Location.None;

        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "JSModuleEnumAttribute")
                continue;

            INamedTypeSymbol? enumType = null;
            string? exportName = null;

            foreach (var arg in attr.NamedArguments)
                if (arg is { Key: "EnumType", Value.Value: INamedTypeSymbol type })
                    enumType = type;
                else if (arg is { Key: "ExportName", Value.Value: string name })
                    exportName = name;

            if (enumType == null)
                continue;

            if (enumType.TypeKind != TypeKind.Enum)
            {
                diagnostics.Add(Diagnostic.Create(InvalidModuleEnumError, location,
                    enumType.ToDisplayString()));
                continue;
            }

            if (!HasAttribute(enumType, "HakoJS.SourceGeneration.JSEnumAttribute"))
            {
                diagnostics.Add(Diagnostic.Create(InvalidModuleEnumError, location,
                    enumType.ToDisplayString()));
                continue;
            }

            var jsEnumName = GetJsEnumName(enumType);
            exportName ??= jsEnumName;

            var isFlags = enumType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");

            var values = new List<EnumValueModel>();
            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.IsImplicitlyDeclared || !member.HasConstantValue)
                    continue;

                values.Add(new EnumValueModel
                {
                    Name = member.Name,
                    JsName = member.Name,
                    Value = member.ConstantValue ?? 0,
                    Documentation = ExtractXmlDocumentation(member)
                });
            }

            var documentation = ExtractXmlDocumentation(enumType);
            var enumTypeScriptDef = GenerateEnumTypeScriptDefinition(jsEnumName, values, isFlags, documentation);

            references.Add(new ModuleEnumReference
            {
                FullTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SimpleName = enumType.Name,
                ExportName = exportName,
                TypeScriptDefinition = enumTypeScriptDef,
                Documentation = documentation,
                Values = values,
                IsFlags = isFlags
            });
        }

        return references;
    }

    private static List<ModuleValueModel> FindModuleValues(INamedTypeSymbol classSymbol, string moduleName,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var values = new List<ModuleValueModel>();
        var jsNames = new Dictionary<string, string>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (!member.IsStatic)
                continue;

            AttributeData? valueAttr = null;
            ITypeSymbol? memberType = null;
            string? documentation = null;
            var memberLocation = member.Locations.FirstOrDefault() ?? Location.None;

            if (member is IPropertySymbol prop)
            {
                valueAttr = prop.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "JSModuleValueAttribute");
                if (valueAttr != null)
                {
                    memberType = prop.Type;
                    documentation = ExtractXmlDocumentation(prop);
                }
            }
            else if (member is IFieldSymbol field)
            {
                valueAttr = field.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "JSModuleValueAttribute");
                if (valueAttr != null)
                {
                    memberType = field.Type;
                    documentation = ExtractXmlDocumentation(field);
                }
            }

            if (valueAttr == null || memberType == null)
                continue;

            if (!CanMarshalType(memberType))
            {
                diagnostics.Add(Diagnostic.Create(UnmarshalableModuleValueTypeError, memberLocation,
                    member.Name, moduleName, memberType.ToDisplayString()));
                continue;
            }

            var jsName = ToCamelCase(member.Name);
            foreach (var arg in valueAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                    jsName = name;

            if (jsNames.ContainsKey(jsName))
            {
                diagnostics.Add(Diagnostic.Create(DuplicateModuleValueNameError, memberLocation, classSymbol.Name,
                    jsName));
                continue;
            }

            jsNames[jsName] = member.Name;

            values.Add(new ModuleValueModel
            {
                Name = member.Name,
                JsName = jsName,
                TypeInfo = CreateTypeInfo(memberType),
                Documentation = documentation
            });
        }

        return values;
    }

    private static List<ModuleMethodModel> FindModuleMethods(INamedTypeSymbol classSymbol, string moduleName,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var methods = new List<ModuleMethodModel>();
        var jsNames = new Dictionary<string, string>();

        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary || !member.IsStatic)
                continue;

            var methodAttr = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "JSModuleMethodAttribute");
            if (methodAttr == null)
                continue;

            var location = member.Locations.FirstOrDefault() ?? Location.None;
            var isAsync = member.IsAsync || IsTaskType(member.ReturnType);
            var returnTypeInfo = isAsync ? GetTaskReturnType(member.ReturnType) : CreateTypeInfo(member.ReturnType);

            if (!member.ReturnsVoid && returnTypeInfo.SpecialType != SpecialType.System_Void)
            {
                var typeToCheck = isAsync ? GetTaskInnerType(member.ReturnType) : member.ReturnType;
                if (typeToCheck != null && !CanMarshalType(typeToCheck))
                {
                    diagnostics.Add(Diagnostic.Create(UnmarshalableModuleMethodReturnTypeError, location,
                        member.Name, moduleName, typeToCheck.ToDisplayString()));
                    continue;
                }
            }

            var jsName = ToCamelCase(member.Name);
            foreach (var arg in methodAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                    jsName = name;

            if (jsNames.ContainsKey(jsName))
            {
                diagnostics.Add(Diagnostic.Create(DuplicateModuleMethodNameError, location, classSymbol.Name, jsName));
                continue;
            }

            jsNames[jsName] = member.Name;

            methods.Add(new ModuleMethodModel
            {
                Name = member.Name,
                JsName = jsName,
                ReturnType = returnTypeInfo,
                IsVoid = member.ReturnsVoid,
                IsAsync = isAsync,
                Parameters = member.Parameters.Select(CreateParameterModel).ToList(),
                Documentation = ExtractXmlDocumentation(member),
                ParameterDocs = ExtractParameterDocs(member),
                ReturnDoc = ExtractReturnDoc(member)
            });
        }

        return methods;
    }

    private static List<ModuleClassReference> FindModuleClassReferences(INamedTypeSymbol classSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var references = new List<ModuleClassReference>();
        var location = classSymbol.Locations.FirstOrDefault() ?? Location.None;

        var moduleAttr = classSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "JSModuleAttribute");
        var moduleName = moduleAttr != null ? GetModuleName(moduleAttr, classSymbol.Name) : null;

        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "JSModuleClassAttribute")
                continue;

            INamedTypeSymbol? classType = null;
            string? exportName = null;

            foreach (var arg in attr.NamedArguments)
                if (arg is { Key: "ClassType", Value.Value: INamedTypeSymbol type })
                    classType = type;
                else if (arg is { Key: "ExportName", Value.Value: string name })
                    exportName = name;

            if (classType == null)
                continue;

            if (!HasAttribute(classType, "HakoJS.SourceGeneration.JSClassAttribute"))
            {
                diagnostics.Add(Diagnostic.Create(InvalidModuleClassError, location, classType.ToDisplayString()));
                continue;
            }

            exportName ??= classType.Name;

            var jsClassName = GetJsClassNameFromSymbol(classType);
            var properties = FindProperties(classType, ImmutableArray.CreateBuilder<Diagnostic>());
            var methods = FindMethods(classType, ImmutableArray.CreateBuilder<Diagnostic>());
            var constructor = FindConstructor(classType);
            var documentation = ExtractXmlDocumentation(classType);

            var classTypeScriptDef = GenerateClassTypeScriptDefinition(
                jsClassName, constructor, properties, methods, documentation, moduleName, classType);

            references.Add(new ModuleClassReference
            {
                FullTypeName = classType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SimpleName = classType.Name,
                ExportName = exportName,
                TypeScriptDefinition = classTypeScriptDef,
                Documentation = documentation,
                Constructor = constructor,
                Properties = properties,
                Methods = methods
            });
        }

        return references;
    }

    private static List<ModuleInterfaceReference> FindModuleInterfaceReferences(INamedTypeSymbol classSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var references = new List<ModuleInterfaceReference>();
        var location = classSymbol.Locations.FirstOrDefault() ?? Location.None;

        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "JSModuleInterfaceAttribute")
                continue;

            INamedTypeSymbol? interfaceType = null;
            string? exportName = null;

            foreach (var arg in attr.NamedArguments)
                if (arg is { Key: "InterfaceType", Value.Value: INamedTypeSymbol type })
                    interfaceType = type;
                else if (arg is { Key: "ExportName", Value.Value: string name })
                    exportName = name;

            if (interfaceType == null)
                continue;

            if (!HasAttribute(interfaceType, "HakoJS.SourceGeneration.JSObjectAttribute"))
            {
                diagnostics.Add(Diagnostic.Create(InvalidModuleInterfaceError, location,
                    interfaceType.ToDisplayString()));
                continue;
            }

            exportName ??= interfaceType.Name;

            var objectAttribute = interfaceType.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "JSObjectAttribute");
            var isReadOnly = true;
            if (objectAttribute != null)
                foreach (var arg in objectAttribute.NamedArguments)
                    if (arg is { Key: "ReadOnly", Value.Value: bool readOnlyValue })
                    {
                        isReadOnly = readOnlyValue;
                        break;
                    }

            var parameters = FindRecordParameters(interfaceType, ImmutableArray.CreateBuilder<Diagnostic>());
            var documentation = ExtractXmlDocumentation(interfaceType);

            var interfaceTypeScriptDef = GenerateObjectTypeScriptDefinition(
                interfaceType.Name,
                parameters,
                documentation,
                isReadOnly,
                interfaceType);

            references.Add(new ModuleInterfaceReference
            {
                FullTypeName = interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SimpleName = interfaceType.Name,
                ExportName = exportName,
                TypeScriptDefinition = interfaceTypeScriptDef,
                Documentation = documentation,
                Parameters = parameters,
                ReadOnly = isReadOnly
            });
        }

        return references;
    }

    private static List<RecordParameterModel> FindRecordParameters(INamedTypeSymbol typeSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics, bool includeInherited = true)
    {
        var parameters = new List<RecordParameterModel>();

        // Recursively collect parameters from base types first (only if includeInherited is true)
        if (includeInherited && typeSymbol.HasJSObjectBase())
        {
            var baseParameters = FindRecordParameters(typeSymbol.BaseType!, diagnostics);
            parameters.AddRange(baseParameters);
        }

        // Now add this type's own parameters
        var primaryCtor = typeSymbol.Constructors
            .Where(c => !c.IsStatic)
            .Where(c => c.Parameters.Length > 0)
            .Where(c =>
            {
                // Skip copy constructors (single parameter of the same type)
                if (c.Parameters.Length == 1)
                {
                    var param = c.Parameters[0];
                    if (SymbolEqualityComparer.Default.Equals(param.Type, typeSymbol))
                        return false;
                }

                return true;
            })
            .FirstOrDefault();

        if (primaryCtor == null)
            return parameters;

        var paramDocs = ExtractParameterDocs(primaryCtor);

        // Get the names of parameters we've already added from base types
        var existingParamNames = new HashSet<string>(parameters.Select(p => p.Name));

        foreach (var param in primaryCtor.Parameters)
        {
            // Skip parameters that were already added from base types
            if (existingParamNames.Contains(param.Name))
                continue;

            var location = param.Locations.FirstOrDefault() ?? Location.None;

            var jsPropertyNameAttr = param.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "JSPropertyNameAttribute");

            var jsName = ToCamelCase(param.Name);
            if (jsPropertyNameAttr != null)
                foreach (var arg in jsPropertyNameAttr.ConstructorArguments)
                    if (arg.Value is string name)
                        jsName = name;

            if (!CanMarshalType(param.Type))
            {
                diagnostics.Add(Diagnostic.Create(UnmarshalableRecordParameterTypeError, location,
                    param.Name, typeSymbol.Name, param.Type.ToDisplayString()));
                continue;
            }

            var typeInfo = CreateTypeInfo(param.Type);
            var isDelegate = IsDelegateType(param.Type);
            paramDocs.TryGetValue(param.Name, out var paramDoc);

            parameters.Add(new RecordParameterModel
            {
                Name = param.Name,
                JsName = jsName,
                TypeInfo = typeInfo,
                IsOptional = param.HasExplicitDefaultValue,
                DefaultValue = param.HasExplicitDefaultValue ? FormatDefaultValue(param) : null,
                IsDelegate = isDelegate,
                DelegateInfo = isDelegate ? AnalyzeDelegate(param.Type) : null,
                Documentation = paramDoc
            });
        }

        return parameters;
    }

    #endregion

    #region Validation and Helper Methods

    private static void ValidateModuleExports(string moduleName, Location location,
        List<ModuleValueModel> values, List<ModuleMethodModel> methods,
        List<ModuleClassReference> classReferences, List<ModuleInterfaceReference> interfaceReferences,
        List<ModuleEnumReference> enumReferences,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var exportNames = new Dictionary<string, string>();

        foreach (var value in values)
            if (exportNames.ContainsKey(value.JsName))
                diagnostics.Add(Diagnostic.Create(DuplicateModuleExportNameError, location, moduleName, value.JsName));
            else
                exportNames[value.JsName] = value.Name;

        foreach (var method in methods)
            if (exportNames.ContainsKey(method.JsName))
                diagnostics.Add(Diagnostic.Create(DuplicateModuleExportNameError, location, moduleName, method.JsName));
            else
                exportNames[method.JsName] = method.Name;

        foreach (var classRef in classReferences)
            if (exportNames.ContainsKey(classRef.ExportName))
                diagnostics.Add(Diagnostic.Create(DuplicateModuleExportNameError, location, moduleName,
                    classRef.ExportName));
            else
                exportNames[classRef.ExportName] = classRef.SimpleName;

        foreach (var interfaceRef in interfaceReferences)
            if (exportNames.ContainsKey(interfaceRef.ExportName))
                diagnostics.Add(Diagnostic.Create(DuplicateModuleExportNameError, location, moduleName,
                    interfaceRef.ExportName));
            else
                exportNames[interfaceRef.ExportName] = interfaceRef.SimpleName;

        foreach (var enumRef in enumReferences)
            if (exportNames.ContainsKey(enumRef.ExportName))
                diagnostics.Add(Diagnostic.Create(DuplicateModuleExportNameError, location, moduleName,
                    enumRef.ExportName));
            else
                exportNames[enumRef.ExportName] = enumRef.SimpleName;
    }

    private static bool IsPartialClass(INamedTypeSymbol symbol, CancellationToken ct)
    {
        return symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(ct))
            .OfType<ClassDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private static bool IsPartialRecord(INamedTypeSymbol symbol, CancellationToken ct)
    {
        return symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(ct))
            .OfType<RecordDeclarationSyntax>()
            .Any(r => r.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private static bool IsRecord(INamedTypeSymbol symbol, CancellationToken ct)
    {
        return symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(ct))
            .Any(s => s is RecordDeclarationSyntax);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeName
                                               || a.AttributeClass?.Name == attributeName.Split('.').Last());
    }

    private static string GetJsClassName(AttributeData attr, string defaultClassName)
    {
        foreach (var arg in attr.NamedArguments)
            if (arg is { Key: "Name", Value.Value: string name })
                return name;
        return defaultClassName;
    }

    private static string GetModuleName(AttributeData attr, string defaultName)
    {
        foreach (var arg in attr.NamedArguments)
            if (arg is { Key: "Name", Value.Value: string name })
                return name;
        return defaultName;
    }

    private static string GetJsClassNameFromSymbol(INamedTypeSymbol classType)
    {
        var jsClassAttr = classType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "HakoJS.SourceGeneration.JSClassAttribute");

        if (jsClassAttr != null)
            foreach (var arg in jsClassAttr.NamedArguments)
                if (arg is { Key: "Name", Value.Value: string name })
                    return name;

        return classType.Name;
    }

    private static bool IsNumericType(ITypeSymbol? type)
    {
        return type?.SpecialType is SpecialType.System_Int32 or
            SpecialType.System_Int64 or
            SpecialType.System_Int16 or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_UInt32 or
            SpecialType.System_UInt64 or
            SpecialType.System_UInt16 or
            SpecialType.System_Double or
            SpecialType.System_Single or
            SpecialType.System_Decimal;
    }

    private static bool CanMarshalType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
            return true;

        if (type.SpecialType == SpecialType.System_Object)
            return true;

        if (IsPrimitiveType(type))
            return true;

        if (type.IsNullableValueType() && type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedType)
            return CanMarshalType(namedType.TypeArguments[0]);

        if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return true;

        if (type is IArrayTypeSymbol arrayOfPrimitives && IsPrimitiveType(arrayOfPrimitives.ElementType))
            return true;

        if (type is IArrayTypeSymbol arrayType)
        {
            var elementType = arrayType.ElementType;

            // Check if element is [JSEnum]
            if (elementType.TypeKind == TypeKind.Enum && elementType is INamedTypeSymbol enumSymbol)
                if (HasAttribute(enumSymbol, "HakoJS.SourceGeneration.JSEnumAttribute"))
                    return true;

            if (HasAttribute(elementType, "HakoJS.SourceGeneration.JSObjectAttribute") ||
                HasAttribute(elementType, "HakoJS.SourceGeneration.JSClassAttribute"))
                return true;

            return ImplementsIJSMarshalable(elementType) || ImplementsIJSBindable(elementType);
        }

        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var typeDefinition = genericType.ConstructedFrom.ToDisplayString();

            // Handle dictionaries
            if (typeDefinition is "System.Collections.Generic.Dictionary<TKey, TValue>" or
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                if (genericType.TypeArguments.Length >= 2)
                {
                    var keyType = genericType.TypeArguments[0];
                    var valueType = genericType.TypeArguments[1];

                    // Key must be string or numeric
                    var isKeyValid = keyType.SpecialType == SpecialType.System_String || IsNumericType(keyType);
                    if (!isKeyValid)
                        return false;

                    // Value can be any marshalable type
                    return CanMarshalType(valueType);
                }

                return false;
            }

            // Handle generic collections
            if (typeDefinition is "System.Collections.Generic.List<T>" or
                "System.Collections.Generic.IList<T>" or
                "System.Collections.Generic.ICollection<T>" or
                "System.Collections.Generic.IEnumerable<T>" or
                "System.Collections.Generic.IReadOnlyList<T>" or
                "System.Collections.Generic.IReadOnlyCollection<T>")
            {
                if (genericType.TypeArguments.Length > 0)
                    return CanMarshalType(genericType.TypeArguments[0]);
                return false;
            }
        }

        if (IsTaskType(type))
        {
            var innerType = GetTaskInnerType(type);
            return innerType == null || CanMarshalType(innerType);
        }

        if (IsDelegateType(type))
            return true;

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumSymbol2)
            return HasAttribute(enumSymbol2, "HakoJS.SourceGeneration.JSEnumAttribute");

        if (type is INamedTypeSymbol classType)
            if (HasAttribute(classType, "HakoJS.SourceGeneration.JSClassAttribute") ||
                HasAttribute(classType, "HakoJS.SourceGeneration.JSObjectAttribute"))
                return true;

        return ImplementsIJSMarshalable(type) || ImplementsIJSBindable(type);
    }

    private static bool IsPrimitiveType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Boolean or SpecialType.System_Char or
                SpecialType.System_SByte or SpecialType.System_Byte or
                SpecialType.System_Int16 or SpecialType.System_UInt16 or
                SpecialType.System_Int32 or SpecialType.System_UInt32 or
                SpecialType.System_Int64 or SpecialType.System_UInt64 or
                SpecialType.System_Single or SpecialType.System_Double or
                SpecialType.System_DateTime or
                SpecialType.System_String => true,
            _ => false
        };
    }

    private static bool ImplementsIJSMarshalable(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(face =>
            face.Name == "IJSMarshalable" &&
            face.ContainingNamespace?.ToDisplayString() == "HakoJS.SourceGeneration");
    }

    private static bool ImplementsIJSBindable(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(face =>
            face.Name == "IJSBindable" &&
            face.ContainingNamespace?.ToDisplayString() == "HakoJS.SourceGeneration");
    }

    private static bool IsTaskType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var fullName = namedType.ConstructedFrom.ToDisplayString();
        return fullName is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>" or
            "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask<TResult>";
    }

    private static ITypeSymbol? GetTaskInnerType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedType
            ? namedType.TypeArguments[0]
            : null;
    }

    private static TypeInfo GetTaskReturnType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedType)
            return CreateTypeInfo(namedType.TypeArguments[0]);

        return new TypeInfo(
            "void",
            false,
            true,
            false,
            null,
            SpecialType.System_Void,
            null,
            false,
            false, // isFlags
            false, // isGenericDictionary
            null, // keyType
            null, // valueType
            null, // keyTypeSymbol
            null, // valueTypeSymbol
            false, // isGenericCollection
            null, // itemType
            null // itemTypeSymbol
        );
    }

    private static bool IsDelegateType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Delegate;
    }

    private static DelegateInfo? AnalyzeDelegate(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || namedType.DelegateInvokeMethod == null)
            return null;

        var invokeMethod = namedType.DelegateInvokeMethod;
        var isAsync = IsTaskType(invokeMethod.ReturnType);
        var returnType = isAsync ? GetTaskReturnType(invokeMethod.ReturnType) : CreateTypeInfo(invokeMethod.ReturnType);

        var isNamedDelegate = !namedType.Name.StartsWith("Func") && !namedType.Name.StartsWith("Action");

        var parameters = new List<ParameterModel>();
        for (var i = 0; i < invokeMethod.Parameters.Length; i++)
        {
            var param = invokeMethod.Parameters[i];
            var paramModel = CreateParameterModel(param);

            if (!isNamedDelegate)
                paramModel.Name = $"arg{i}";

            parameters.Add(paramModel);
        }

        return new DelegateInfo
        {
            IsAsync = isAsync,
            ReturnType = returnType,
            IsVoid = invokeMethod.ReturnsVoid || returnType.SpecialType == SpecialType.System_Void,
            Parameters = parameters
        };
    }

    private static ParameterModel CreateParameterModel(IParameterSymbol param)
    {
        var typeInfo = CreateTypeInfo(param.Type);
        var isDelegate = IsDelegateType(param.Type);

        return new ParameterModel
        {
            Name = param.Name,
            TypeInfo = typeInfo,
            IsOptional = param.HasExplicitDefaultValue,
            DefaultValue = param.HasExplicitDefaultValue ? FormatDefaultValue(param) : null,
            IsDelegate = isDelegate,
            DelegateInfo = isDelegate ? AnalyzeDelegate(param.Type) : null
        };
    }

    private static TypeInfo CreateTypeInfo(ITypeSymbol type)
    {
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isNullable = type.NullableAnnotation == NullableAnnotation.Annotated;
        var isValueType = type.IsValueType;
        var isArray = type.TypeKind == TypeKind.Array;

        string? elementType = null;
        if (isArray && type is IArrayTypeSymbol arrayType)
            elementType = arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var specialType = type.SpecialType;

        ITypeSymbol? underlyingType = null;
        if (type.IsNullableValueType() && type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedType)
            underlyingType = namedType.TypeArguments[0];

        // Check if it's a [JSEnum]
        var isEnum = false;
        var isFlags = false;

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumSymbol)
        {
            isEnum = enumSymbol.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == "HakoJS.SourceGeneration.JSEnumAttribute");

            if (isEnum)
                isFlags = enumSymbol.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
        }

        // Check if it's a generic dictionary
        var isGenericDictionary = false;
        string? keyType = null;
        string? valueType = null;
        ITypeSymbol? keyTypeSymbol = null;
        ITypeSymbol? valueTypeSymbol = null;

        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var typeDefinition = genericType.ConstructedFrom.ToDisplayString();
            if (typeDefinition is "System.Collections.Generic.Dictionary<TKey, TValue>" or
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
                if (genericType.TypeArguments.Length >= 2)
                {
                    isGenericDictionary = true;
                    keyTypeSymbol = genericType.TypeArguments[0];
                    valueTypeSymbol = genericType.TypeArguments[1];
                    keyType = keyTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    valueType = valueTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
        }

        // Check if it's a generic collection
        var isGenericCollection = false;
        string? itemType = null;
        ITypeSymbol? itemTypeSymbol = null;

        if (type is INamedTypeSymbol { IsGenericType: true } collectionType && !isGenericDictionary)
        {
            var typeDefinition = collectionType.ConstructedFrom.ToDisplayString();
            if (typeDefinition is "System.Collections.Generic.List<T>" or
                "System.Collections.Generic.IList<T>" or
                "System.Collections.Generic.ICollection<T>" or
                "System.Collections.Generic.IEnumerable<T>" or
                "System.Collections.Generic.IReadOnlyList<T>" or
                "System.Collections.Generic.IReadOnlyCollection<T>")
                if (collectionType.TypeArguments.Length > 0)
                {
                    isGenericCollection = true;
                    itemTypeSymbol = collectionType.TypeArguments[0];
                    itemType = itemTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
        }

        return new TypeInfo(
            fullName, isNullable, isValueType, isArray, elementType, specialType, underlyingType,
            isEnum, isFlags, isGenericDictionary, keyType, valueType, keyTypeSymbol, valueTypeSymbol,
            isGenericCollection, itemType, itemTypeSymbol);
    }

    private static string? FormatDefaultValue(IParameterSymbol param)
    {
        if (!param.HasExplicitDefaultValue)
            return null;

        var value = param.ExplicitDefaultValue;
        var type = param.Type;

        if (value == null)
            return type.IsValueType ? $"default({type.ToDisplayString()})" : "null";

        return type.SpecialType switch
        {
            SpecialType.System_String => $"\"{EscapeString(value.ToString() ?? "")}\"",
            SpecialType.System_Boolean => value.ToString()?.ToLowerInvariant() ?? "false",
            SpecialType.System_Char => $"'{value}'",
            SpecialType.System_Single => $"{value}f",
            SpecialType.System_Double => $"{value}",
            SpecialType.System_Decimal => $"{value}m",
            _ => value.ToString() ?? "0"
        };
    }

    private static string EscapeString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;
        return char.ToLower(str[0]) + str.Substring(1);
    }

    private static string ToPascalCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsUpper(str[0]))
            return str;
        return char.ToUpper(str[0]) + str.Substring(1);
    }

    #endregion
}