using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests;

/// <summary>
/// Test ergonomics for <see cref="global::app.data.@this"/> result assertions.
/// <c>Assert.That(result.Success).IsTrue()</c> reports only "expected true,
/// found false" — useless when a handler failed. <c>await result.IsSuccess()</c>
/// surfaces <c>Error.Key</c> + <c>Error.Message</c> in the failure text so the
/// root cause is visible without a debugger or a Console probe.
/// </summary>
public static class DataAssertions
{
    /// <summary>Assert the Data succeeded; on failure show the error key + message.</summary>
    public static async Task IsSuccess(this global::app.data.@this data)
    {
        await Assert.That(data.Success).IsTrue()
            .Because($"Data failed: [{data.Error?.Key}] {data.Error?.Message}");
    }

    /// <summary>Assert the Data failed; on failure show the value that leaked through.</summary>
    public static async Task IsFailure(this global::app.data.@this data)
    {
        await Assert.That(data.Success).IsFalse()
            .Because($"Expected failure but Data succeeded with value: {data.Value}");
    }
}
