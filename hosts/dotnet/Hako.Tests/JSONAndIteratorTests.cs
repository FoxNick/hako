using HakoJS.Extensions;

namespace HakoJS.Tests;

/// <summary>
/// Tests for JSON operations and iterators.
/// </summary>
public class JSONAndIteratorTests : TestBase
{
    public JSONAndIteratorTests(HakoFixture fixture) : base(fixture) { }

    #region JSON Tests

    [Fact]
    public void ParseJson_ValidJson_Parses()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.ParseJson(@"{""name"":""test"",""value"":42}");

        Assert.True(obj.IsObject());

        using var name = obj.GetProperty("name");
        Assert.Equal("test", name.AsString());

        using var value = obj.GetProperty("value");
        Assert.Equal(42, value.AsNumber());
    }

    [Fact]
    public void ParseJson_ComplexObject_ParsesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.ParseJson(@"
        {
            ""name"": ""my-package"",
            ""version"": ""1.2.3"",
            ""dependencies"": {
                ""lodash"": ""^4.17.21"",
                ""react"": ""^18.0.0""
            },
            ""scripts"": {
                ""test"": ""jest"",
                ""build"": ""webpack""
            }
        }");

        Assert.True(obj.IsObject());

        using var name = obj.GetProperty("name");
        Assert.Equal("my-package", name.AsString());

        using var deps = obj.GetProperty("dependencies");
        Assert.True(deps.IsObject());

        using var lodash = deps.GetProperty("lodash");
        Assert.Equal("^4.17.21", lodash.AsString());
    }

    [Fact]
    public void ParseJson_EmptyString_ReturnsUndefined()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var result = realm.ParseJson("");

        Assert.True(result.IsUndefined());
    }

    [Fact]
    public void ParseJson_InvalidJson_Throws()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();

        Assert.Throws<HakoJS.Exceptions.HakoException>(() =>
        {
            realm.ParseJson("{invalid json}");
        });
    }

    [Fact]
    public void BJSONEncode_SimpleObject_Encodes()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.NewObject();
        obj.SetProperty("test", 42);

        var bjson = realm.BJSONEncode(obj);

        Assert.NotNull(bjson);
        Assert.True(bjson.Length > 0);
    }

    [Fact]
    public void BJSONDecode_EncodedData_DecodesCorrectly()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var originalObj = realm.NewObject();
        originalObj.SetProperty("name", "test");
        originalObj.SetProperty("value", 42);
        originalObj.SetProperty("active", true);

        var bjson = realm.BJSONEncode(originalObj);
        using var decoded = realm.BJSONDecode(bjson);

        Assert.True(decoded.IsObject());

        using var name = decoded.GetProperty("name");
        Assert.Equal("test", name.AsString());

        using var value = decoded.GetProperty("value");
        Assert.Equal(42, value.AsNumber());

        using var active = decoded.GetProperty("active");
        Assert.True(active.AsBoolean());
    }

    [Fact]
    public void BJSONRoundTrip_ComplexObject_PreservesStructure()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
    
        var data = new Dictionary<string, object>
        {
            ["str"] = "hello",
            ["num"] = 42,
            ["boolean"] = true,
            ["array"] = new object[] { 1, 2, 3 },
            ["nested"] = new Dictionary<string, object>
            {
                ["a"] = 1,
                ["b"] = 2
            }
        };
    
        using var original = realm.NewValue(data);

        var bjson = realm.BJSONEncode(original);
        using var decoded = realm.BJSONDecode(bjson);

        using var str = decoded.GetProperty("str");
        Assert.Equal("hello", str.AsString());

        using var num = decoded.GetProperty("num");
        Assert.Equal(42, num.AsNumber());

        using var arr = decoded.GetProperty("array");
        Assert.True(arr.IsArray());
    
        using var nested = decoded.GetProperty("nested");
        using var nestedA = nested.GetProperty("a");
        Assert.Equal(1, nestedA.AsNumber());
    }

    [Fact]
    public void Dump_Object_ReturnsRepresentation()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var obj = realm.NewObject();
        obj.SetProperty("name", "test");
        obj.SetProperty("value", 42);

        var dump = realm.Dump(obj);

        Assert.NotNull(dump);
    }

    #endregion

    #region Iterator Tests

    [Fact]
    public void GetIterator_WithArray_Iterates()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.EvalCode("[1, 2, 3]").Unwrap();
        using var iterResult = realm.GetIterator(arr);

        Assert.True(iterResult.IsSuccess);
        using var iterator = iterResult.Unwrap();
        Assert.NotNull(iterator);
    }

    [Fact]
    public void Iterate_Array_ReturnsAllElements()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var arr = realm.EvalCode("[1, 2, 3, 4, 5]").Unwrap();

        var values = new List<double>();
        foreach (var itemResult in arr.Iterate())
        {
            if (itemResult.TryGetSuccess(out var item))
            {
                using (item)
                {
                    values.Add(item.AsNumber());
                }
            }
        }

        Assert.Equal(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, values);
    }

    [Fact]
    public void Iterate_Map_ReturnsKeyValuePairs()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var map = realm.EvalCode(@"
            const map = new Map();
            map.set('key1', 'value1');
            map.set('key2', 'value2');
            map.set('key3', 'value3');
            map;
        ").Unwrap();

        var entries = new Dictionary<string, string>();
        foreach (var itemResult in map.Iterate())
        {
            if (itemResult.TryGetSuccess(out var entry))
            {
                using (entry)
                {
                    using var key = entry.GetProperty(0);
                    using var value = entry.GetProperty(1);
                    entries[key.AsString()] = value.AsString();
                }
            }
        }

        Assert.Equal(3, entries.Count);
        Assert.Equal("value1", entries["key1"]);
        Assert.Equal("value2", entries["key2"]);
        Assert.Equal("value3", entries["key3"]);
    }

    [Fact]
    public void IterateMap_WithGenericTypes_Works()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var map = realm.EvalCode(@"
            const map = new Map();
            map.set('a', 1);
            map.set('b', 2);
            map.set('c', 3);
            map;
        ").Unwrap();

        var entries = new Dictionary<string, int>();
        foreach (var (key, value) in map.IterateMap<string, int>())
        {
            entries[key] = value;
        }

        Assert.Equal(3, entries.Count);
        Assert.Equal(1, entries["a"]);
        Assert.Equal(2, entries["b"]);
        Assert.Equal(3, entries["c"]);
    }

    [Fact]
    public void IterateSet_ReturnsAllValues()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var set = realm.EvalCode(@"
            const set = new Set();
            set.add(1);
            set.add(2);
            set.add(3);
            set;
        ").Unwrap();

        var values = set.IterateSet<int>().ToList();

        Assert.Equal(3, values.Count);
        Assert.Contains(1, values);
        Assert.Contains(2, values);
        Assert.Contains(3, values);
    }

    [Fact]
    public void GetWellKnownSymbol_Iterator_ReturnsSymbol()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var symbol = realm.GetWellKnownSymbol("iterator");

        Assert.True(symbol.IsSymbol());
    }

    [Fact]
    public void GetWellKnownSymbol_AsyncIterator_ReturnsSymbol()
    {
        if (!IsAvailable) return;

        using var realm = Hako.Runtime.CreateRealm();
        using var symbol = realm.GetWellKnownSymbol("asyncIterator");

        Assert.True(symbol.IsSymbol());
    }

    #endregion
}
