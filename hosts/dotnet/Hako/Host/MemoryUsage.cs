using System.Text.Json.Serialization;

namespace HakoJS.Host;

/// <summary>
/// Contains detailed memory usage statistics for the JavaScript runtime.
/// </summary>
/// <param name="MallocLimit">The maximum memory allocation limit in bytes, or -1 if no limit is set.</param>
/// <param name="MallocSize">The total amount of memory allocated in bytes.</param>
/// <param name="MallocCount">The total number of memory allocation calls made.</param>
/// <param name="MemoryUsedSize">The total amount of memory currently used in bytes.</param>
/// <param name="MemoryUsedCount">The number of memory blocks currently in use.</param>
/// <param name="AtomCount">The number of atoms (interned strings) in memory.</param>
/// <param name="AtomSize">The total size in bytes of all atoms.</param>
/// <param name="StrCount">The number of JavaScript string objects in memory.</param>
/// <param name="StrSize">The total size in bytes of all JavaScript strings.</param>
/// <param name="ObjCount">The number of JavaScript objects in memory.</param>
/// <param name="ObjSize">The total size in bytes of all JavaScript objects.</param>
/// <param name="PropCount">The number of object properties in memory.</param>
/// <param name="PropSize">The total size in bytes of all object properties.</param>
/// <param name="ShapeCount">The number of shapes (hidden classes/object layouts) in memory.</param>
/// <param name="ShapeSize">The total size in bytes of all shapes.</param>
/// <param name="JsFuncCount">The number of JavaScript functions in memory.</param>
/// <param name="JsFuncSize">The total size in bytes of all JavaScript functions.</param>
/// <param name="JsFuncCodeSize">The total size in bytes of JavaScript function bytecode.</param>
/// <param name="JsFuncPc2LineCount">The number of program counter to line number mappings.</param>
/// <param name="JsFuncPc2LineSize">The total size in bytes of program counter to line number mappings.</param>
/// <param name="CFuncCount">The number of C functions (native functions) registered.</param>
/// <param name="ArrayCount">The total number of arrays in memory.</param>
/// <param name="FastArrayCount">The number of arrays using the optimized fast array representation.</param>
/// <param name="FastArrayElements">The total number of elements in fast arrays.</param>
/// <param name="BinaryObjectCount">The number of binary objects (ArrayBuffers, TypedArrays, etc.) in memory.</param>
/// <param name="BinaryObjectSize">The total size in bytes of all binary objects.</param>
public record MemoryUsage(
    [property: JsonPropertyName("malloc_limit")] long MallocLimit,
    [property: JsonPropertyName("malloc_size")] long MallocSize,
    [property: JsonPropertyName("malloc_count")] long MallocCount,
    [property: JsonPropertyName("memory_used_size")] long MemoryUsedSize,
    [property: JsonPropertyName("memory_used_count")] long MemoryUsedCount,
    [property: JsonPropertyName("atom_count")] long AtomCount,
    [property: JsonPropertyName("atom_size")] long AtomSize,
    [property: JsonPropertyName("str_count")] long StrCount,
    [property: JsonPropertyName("str_size")] long StrSize,
    [property: JsonPropertyName("obj_count")] long ObjCount,
    [property: JsonPropertyName("obj_size")] long ObjSize,
    [property: JsonPropertyName("prop_count")] long PropCount,
    [property: JsonPropertyName("prop_size")] long PropSize,
    [property: JsonPropertyName("shape_count")] long ShapeCount,
    [property: JsonPropertyName("shape_size")] long ShapeSize,
    [property: JsonPropertyName("js_func_count")] long JsFuncCount,
    [property: JsonPropertyName("js_func_size")] long JsFuncSize,
    [property: JsonPropertyName("js_func_code_size")] long JsFuncCodeSize,
    [property: JsonPropertyName("js_func_pc2line_count")] long JsFuncPc2LineCount,
    [property: JsonPropertyName("js_func_pc2line_size")] long JsFuncPc2LineSize,
    [property: JsonPropertyName("c_func_count")] long CFuncCount,
    [property: JsonPropertyName("array_count")] long ArrayCount,
    [property: JsonPropertyName("fast_array_count")] long FastArrayCount,
    [property: JsonPropertyName("fast_array_elements")] long FastArrayElements,
    [property: JsonPropertyName("binary_object_count")] long BinaryObjectCount,
    [property: JsonPropertyName("binary_object_size")] long BinaryObjectSize);