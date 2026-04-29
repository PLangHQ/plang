namespace PLang.Tests.Generator.Matrix.Modifier;

// Matrix entry for [Modifier] handlers — wrap, retry, or short-circuit dispatch.
// v4 contract: Action.RunAsync passes a dispatch lambda to Modifiers.RunAsync;
//   each modifier-driven dispatch invokes App.Run, which pushes/pops a callstack frame independently.
// A retry modifier that calls dispatch twice produces two frame push/pop pairs (correct, not a bug).

public class ModifierActionTests
{
    // Modifier wraps dispatch (e.g., Wrap(...)) — handler runs once, frame push/pop is symmetric.
    [Test] public async Task ModifierAction_WrapDispatch_FramePushedOnce() => Assert.Fail("Not implemented");

    // Modifier retries dispatch twice — App.Run executes twice, two frame push/pops, two snapshots.
    [Test] public async Task ModifierAction_RetryTwice_TwoFramesPushed() => Assert.Fail("Not implemented");

    // Modifier short-circuits (returns Handled=true) → App.Run not invoked, no frame, but result still flows back as __data__.
    [Test] public async Task ModifierAction_HandledOverride_BypassesAppRun() => Assert.Fail("Not implemented");

    // Handled-override result still fires AfterAction events on Action.RunAsync.
    [Test] public async Task ModifierAction_HandledOverride_FiresAfterActionEvents() => Assert.Fail("Not implemented");
}
