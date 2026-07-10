using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests;

/// <summary>
/// Test ergonomics for the plang <see cref="global::app.type.item.@bool.@this"/>. Lets a
/// test assert on a plang bool directly — <c>await someBool.IsTrue()</c> — instead of
/// reaching through <c>.Value</c> to the CLR bool. Matches the <see cref="DataAssertions"/>
/// extension-on-value style rather than TUnit's <c>Assert.That</c> generic chain.
/// </summary>
public static class BoolAssertions
{
    /// <summary>Assert the plang bool is true.</summary>
    public static async Task IsTrue(this global::app.type.item.@bool.@this value, string? because = null)
        => await Assert.That(value.Value).IsTrue().Because(because ?? "expected plang @bool true");

    /// <summary>Assert the plang bool is false.</summary>
    public static async Task IsFalse(this global::app.type.item.@bool.@this value, string? because = null)
        => await Assert.That(value.Value).IsFalse().Because(because ?? "expected plang @bool false");

    /// <summary>Assert a resolved-async plang bool is true (e.g. <c>await list.Contains(x)</c>).</summary>
    public static async Task IsTrue(this System.Threading.Tasks.ValueTask<global::app.type.item.@bool.@this> value, string? because = null)
        => await (await value).IsTrue(because);

    /// <summary>Assert a resolved-async plang bool is false.</summary>
    public static async Task IsFalse(this System.Threading.Tasks.ValueTask<global::app.type.item.@bool.@this> value, string? because = null)
        => await (await value).IsFalse(because);
}
