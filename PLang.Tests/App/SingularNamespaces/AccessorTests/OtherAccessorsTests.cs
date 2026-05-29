using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch D (part 2) — the remaining accessor renames: event, format, variable, error, navigator.
// Plus the headline negative guard: the four App* wrapper aliases (AppGoals/AppChannels/AppEvents/AppModules)
// no longer exist anywhere in the codebase.
public class OtherAccessorsTests
{
    // event registry — register / unregister / getbindings round-trip. No .current.
    [Test] public async Task AppEvent_RegisterUnregister_RoundTripsBinding()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppEvent_GetBindings_ReturnsTheRegisteredBindings()
        => Assert.Fail("Not implemented");

    // format registry — name lookups (Mime / KindOf / Compressible) stay as verb methods.
    [Test] public async Task AppFormat_LookupByName_ReturnsMimeAndCompressibleInfo()
        => Assert.Fail("Not implemented");

    // variable — Set stays a verb (mutation); Get reads cleaner as ["name"] in some sites.
    [Test] public async Task ContextVariable_IndexByName_AfterSet_ReturnsValue()
        => Assert.Fail("Not implemented");

    [Test] public async Task ContextVariable_Set_RemainsAVerb_NotIndexerAssignment()
        => Assert.Fail("Not implemented");

    // error registry + trail — Push/Count/Error/Trail are real operations, kept as verbs.
    [Test] public async Task AppError_PushAndCount_RoundTripsThroughTheRegistry()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppError_Trail_RestoreTrail_ReplaysTheErrorChain()
        => Assert.Fail("Not implemented");

    // navigator — Get → [type]; per-type navigator selection.
    [Test] public async Task AppNavigator_IndexByType_ReturnsTheNavigatorForThatType()
        => Assert.Fail("Not implemented");

    // The OBP-violation aliases removed in Stage 3 — reflection probe of GlobalUsings.
    [Test] public async Task AppStarAliases_AppGoalsAppChannelsAppEventsAppModules_NoLongerExist()
        => Assert.Fail("Not implemented");

    // Subsystems renamed (Stage 1): no `app.<old-plural>` namespace resolves.
    [Test] public async Task LegacyPluralNamespaces_DoNotResolve_AfterRename()
        => Assert.Fail("Not implemented");
}
