using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for JavaScript value creation and manipulation.
/// </summary>
public class ValueTests : TestBase
{
    public ValueTests(HakoFixture fixture) : base(fixture) { }

    [Fact]
    public void NewValue_PrimitiveTypes_ConvertsCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var str = realm.NewValue("hello");
        Assert.True(str.IsString());
        Assert.Equal("hello", str.AsString());

        using var num = realm.NewValue(42.5);
        Assert.True(num.IsNumber());
        Assert.Equal(42.5, num.AsNumber());

        using var boolean = realm.NewValue(true);
        Assert.True(boolean.IsBoolean());
        Assert.True(boolean.AsBoolean());

        using var nullVal = realm.NewValue(null);
        Assert.True(nullVal.IsNull());
    }

    [Fact]
    public void NewValue_Array_ConvertsCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.NewValue(new object[] { 1, "two", true });

        Assert.True(arr.IsArray());

        using var length = arr.GetProperty("length");
        Assert.Equal(3, length.AsNumber());

        using var elem0 = arr.GetProperty(0);
        using var elem1 = arr.GetProperty(1);
        using var elem2 = arr.GetProperty(2);

        Assert.Equal(1, elem0.AsNumber());
        Assert.Equal("two", elem1.AsString());
        Assert.True(elem2.AsBoolean());
    }

    [Fact]
    public void NewObject_WithProperties_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.NewObject();

        obj.SetProperty("name", "test");
        obj.SetProperty("value", 42);
        obj.SetProperty("active", true);

        using var name = obj.GetProperty("name");
        using var value = obj.GetProperty("value");
        using var active = obj.GetProperty("active");

        Assert.Equal("test", name.AsString());
        Assert.Equal(42, value.AsNumber());
        Assert.True(active.AsBoolean());
    }

    [Fact]
    public void NewArray_WithElements_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.NewArray();

        arr.SetProperty(0, "hello");
        arr.SetProperty(1, 42);
        arr.SetProperty(2, true);

        using var length = arr.GetProperty("length");
        Assert.Equal(3, length.AsNumber());

        using var elem0 = arr.GetProperty(0);
        using var elem1 = arr.GetProperty(1);
        using var elem2 = arr.GetProperty(2);

        Assert.Equal("hello", elem0.AsString());
        Assert.Equal(42, elem1.AsNumber());
        Assert.True(elem2.AsBoolean());
    }

    [Fact]
    public void NewArrayBuffer_CreatesCorrectBuffer()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var buffer = realm.NewArrayBuffer(data);

        Assert.True(buffer.IsArrayBuffer());

        var retrieved = buffer.CopyArrayBuffer();
        Assert.Equal(5, retrieved.Length);
        Assert.Equal(data, retrieved);
    }

    [Fact]
    public void NewUint8Array_CreatesCorrectArray()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var bytes = new byte[] { 10, 20, 30 };
        using var arr = realm.NewUint8Array(bytes);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Uint8Array, arr.GetTypedArrayType());

        var copied = arr.CopyTypedArray();
        Assert.Equal(bytes, copied);
    }

    [Fact]
    public void NewInt32Array_CreatesCorrectArray()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var ints = new int[] { 100, 200, 300 };
        using var arr = realm.NewInt32Array(ints);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Int32Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void NewFloat64Array_CreatesCorrectArray()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var doubles = new double[] { 1.1, 2.2, 3.3 };
        using var arr = realm.NewFloat64Array(doubles);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Float64Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void NewTypedArray_CreatesCorrectType()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.NewTypedArray(10, TypedArrayType.Uint8Array);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Uint8Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void NewTypedArrayWithBuffer_UsesExistingBuffer()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var buffer = realm.NewArrayBuffer(data);
        using var arr = realm.NewTypedArrayWithBuffer(buffer, 0, 4, TypedArrayType.Uint8Array);

        Assert.True(arr.IsTypedArray());
        Assert.Equal(TypedArrayType.Uint8Array, arr.GetTypedArrayType());
    }

    [Fact]
    public void Undefined_ReturnsUndefined()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var undef = realm.Undefined();

        Assert.True(undef.IsUndefined());
    }

    [Fact]
    public void Null_ReturnsNull()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var nullVal = realm.Null();

        Assert.True(nullVal.IsNull());
    }

    [Fact]
    public void True_ReturnsTrue()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var trueVal = realm.True();

        Assert.True(trueVal.IsBoolean());
        Assert.True(trueVal.AsBoolean());
    }

    [Fact]
    public void False_ReturnsFalse()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var falseVal = realm.False();

        Assert.True(falseVal.IsBoolean());
        Assert.False(falseVal.AsBoolean());
    }

    [Fact]
    public void GetGlobalObject_ReturnsGlobal()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var global = realm.GetGlobalObject();

        Assert.NotNull(global);
        Assert.True(global.IsObject());
    }

    [Fact]
    public void GlobalObject_CanSetAndGetProperties()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var global = realm.GetGlobalObject();

        global.SetProperty("testGlobal", 42);

        using var result = realm.EvalCode("testGlobal + 10");
        using var value = result.Unwrap();
        Assert.Equal(52, value.AsNumber());
    }

    [Fact]
    public void NewObjectWithPrototype_UsesPrototype()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var proto = realm.NewObject();
        proto.SetProperty("inherited", true);

        using var obj = realm.NewObjectWithPrototype(proto);

        Assert.True(obj.IsObject());
    }

    [Fact]
    public void DupValue_CreatesIndependentCopy()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var original = realm.NewNumber(42);
        var ptr = original.GetHandle();

        using var duplicate = realm.DupValue(ptr);

        Assert.Equal(42, duplicate.AsNumber());
    }

    [Fact]
    public void ToNativeValue_ConvertsCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        using var str = realm.NewString("hello");
        using var strBox = str.ToNativeValue<string>();
        Assert.Equal("hello", strBox.Value);

        using var num = realm.NewNumber(42.5);
        using var numBox = num.ToNativeValue<double>();
        Assert.Equal(42.5, numBox.Value);

        using var boolean = realm.True();
        using var boolBox = boolean.ToNativeValue<bool>();
        Assert.True(boolBox.Value);
    }
}
