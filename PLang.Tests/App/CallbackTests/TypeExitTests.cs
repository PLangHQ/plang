using TUnit.Core;
using global::App;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 3: `Type.Exit()` extension and the `Ask` IExitsGoal marker.
/// `Type.Exit()` is the only engine-side discriminator for "this Data exits the
/// goal" — query is `result.Type?.ClrType?.Exit() == true`.
public class TypeExitTests
{
    [Test] public Task TypeExit_TrueFor_Ask()                                   { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task TypeExit_FalseFor_String()                               { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task TypeExit_FalseFor_ByteArray()                            { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task TypeExit_FalseFor_PlainClassWithoutMarker()              { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task TypeExit_FalseFor_GenericDataOfNonExitT()                { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Ask_ImplementsIExitsGoal()                               { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
