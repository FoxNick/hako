using HakoJS.Extensions;
using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for JavaScript value creation and manipulation.
/// </summary>
public class DateTests : TestBase
{
    public DateTests(HakoFixture fixture) : base(fixture) { }

    #region DateTime/Date Tests

[Fact]
public void NewDate_CreatesDateFromDateTime()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var now = DateTime.UtcNow;
    using var date = realm.NewDate(now);

    Assert.True(date.IsDate());
    Assert.False(date.IsNumber());
    Assert.False(date.IsString());
    Assert.True(date.IsObject());
}

[Fact]
public void AsDateTime_ConvertsDateToDateTime()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var original = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
    using var date = realm.NewDate(original);

    var result = date.AsDateTime();

    Assert.Equal(DateTimeKind.Utc, result.Kind);
    Assert.Equal(original, result);
}

[Fact]
public void NewDate_RoundTrip_PreservesValue()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var original = DateTime.UtcNow;
    
    using var date = realm.NewDate(original);
    var roundTripped = date.AsDateTime();

    // Should be equal to millisecond precision (JavaScript Date precision)
    Assert.Equal(original.Year, roundTripped.Year);
    Assert.Equal(original.Month, roundTripped.Month);
    Assert.Equal(original.Day, roundTripped.Day);
    Assert.Equal(original.Hour, roundTripped.Hour);
    Assert.Equal(original.Minute, roundTripped.Minute);
    Assert.Equal(original.Second, roundTripped.Second);
    Assert.Equal(original.Millisecond, roundTripped.Millisecond);
}

[Fact]
public void AsDateTime_ThrowsOnNonDate()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    using var number = realm.NewNumber(42);

    Assert.Throws<InvalidOperationException>(() => number.AsDateTime());
}

[Fact]
public void NewDate_WithEpoch_CreatesCorrectDate()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    using var date = realm.NewDate(epoch);

    var result = date.AsDateTime();

    Assert.Equal(epoch, result);
}

[Fact]
public void NewDate_WithFutureDate_WorksCorrectly()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var future = new DateTime(2050, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    using var date = realm.NewDate(future);

    var result = date.AsDateTime();

    Assert.Equal(future.Year, result.Year);
    Assert.Equal(future.Month, result.Month);
    Assert.Equal(future.Day, result.Day);
}

[Fact]
public void NewDate_WithLocalTime_ConvertsToUtc()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var local = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Local);
    var expectedUtc = local.ToUniversalTime();
    
    using var date = realm.NewDate(local);
    var result = date.AsDateTime();

    Assert.Equal(DateTimeKind.Utc, result.Kind);
    Assert.Equal(expectedUtc, result);
}

[Fact]
public void Date_InObject_WorksCorrectly()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var now = DateTime.UtcNow;
    
    using var obj = realm.NewObject();
    obj.SetProperty("timestamp", realm.NewDate(now));

    using var timestampProp = obj.GetProperty("timestamp");
    
    Assert.True(timestampProp.IsDate());
    var result = timestampProp.AsDateTime();
    
    Assert.Equal(now.Year, result.Year);
    Assert.Equal(now.Month, result.Month);
    Assert.Equal(now.Day, result.Day);
}

[Fact]
public void Date_InArray_WorksCorrectly()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var dates = new[]
    {
        new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
        new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)
    };

    using var arr = dates.ToJSArray(realm);

    for (int i = 0; i < dates.Length; i++)
    {
        using var element = arr.GetProperty(i);
        Assert.True(element.IsDate());
        
        var result = element.AsDateTime();
        Assert.Equal(dates[i], result);
    }
}

[Fact]
public void Date_CreatedInJavaScript_CanBeRead()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    using var result = realm.EvalCode("new Date('2024-06-15T14:30:00.000Z')");
    using var date = result.Unwrap();

    Assert.True(date.IsDate());
    
    var dt = date.AsDateTime();
    
    Assert.Equal(2024, dt.Year);
    Assert.Equal(6, dt.Month);
    Assert.Equal(15, dt.Day);
    Assert.Equal(14, dt.Hour);
    Assert.Equal(30, dt.Minute);
    Assert.Equal(0, dt.Second);
}


[Fact]
public void Date_ToNativeValue_WorksCorrectly()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var original = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
    using var date = realm.NewDate(original);

    using var box = date.ToNativeValue<DateTime>();
    
    Assert.Equal(original, box.Value);
    Assert.Equal(DateTimeKind.Utc, box.Value.Kind);
}

[Fact]
public void NewValue_DateTime_CreatesDate()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    var now = DateTime.UtcNow;
    using var date = realm.NewValue(now);

    Assert.True(date.IsDate());
    
    var result = date.AsDateTime();
    Assert.Equal(now.Year, result.Year);
    Assert.Equal(now.Month, result.Month);
    Assert.Equal(now.Day, result.Day);
}

[Fact]
public void Date_InvalidDate_ThrowsException()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    using var result = realm.EvalCode("new Date('invalid')");
    using var invalidDate = result.Unwrap();

    Assert.True(invalidDate.IsDate());
    Assert.Throws<InvalidOperationException>(() => invalidDate.AsDateTime());
}

[Fact]
public void Date_MinMaxValues_WorkCorrectly()
{
    if (!IsAvailable) return;

    using var realm = Hako.Runtime.CreateRealm();
    
    // Test with a reasonable min date (JavaScript Date has different limits than DateTime)
    var minDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    using var min = realm.NewDate(minDate);
    var minResult = min.AsDateTime();
    Assert.Equal(minDate, minResult);

    // Test with a far future date
    var maxDate = new DateTime(2100, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    using var max = realm.NewDate(maxDate);
    var maxResult = max.AsDateTime();
    Assert.Equal(maxDate.Year, maxResult.Year);
}

#endregion
}
