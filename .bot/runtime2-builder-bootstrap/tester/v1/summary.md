# Tester v1 — Summary

## What this is

`runtime2-builder-bootstrap` is the v2 self-hosting builder branch — what the coder handover described as a 3-gap fix is actually the full builder + diagnostics + type-system pipeline (~2300 files, ~30k inserts). Codeanalyzer ran four code-review rounds (v1–v4); v4 was CLEAN with two carryovers escalated to me: format-side InvariantCulture asymmetry and `promoteGroups` zero coverage.

My job: run the tests, run coverage, hunt false greens. I'm version v1 because there's only one coder version directory.

## What was done

**Test runs.**

| Suite | Total | Pass | Fail | Stale |
|---|---:|---:|---:|---:|
| C# | 2289 | 2281 | 8 | – |
| PLang `/Tests/` | 161 | 132 | 25 | 4 |
| PLang `/tests/` | 9 | 8 | 1 | 0 |

**Coverage.** 36.5% global line, 21% global branch. Per-changed-file in `coverage.json`. Notable: `promoteGroups.cs` and `enrichResponse.cs` at **0%**, `TypeConverter.cs` at 50.4%, `Debug/this.cs` at 40.3% (943-line file).

**Findings (12 total) in `result.md` and `../test-report.json`.** Headlines:

1. **CRITICAL — `BuildingGuard` regression (8 C# tests).** The tests assert "Builder actions return error when `Building.IsEnabled == false`". The guard existed in `runtime2` (helper at `DefaultBuilderProvider.cs:18-23` plus 8 call sites) and was deleted by the v2 builder squash 50351d8b. The test file is byte-identical to runtime2; production lost the guard. Codeanalyzer's 4 rounds reviewed code-only and never ran tests, so this slipped through.
2. **MAJOR — `BuilderValidateValid` flood of conversion errors.** ~80 errors of the form `'Cannot convert int = 1 (String) to Int32'` — the `@known` annotation strings reach `Convert.ChangeType` instead of being unwrapped first.
3. **MAJOR — Loop/Signing/Event/Identity test clusters.** 25 PLang test fails surface real production regressions: `Loop` returning `"0 + 1 + 1 + 1"` instead of `3` (string concat instead of arithmetic), `'timeout.after.after'` action lookup, `IdentityUnarchive` leaving literal `"%__data__"` unresolved.
4. **CONFIRMED v4 carryover #1.** Locale-format asymmetry has zero test coverage. `App.Culture` is set to InvariantCulture but **never read by any production code** (verified by grep across `PLang/App/`).
5. **CONFIRMED v4 carryover #2.** `promoteGroups.cs` and `enrichResponse.cs` both at 0% — neither is exercised by the test suite. `enrichResponse` is on the build pipeline's hot path but only at build time.
6. **Missing coverage on the three coder-handover gaps.** AsDefault is OK (96.6%), but ResolveVariables (Gap 2) has zero tests anywhere, and the single→list auto-wrap (Gap 3) has zero tests despite `TypeConverter.cs:156-168` being its dedicated path.
7. **Weak assertions.** Several `Success.IsFalse()` without `Error.Key` checks — most painfully in retry-count tests where the assertion would still pass if the retry loop never executed.

**Verdict: `needs-fixes`.** Not because tests are bad — most are honest — but because they expose a production regression and three test-coverage gaps the coder still owes.

## Code example — the regression that 4 code reviews missed

The shape that should have triggered codeanalyzer to flag it:

```csharp
// runtime2 baseline (PLang/App/modules/builder/providers/DefaultBuilderProvider.cs)
private static Data.@this? BuildingGuard(IContext action)
{
    if (!action.Context.App.Building.IsEnabled)
        return Data.@this.FromError(new Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));
    return null;
}

public Task<Data.@this> Actions(GetActions action)
{
    var guard = BuildingGuard(action);            // ← deleted on this branch
    if (guard != null) return Task.FromResult(guard);
    return Task.FromResult(Data.@this.Ok(action.Context.App.Modules.Describe()));
}

// runtime2-builder-bootstrap (current)
public Task<Data.@this> Actions(GetActions action)
{
    return Task.FromResult(Data.@this.Ok(action.Context.App.Modules.Describe()));
    //  ← guard gone, no replacement layer
}
```

The test:

```csharp
[Test]
public async Task GetActions_BuildingDisabled_ReturnsError()
{
    var action = new GetActions { Context = _app.Context };
    var result = await _app.RunAction(action, _app.Context);
    await Assert.That(result.Success).IsFalse();                     // FAIL: returns true
    await Assert.That(result.Error!.Message).Contains("Building is not enabled");
}
```

The lesson for the next codeanalyzer round: when reviewing a squash that deletes a helper, grep the test files for assertions on the helper's behavior. `Building is not enabled` was a literal in tests that should have surfaced as orphaned during code review.

## Next step

Send back to coder with priority list in `result.md`. After fixes, re-run me before security/auditor.
