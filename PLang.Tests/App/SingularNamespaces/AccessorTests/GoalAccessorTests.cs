using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch A — app.goal collection node (Stage 3).
// `app.goal` is the collection (goal.list.@this), `app.goal["name"]` selects,
// `app.goal.list` enumerates, `app.goal.current` reads CallStack.Current.Action.Step.Goal,
// `app.goal["nope"]` throws (index-miss is a hard error).
public class GoalAccessorTests
{
    [Test] public async Task AppGoal_IndexByName_ReturnsTheNamedGoal()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppGoal_IndexByPrPath_ReturnsTheGoalForThatPath()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppGoal_IndexByPathInstance_ReturnsSameGoalAsStringPath()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppGoalList_Enumerates_LoadedGoals_ExcludingSetup()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppGoalCurrent_MidRun_ReturnsExecutingGoal()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppGoalCurrent_AtRest_IsNull()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppGoal_IndexOfUnknownName_ThrowsTypedError()
        => Assert.Fail("Not implemented");

    // Goal collection is selection + lifecycle + enumeration only — no per-element behavior on the registry.
    [Test] public async Task GoalListType_ExposesNoIoOrPerElementBehavior_OnTheRegistry()
        => Assert.Fail("Not implemented");
}
