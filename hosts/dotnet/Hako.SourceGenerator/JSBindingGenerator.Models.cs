using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace HakoJS.SourceGenerator;

public partial class JSBindingGenerator
{
    #region Result Structs

    private readonly struct Result(ClassModel? model, ImmutableArray<Diagnostic> diagnostics)
    {
        public readonly ClassModel? Model = model;
        public readonly ImmutableArray<Diagnostic> Diagnostics = diagnostics;
    }

    private readonly struct ModuleResult(ModuleModel? model, ImmutableArray<Diagnostic> diagnostics)
    {
        public readonly ModuleModel? Model = model;
        public readonly ImmutableArray<Diagnostic> Diagnostics = diagnostics;
    }

    private readonly struct ObjectResult(ObjectModel? model, ImmutableArray<Diagnostic> diagnostics)
    {
        public readonly ObjectModel? Model = model;
        public readonly ImmutableArray<Diagnostic> Diagnostics = diagnostics;
    }

    private readonly struct MarshalableResult(MarshalableModel? model, ImmutableArray<Diagnostic> diagnostics)
    {
        public readonly MarshalableModel? Model = model;
        public readonly ImmutableArray<Diagnostic> Diagnostics = diagnostics;
    }

    #endregion

    #region Model Classes

    private class ClassModel
    {
        public string ClassName { get; set; } = "";
        public string SourceNamespace { get; set; } = "";
        public string JsClassName { get; set; } = "";
        public ConstructorModel? Constructor { get; set; }
        public List<PropertyModel> Properties { get; set; } = new();
        public List<MethodModel> Methods { get; set; } = new();
        public string TypeScriptDefinition { get; set; } = "";
        public string? Documentation { get; set; }
    }

    private class ModuleModel
    {
        public string ClassName { get; set; } = "";
        public string SourceNamespace { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public Location Location { get; set; } = Location.None;
        public List<ModuleValueModel> Values { get; set; } = new();
        public List<ModuleMethodModel> Methods { get; set; } = new();
        public List<ModuleClassReference> ClassReferences { get; set; } = new();
        public string TypeScriptDefinition { get; set; } = "";
        public string? Documentation { get; set; }
    }

    private class ObjectModel
    {
        public string TypeName { get; set; } = "";
        public string SourceNamespace { get; set; } = "";
        public List<RecordParameterModel> Parameters { get; set; } = new();
        public string TypeScriptDefinition { get; set; } = "";
        public string? Documentation { get; set; }
    }

    private class MarshalableModel
    {
        public string TypeName { get; set; } = "";
        public string SourceNamespace { get; set; } = "";
        public List<MarshalablePropertyModel> Properties { get; set; } = new();
        public string TypeScriptDefinition { get; set; } = "";
        public string? Documentation { get; set; }
        public bool IsNested { get; set; }
        public string? ParentClassName { get; set; }
        public string TypeKind { get; set; } = "class";
    }

    private class MarshalablePropertyModel
    {
        public string Name { get; set; } = "";
        public string JsName { get; set; } = "";
        public TypeInfo TypeInfo { get; set; }
        public string? Documentation { get; set; }
    }

    private class ConstructorModel
    {
        public List<ParameterModel> Parameters { get; set; } = new();
        public string? Documentation { get; set; }
        public Dictionary<string, string> ParameterDocs { get; set; } = new();
    }

    private class PropertyModel
    {
        public string Name { get; set; } = "";
        public string JsName { get; set; } = "";
        public TypeInfo TypeInfo { get; set; }
        public bool HasSetter { get; set; }
        public bool IsStatic { get; set; }
        public string? Documentation { get; set; }
    }

    private class MethodModel
    {
        public string Name { get; set; } = "";
        public string JsName { get; set; } = "";
        public TypeInfo ReturnType { get; set; }
        public bool IsVoid { get; set; }
        public bool IsAsync { get; set; }
        public bool IsStatic { get; set; }
        public List<ParameterModel> Parameters { get; set; } = new();
        public string? Documentation { get; set; }
        public Dictionary<string, string> ParameterDocs { get; set; } = new();
        public string? ReturnDoc { get; set; }
    }

    private class ModuleValueModel
    {
        public string Name { get; set; } = "";
        public string JsName { get; set; } = "";
        public TypeInfo TypeInfo { get; set; }
        public string? Documentation { get; set; }
    }

    private class ModuleMethodModel
    {
        public string Name { get; set; } = "";
        public string JsName { get; set; } = "";
        public TypeInfo ReturnType { get; set; }
        public bool IsVoid { get; set; }
        public bool IsAsync { get; set; }
        public List<ParameterModel> Parameters { get; set; } = new();
        public string? Documentation { get; set; }
        public Dictionary<string, string> ParameterDocs { get; set; } = new();
        public string? ReturnDoc { get; set; }
    }

    private class ModuleClassReference
    {
        public string FullTypeName { get; set; } = "";
        public string SimpleName { get; set; } = "";
        public string ExportName { get; set; } = "";
        public string TypeScriptDefinition { get; set; } = "";
        public string? Documentation { get; set; }
        public ConstructorModel? Constructor { get; set; }
        public List<PropertyModel> Properties { get; set; } = new();
        public List<MethodModel> Methods { get; set; } = new();
    }

    private class ParameterModel
    {
        public string Name { get; set; } = "";
        public TypeInfo TypeInfo { get; set; }
        public bool IsOptional { get; set; }
        public string? DefaultValue { get; set; }
        public string? Documentation { get; set; }
        
        public bool IsDelegate { get; set; }
        public DelegateInfo? DelegateInfo { get; set; }
    }

    private class RecordParameterModel
    {
        public string Name { get; set; } = "";
        public string JsName { get; set; } = "";
        public TypeInfo TypeInfo { get; set; }
        public bool IsOptional { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsDelegate { get; set; }
        public DelegateInfo? DelegateInfo { get; set; }
        public string? Documentation { get; set; }
    }

    private class DelegateInfo
    {
        public bool IsAsync { get; set; }
        public TypeInfo ReturnType { get; set; }
        public bool IsVoid { get; set; }
        public List<ParameterModel> Parameters { get; set; } = new();
    }

    private struct TypeInfo(
        string fullName,
        bool isNullable,
        bool isValueType,
        bool isArray,
        string? elementType,
        SpecialType specialType,
        ITypeSymbol? underlyingType)
    {
        public readonly string FullName = fullName;
        public readonly bool IsNullable = isNullable;
        public readonly bool IsValueType = isValueType;
        public readonly bool IsArray = isArray;
        public readonly string? ElementType = elementType;
        public readonly SpecialType SpecialType = specialType;
        public readonly ITypeSymbol? UnderlyingType = underlyingType;
    }

    private class TypeDependency
    {
        public string TypeName { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public bool IsFromModule { get; set; }
    }

    #endregion
}