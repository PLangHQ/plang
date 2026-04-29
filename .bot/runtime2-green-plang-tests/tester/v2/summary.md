# Tester v2 — Summary

## What this is

Re-baseline + quality review of coder v1's Waves 1–4 implementation
(commits `ce0de138` + `0cbbeb1f`) on `runtime2-green-plang-tests`. Coder shipped:
- W1: per-test in-memory System sqlite scoped by App.Id.
- W2: `event.on.Type` changed from `Data<string>` to `Data<EventType>`.
- W3: Variables.Put/Set/PutAs unified to a single Set; `variable.set` returns stored
  Data; `Action.RunAsync` publishes to `%__data__%` via aliasing (no result.Name
  mutation); `GetAll()` returns `KeyValuePair<string, Data>`.
- W4b: `http.download` bytes-only (removed SaveTo/IfExists); caller chains `file.save`.
- W4c: Five builder prompt rules in `BuildGoal.llm` — arithmetic-on-set-RHS, download+
  save, wait/sleep, modifier shape, enum event types.

User instruction: "new from coder, we will not code analyzer yet" — go straight to
tester. No codeanalyzer pass for this wave.

## What was done

### 1. Environment
Clean `dotnet build PlangConsole/PLangConsole.csproj` — 0 errors, 921 warnings,
40s. Linux `plang` binary (not `.exe`).

### 2. Suite runs
- **C# (TUnit)**: 2273/2274. Same pre-existing `Query_ToolCall_LlmRequestsToolAndHandlesError`
  flake.
- **PLang**: 168 total — **128 pass / 35 fail / 5 stale**. Coder claimed 122/35/4;
  actual +6 passes from the `tests/modifiers/` folder (pre-existing on branch) and
  +1 stale from a `.bot/runtime2-settings/scaffolder/v1/tests/plang/Start.test.goal`
  leaking into discovery.

### 3. Delta vs tester v1 baseline
- **Zero regressions** from the v1 baseline pass-set of 109.
- 14 tests flipped `fail → pass` (Events, GoalCallReturn, Identity cluster, ReturnMapping,
  Signing/CustomContracts + HeaderMismatch, Start, SystemVariables).
- 4 tests flipped `stale → pass` (ConditionFileExists, ConditionFileNotExists,
  DownloadFile, SigningNonceReplay — these were "no .pr" in v1 but rebuilt since).
- Net +18 wins.

### 4. Coverage (scoped to modified files)
- 14/14 modified files have coverage data. 11/14 above 80%. Critical W1/W3 paths
  in `Actor/this.cs` (CreateSettingsStore) and `Variables/this.cs` are covered at
  the line level. Raw data: `v2/coverage.json`.
- Line coverage is misleading — the gaps are in *what's asserted about NEW semantic
  contracts*, not in coverage %.

### 5. Per-wave quality analysis (the main job)

Full detail in `v2/result.md`. TL;DR:
- **W1** ✅ STRONG. Four tests in `ActorSettingsStoreTests.cs` guard the Testing×
  Building × System × User matrix. Deletion tests pass: if App.Id scoping or the
  Testing branch is removed, tests fail.
- **W2** ⚠️ Minor gap. On_InvalidType_ReturnsError was removed citing compile-time
  enforcement — but the builder-output path (.pr → `__ResolveData.As<EventType>`)
  fails at RUNTIME via TypeMapping.TryConvertTo's `EnumParseFailed`. No test for
  that path.
- **W3** 🔴 Three major gaps. `variable.set` return value not asserted; Action.RunAsync
  no-mutation contract untested; Variables.Set aliasing-without-clone untested.
  Each passes the deletion test — reverting the C# change leaves all C# tests green.
- **W4b** ✅ STRONG. Bytes contract asserted via `Encoding.UTF8.GetString(bytes) ==
  "downloaded data"`. 4 stale `SaveTo`-era tests correctly deleted.
- **W4c** 🔴 Dormant. Coder reverted the full .pr rebuild after it regressed 38
  tests, so the prompt rules are code-present but not exercised. Tests they target
  (Loop, ForeachDictionary, ConditionCompound, SigningExpired/TimedOut) still fail.

### 6. Verdict

**needs-fixes** — route back to **coder**, not security.

- Three major findings (F3-1, F3-2, F3-3) in Wave 3 are the priority.
- F4c-1 (dormant prompt rules) is a coder-or-architect call — the gap is already
  known to coder; I'm codifying that "Wave 4 done" overstates the state.
- Suite remains at 35 failing PLang tests; that's Wave 6 territory (architect's
  next triage).

## Code example — the W3 gap pattern

`PLang.Tests/App/Modules/variable/settests.cs:39` is what F3-1 looks like:

```csharp
[Test]
public async Task Set_ReturnsOk()
{
    var context = _app.Context;
    var action = TestAction.Create("variable", "set", ("name", "%testVar%"), ("value", "testValue"));
    var result = await _app.Run(action, context);

    await Assert.That(result.Success).IsTrue();
    await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
    // MISSING: await Assert.That(result.Value).IsEqualTo("testValue");
}
```

With the missing line present, reverting `variable.set` to return empty `Data()`
would fail the test. Without it, the W3 contract change is a silent no-op at the
C# test level.

## What's next

Hand `test-report.json` to **coder** for v2 (addressing F3-1/2/3). After that,
route to architect for F4c-1 discussion before attempting any .pr rebuild.
Security can run after Wave 3 gaps close.
