using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app;
using app.type;
using app.type.catalog;
using app.module.output;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 3: `Type.Exit()` extension and the `Ask` IExitsGoal marker.
/// `Type.Exit()` is the only engine-side discriminator for "this Data exits the
/// goal" — query is `result.Type?.ClrType?.Exit() == true`.
public class TypeExitTests
{
    [Test] public async Task TypeExit_TrueFor_Ask()
        => await Assert.That(typeof(Ask).Exit()).IsTrue();

    [Test] public async Task TypeExit_FalseFor_String()
        => await Assert.That(typeof(string).Exit()).IsFalse();

    [Test] public async Task TypeExit_FalseFor_ByteArray()
        => await Assert.That(typeof(byte[]).Exit()).IsFalse();

    [Test] public async Task TypeExit_FalseFor_PlainClassWithoutMarker()
        => await Assert.That(typeof(System.Text.StringBuilder).Exit()).IsFalse();

    [Test] public async Task TypeExit_FalseFor_GenericDataOfNonExitT()
    {
        var d = new global::app.data.@this<global::app.type.text.@this>("", "hello");
        await Assert.That(d.Value?.GetType().Exit() ?? false).IsFalse();
    }

    [Test] public async Task Ask_ImplementsIExitsGoal()
        => await Assert.That(typeof(IExitsGoal).IsAssignableFrom(typeof(Ask))).IsTrue();
}
