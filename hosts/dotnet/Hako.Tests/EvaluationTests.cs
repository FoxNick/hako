using HakoJS.Exceptions;
using HakoJS.Extensions;
using HakoJS.VM;

namespace HakoJS.Tests;

/// <summary>
/// Tests for JavaScript code evaluation.
/// </summary>
public class EvaluationTests : TestBase
{
    public EvaluationTests(HakoFixture fixture) : base(fixture) { }

    [Fact]
    public void EvalCode_SimpleExpression_ReturnsCorrectResult()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode("1 + 2"))
            {
                Assert.True(result.IsSuccess);
                using (var value = result.Unwrap())
                {
                    Assert.Equal(3, value.AsNumber());
                }
            }
        }
    }

    [Fact]
    public void EvalCode_WithVariables_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode("let x = 5; let y = 10; x + y"))
            {
                Assert.True(result.IsSuccess);
                using (var value = result.Unwrap())
                {
                    Assert.Equal(15, value.AsNumber());
                }
            }
        }
    }

    [Fact]
    public void EvalCode_WithSyntaxError_ReturnsFailure()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode("let x = ;"))
            {
                Assert.True(result.IsFailure);
                Assert.Throws<InvalidOperationException>(() => result.Unwrap());
            }
        }
    }

    [Fact]
    public void EvalCode_WithRuntimeError_ThrowsException()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode("throw new Error('Test error');"))
            {
                Assert.True(result.IsFailure);
                Assert.Throws<InvalidOperationException>(() => result.Unwrap());
            }
        }
    }

    [Fact]
    public async Task EvalAsync_SimpleExpression_ReturnsValue()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = await realm.EvalAsync("2 + 2"))
            {
                Assert.Equal(4, result.AsNumber());
            }
        }
    }

    [Fact]
    public async Task EvalAsync_WithPromise_AwaitsResolution()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = await realm.EvalAsync("Promise.resolve(42)"))
            {
                Assert.Equal(42, result.AsNumber());
            }
        }
    }

    [Fact]
    public async Task EvalAsync_WithRejectedPromise_ThrowsException()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            await Assert.ThrowsAsync<HakoException>(async () =>
            {
                await realm.EvalAsync("Promise.reject('error')");
            });
        }
    }

    [Fact]
    public async Task EvalAsync_GenericInt_ReturnsTypedValue()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            var result = await realm.EvalAsync<int>("10 + 5");
            Assert.Equal(15, result);
        }
    }

    [Fact]
    public async Task EvalAsync_GenericString_ReturnsTypedValue()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            var result = await realm.EvalAsync<string>("'hello ' + 'world'");
            Assert.Equal("hello world", result);
        }
    }

    [Fact]
    public async Task EvalAsync_GenericBool_ReturnsTypedValue()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            var result = await realm.EvalAsync<bool>("true");
            Assert.True(result);
        }
    }

    [Fact]
    public async Task EvalAsync_WithSyntaxError_ThrowsException()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            await Assert.ThrowsAsync<HakoException>(async () =>
            {
                await realm.EvalAsync("this is not valid javascript");
            });
        }
    }

    [Fact]
    public void EvalCode_EmptyString_ReturnsUndefined()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode(""))
            {
                Assert.True(result.IsSuccess);
                using (var value = result.Unwrap())
                {
                    Assert.True(value.IsUndefined());
                }
            }
        }
    }

    [Fact]
    public void UnwrapResult_WithSuccess_ReturnsValue()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var successResult = realm.EvalCode("40 + 2"))
            {
                using (var successValue = successResult.Unwrap())
                {
                    Assert.Equal(42, successValue.AsNumber());
                }
            }
        }
    }

    [Fact]
    public void UnwrapResult_WithError_ThrowsException()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var errorResult = realm.EvalCode("throw new Error('Test error');"))
            {
                Assert.Throws<InvalidOperationException>(() => errorResult.Unwrap());
            }
        }
    }

    [Fact]
    public void EvalCode_WithMap_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode(@"
            const map = new Map();
            map.set('key1', 'value1');
            map.set('key2', 'value2');
            map.get('key1');
        "))
            {
                Assert.True(result.IsSuccess);
                using (var value = result.Unwrap())
                {
                    Assert.Equal("value1", value.AsString());
                }
            }
        }
    }

    [Fact]
    public void EvalCode_WithCustomFilename_WorksCorrectly()
    {
        if (!IsAvailable) return;

        using (var realm = Hako.Runtime.CreateRealm())
        {
            using (var result = realm.EvalCode("1 + 2", new RealmEvalOptions { FileName = "test.js" }))
            {
                Assert.True(result.IsSuccess);
                using (var value = result.Unwrap())
                {
                    Assert.Equal(3, value.AsNumber());
                }
            }
        }
    }
}