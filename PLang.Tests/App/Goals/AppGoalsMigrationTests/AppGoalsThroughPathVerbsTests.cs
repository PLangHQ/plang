using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Goals.AppGoalsMigrationTests;

/// <summary>
/// Stage 4 — Batch 5. <c>AppGoals</c> migration (D5) + <c>App.Load</c>/<c>App.Save</c>
/// for <c>app.pr</c> (D6).
///
/// AppGoals' direct <c>System.IO</c> reaches (LoadFromFileAsync, TryLoadPr,
/// GetByPrPathAsync, LoadFromDirectoryAsync) rewrite to <c>path.Resolve()</c>
/// + <c>path.ReadText()</c> + <c>path.List()</c>. App.Load/Save lift to Path
/// verbs against <c>.build/app.pr</c>.
/// </summary>
public class AppGoalsThroughPathVerbsTests
{
    [Test] public async Task LoadFromDirectoryAsync_UsesPathListNotDirectoryGetFiles()
    {
        // No System.IO.Directory.GetFiles in the call path — only path.List().
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task LoadFromDirectoryAsync_DeepTree_LoadsEveryGoalFile()
    {
        // Recurse depth ≥ 3, all .goal files surface in AppGoals.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task LoadFromFileAsync_UsesPathReadTextNotFileReadAllText()
    {
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task GetByPrPathAsync_ResolvesRelativeAndAbsolute_ViaPath()
    {
        // Both `Start.pr` and `<root>/.build/Start.pr` resolve to the same Goal.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task AppGoals_FuzzyGetByName_StaysSeparateFromPathKeying()
    {
        // Get("ProcessData") uses a name index, not the Path-keyed dict. D4.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task AppLoad_OnColdStart_NoAppPr_ReturnsEmptyState_NoThrow()
    {
        // Bootstrap with no .build/app.pr → clean empty state.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task AppLoad_OnCorruptAppPr_ReturnsFailureNotCrash()
    {
        // Malformed JSON → Data.Fail with a parse error, not an unhandled exception.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task AppSave_RoundTrip_WrittenAppPr_RehydratesUnderAppLoad()
    {
        // Save → Load yields equivalent App state. Verifies the Path-verb plumbing.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
