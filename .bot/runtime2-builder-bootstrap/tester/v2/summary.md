# tester v2 — verifying coder's response to v1

## What this is

Round 2 of test-quality review on `runtime2-builder-bootstrap`. v1 (commit `d8eb2958`) flagged 12 findings, verdict `needs-fixes`. The coder pushed 5 commits (`d8eb2958..bbf982d4`) addressing 10 of those. v2 verifies the closures, hunts false greens in the new tests, and decides whether the untouched failures (F2/F3/F4) block.

## What was done

Three-pass review:

**Pass 1 — closure verification with deletion test.** For each of the 10 closures, traced production code to confirm the new test asserts the right thing. Strongest closures: F1 (BuildingGuard removal — 4 layers verified to still enforce), F7 (Read ResolveVariables — security guard exercises real `!app.Id` resolution path), F11 (ValidateResponse — exact production-string pin), F9/F12 (stateful-lambda + StatusCode pins).

**Pass 2 — re-run all suites.**
- C# (TUnit): **2288 / 2288 / 0 fail** (was 2281/8 fail). Fully green.
- PLang `/Tests/`: 161 / 132 / 25 fail / 4 stale (unchanged).
- PLang `/tests/`: 9 / 8 / 1 fail (unchanged).

The 25 PLang reds are F2 (BuilderValidateValid `int = 1` × ~80 conversions), F3 (Loop string-concat), F4 (Signing cluster + Identity + UI render + ContextVars + ConditionCompound + ForeachDictionary + Event + Goal/Relative + Test/Discover + Crypto + ErrorTypes + App/SetupGoal). These are real production failures the coder did not address.

**Pass 3 — coverage spot-check.** `file/read.cs` 62.5% → 100%. `Utils/TypeConverter.cs` 50.4% → 54.9% (auto-wrap branch covered; many other paths still untested). `promoteGroups.cs` + `enrichResponse.cs` still 0% but XML-doc-declared "build-time only" — verified `--test` doesn't reach either (and `promoteGroups` is unreachable from any goal anywhere, including bootstrap).

## Caveats / minor findings

1. **F8 ListOfStringToListOfString_PassesThrough is mislabeled.** Comment claims "sanity guard that auto-wrap doesn't engage on already-list inputs," but the input List<string> is consumed by the list-conversion branch at `TypeConverter.cs:126`, not the auto-wrap at line 156. Deleting the auto-wrap branch would NOT fail this test. Cosmetic only — the other 3 Gap 3 tests do exercise the wrap path correctly.

2. **F5 still has zero non-Invariant culture coverage.** The format-side fix is correct at all three sites (`ExampleRenderer:108`, `FluidProvider:143`, `DefaultBuilderProvider FormatValue:439`), but `Phase05_CultureInfo_DefaultsToInvariant` only verifies the App.Culture default — it doesn't set `Thread.CurrentCulture=it-IT` and assert the format produces `"3.14"`. A regression that flipped any of the three sites back to `conv.ToString()` would slip through.

3. **PromoteGroups is unreachable from any goal anywhere.** XML doc says "exercised by bootstrap cycle" but `grep` across `Tests/`, `tests/`, and `os/` finds zero references in any `.goal`. Either delete the action or wire it into the build pipeline.

## Code example

The strongest closure (F7) — deletion-test verified:

```csharp
// FileHandlerTests.cs:139
[Test]
public async Task Read_ResolveVariablesTrue_BlocksInfrastructureVariables()
{
    _fs.File.WriteAllText(TempPath("untrusted.txt"), "id is %!app.Id%");
    var action = new Read {
        Context = _app.Context, Path = MakePath("untrusted.txt"),
        ResolveVariables = new global::App.Data.@this<bool>("ResolveVariables", true)
    };
    var result = await action.Run();
    await Assert.That(result.Value).IsEqualTo("id is %!app.Id%");
    // Without skipInfrastructure:true at read.cs:30,
    // !app is registered as DynamicData (Actor/Context/this.cs:172)
    // → resolves to actual App.Id GUID → test fails.
}
```

## Verdict

**needs-fixes.** 10 of 12 v1 findings closed cleanly. C# suite is 100% green. But F2/F3/F4 — 25 PLang test failures introduced by the v2 builder squash — were not addressed. Recommend back to coder for those three clusters; they're real runtime bugs (LLM annotation strings reaching `Convert.ChangeType`, foreach producing string concat instead of arithmetic sum, signing module routing). Then re-run tester before security/auditor.
