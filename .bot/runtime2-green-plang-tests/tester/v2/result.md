# Tester v2 — Findings

Review of coder v1 (commits `ce0de138` + `0cbbeb1f`), scope Waves 1–4 on `runtime2-green-plang-tests`.

## Headline numbers

| Suite | Total | Pass | Fail | Stale/Skipped |
|---|---|---|---|---|
| C# (TUnit) | 2274 | 2273 | 1 | 0 |
| PLang `.test.goal` | 168 | 128 | 35 | 5 |

- C# flake: `Query_ToolCall_LlmRequestsToolAndHandlesError` — pre-existing, not introduced by coder v1.
- **Zero regressions** from my v1 baseline (109 pass). 14 tests flipped `fail → pass`, 4 tests flipped `stale → pass`. Net +18 wins.
- 6 new tests (all pass) under `tests/modifiers/` — from an earlier branch commit `250d3878`, not coder v1.
- Coder claim was `122 pass / 35 fail / 4 stale`; actual is 128/35/5. Delta due to +6 modifier tests + 1 stale in a `.bot/` subtree that shouldn't have been discovered by the runner.

## Wave-by-wave quality review

### Wave 1 — per-test in-memory System db ✅ STRONG

Coverage: Actor/this.cs 89.4%. CreateSettingsStore (lines 110–126) fully covered. Uncovered lines 61/72–75/131 are in unrelated surface (Resolve, EscalationLevel, Cancel).

`PLang.Tests/App/Context/ActorSettingsStoreTests.cs` has 4 tests covering the Testing/Building × System/User matrix:
- `SystemActor_DuringTesting_IsolatedPerEngineInstance` — assertion-flipped from the old shared-cache expectation to "engine2 sees null". Deletion test: remove `-{App.Id}` from `SqliteSettingsStore.InMemory(...)` on line 116, or remove the whole `Testing` branch → test fails. ✅
- `SystemActor_DuringBuilding_PersistsCacheAcrossEngineInstances` — guards the "System stays on-disk during Building" path. Deletion test: if the `this != App.System` guard on line 120 is removed, System goes in-memory during Building → test fails. ✅
- `UserActor_DuringBuilding_DoesNotPersistAcrossEngineInstances` + `UserActor_DuringTesting_DoesNotPersistAcrossEngineInstances` — User-side symmetry; implicitly guard App.Id scoping by demonstrating two engine instances never share User data even though both name is `user`.

Minor note — the reference-equality check `this != App.System` (replacing the old `Name.Equals("System", ...)`) isn't guarded by a test that could distinguish them. Not a blocker.

### Wave 2 — `event.on.Type` → `Data<EventType>` ⚠️ PARTIAL GAP

Coverage: on.cs 100%. 

All 14 Event tests in `EventHandlerTests.cs` updated from `string` to `EventType` enum values. Registration-level coverage is intact. `On_InvalidType_ReturnsError` was removed — coder justified with "compile-time enforcement replaces it".

**F2-1 (minor)** — Compile-time claim holds only for direct C# instantiation (`new On { Type = EventType.BeforeGoal }`). In the production path (LLM → .pr → deserialize → `__ResolveData("type").As<EventType>(Context)`), enum resolution happens at RUNTIME via `TypeMapping.TryConvertTo` (see `PLang/App/Utils/TypeMapping.cs:499–508`, which uses `Enum.TryParse` and returns `EnumParseFailed` ServiceError). No C# test exercises this path for `event.on.Type`. Removing the `On_InvalidType_ReturnsError` test removed the only guard against the LLM-invents-bad-string case. The prompt rule in `BuildGoal.llm` guides the LLM but is itself untested.

Suggestion: re-add a test that round-trips a .pr with `"type": "NonExistentType"` through `__ResolveData`, asserting `Error.Key == "EnumParseFailed"`. Or: accept the gap as "handled one layer up in the type system" and document the reliance on `TypeMapping.TryConvertTo`.

### Wave 3 — Variables unification + variable.set return + Action.RunAsync no-mutation 🔴 WEAK, MULTIPLE GAPS

This is the riskiest wave per my plan. Three semantic changes, all load-bearing, with **three major gaps** in C# test coverage.

Coverage: Variables/this.cs 91.1%, variable/set.cs 96.6%, Action/this.cs 87.3%. Line coverage is misleading — the NEW contracts are what matters, and they're under-asserted.

**F3-1 (major) — `variable.set` return-value contract has no C# test.**

`SetTests.Set_ReturnsOk` at `PLang.Tests/App/Modules/variable/settests.cs:39–48` asserts:
```csharp
var result = await _app.Run(action, context);
await Assert.That(result.Success).IsTrue();
await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
```
It never asserts `result.Value == "testValue"`. Revert `variable.set` to return empty `Data()` → this test still passes. The W3 contract change is only observable via two PLang integration tests (ReturnMapping.test.goal, GoalCallReturn.test.goal).

Suggestion:
```csharp
await Assert.That(result.Value).IsEqualTo("testValue");
```

Same pattern for `Set_AsDefault_DoesNotOverwriteExisting` (line 62–76): the test should assert `result.Value == "original"` — currently it only checks the stored variable.

**F3-2 (major) — `Action.RunAsync` no-rename-mutation is uncovered.**

Coder's own v1 summary calls this "the key insight": old code `result.Name = "__data__"; Variables.Put(result)` mutated the Data reference, corrupting aliased producers. New code at `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:113` is `context.Variables.Set("__data__", result)` which aliases without renaming.

Grep for `result.Name` + `__data__` in `PLang.Tests/` returns zero assertions that verify the non-mutation contract. A future refactor reintroducing `result.Name = "__data__"` goes silent.

Deletion test: reintroduce the assignment before the Set call → all C# tests still pass.

Suggestion — add to an existing action-execution test (e.g., `CacheWrapTests` or a new one):
```csharp
var producer = new TestAction { ReturnValue = new Data("originalName", 42) };
await producer.RunAsync(Ctx);
var stored = Ctx.Variables.Get("__data__");
await Assert.That(stored.Name).IsEqualTo("originalName");  // not "__data__"
await Assert.That(ReferenceEquals(stored, producer.LastReturned)).IsTrue();
```

**F3-3 (major) — Variables.Set aliasing-without-clone is uncovered.**

New behavior per the docstring at `PLang/App/Variables/this.cs:46–52`: "Data value → aliased under `name` as-is. No clone, no rename."

The old `Set(string, object)` cloned if names differed (old code: `if (!string.Equals(dv.Name, name, ...)) stored = dv.ShallowClone(); stored.Name = name;`). The new code aliases the same reference.

Grep for `ReferenceEquals`, `alias`, `no clone`, `no rename`, `differ.*name` in `VariablesTests.cs` returns zero matches. Revert the implementation to the old clone-if-name-differs branch → all C# tests still pass.

The only `ReferenceEquals` check in VariablesTests.cs is at line 1024 (`IsTrue`, on a dot-path value) and line 1111 (`IsFalse`, on something else). Neither exercises the new aliasing semantic.

Suggestion — add:
```csharp
[Test]
public async Task Set_DataWithDifferentName_StoresByKeyAsAlias()
{
    var stack = new Variables();
    var original = new Data("originalName", 42);
    
    stack.Set("alias", original);
    
    var retrieved = stack.Get("alias");
    await Assert.That(ReferenceEquals(retrieved, original)).IsTrue();
    await Assert.That(retrieved.Name).IsEqualTo("originalName");  // not mutated to "alias"
}
```

**F3-4 (minor) — FluidProvider's kvp.Key usage is uncovered.**

`PLang/App/modules/ui/providers/FluidProvider.cs:87` changed from `data.Name` to `kvp.Key`. The comment says: "Use dictionary key as the Fluid variable name — Data.Name is advisory and may differ."

All existing `RenderTests.Render_*` tests only call `Set(new Data("x", ...))` where the key and `Data.Name` are equal. Revert line 87 back to `kvp.Value.Name` → all tests pass.

Suggestion:
```csharp
ctx.Variables.Set("display", new Data("internal", "World"));
var action = new Render { Context = ctx, Template = "Hello {{ display }}", IsFile = false };
var result = await _provider.Render(action);
await Assert.That((string)result.Value).IsEqualTo("Hello World");
```

### Wave 4b — http.download split ✅ STRONG

Coverage: download.cs 100% (2/2 lines; nearly empty after SaveTo removal). DefaultHttpProvider.cs 84.9% — pre-existing gaps in streaming/signed paths.

`DownloadActionTests.cs` reduced from 7 tests to 2:
- `Download_HappyPath_ReturnsBytes` — asserts `result.Value as byte[]` is not null AND content decodes to "downloaded data". Strong guard for the bytes contract.
- `Download_404_ReturnsHttpError` — asserts `Error.Key == "HttpError"` AND `Error.StatusCode == 404`. Strong.

4 tests correctly deleted (FileExistsError, FileExistsSkip, FileExistsOverwrite, CreatesParentDirectories) — they tested the responsibility that moved out of download.cs into the caller's `file.save`.

**F4b-1 (minor, pre-existing)** — No coverage for `MaxDownloadSize` limit path, `OnProgress` callback invocation, or signed download. Not introduced by W4b, not a blocker for this review.

### Wave 4c — Builder prompt rules 🔴 DORMANT

**F4c-1 (critical, surfaced by coder)** — The five new prompt rules in `system/builder/llm/BuildGoal.llm` (arithmetic-on-set-RHS, download+save, wait/sleep, modifier shape, enum event types) are code-landed but have **zero observable effect on the test suite**.

Coder's own v1 summary at `.bot/.../coder/v1/summary.md`:
> "Rebuild regressed 38 previously-green tests ... Reverted all `.pr` changes (`git checkout -- Tests/`). State returned to 122/35."

Evidence: the tests these rules were intended to unblock STILL FAIL:
- `Loop.test.goal` (arithmetic on set RHS: `"0 + 1 + 1 + 1"` string) — fail
- `ForeachDictionary.test.goal` (same pattern) — fail
- `ConditionCompound.test.goal`, `ConditionCompoundAnd.test.goal` — fail
- `SigningExpired.test.goal`, `SigningTimedOut.test.goal` (modifier dotted-path) — fail

The `+13 pass` win from coder v1 comes entirely from the C# changes (W1 + W3 return-value), NOT from the prompt rules. Current `.pr` files in the tree still encode the pre-W4 patterns that the rules were written to prevent.

This is a **known limitation** coder flagged in the handoff — not a secret bug. But "Wave 4 done" overstates the state. The prompt rules require a targeted rebuild loop to take effect, and that rebuild will introduce LLM non-determinism that needs human scrutiny of each resulting `.pr`.

Suggestion — coder or architect's call, not tester's:
- (a) Accept W4c as landed-but-dormant; next step is a surgical per-goal rebuild with hand review.
- (b) Add PLang-level pipeline tests: compile specific .goal fixtures under the new prompt and assert the resulting .pr structure matches the expected action list. This would be the only way to make prompt rules self-testing without full Tests/ churn.

---

## Coverage summary (modified files only)

| File | Pct | Cov/Total | Notes |
|---|---|---|---|
| App/Actor/this.cs | 89.4% | 101/113 | W1 core (CreateSettingsStore) fully covered; uncovered lines unrelated |
| App/Variables/this.cs | 91.1% | 740/812 | Uncovered 135,179–187 = dot-path NotFound + IList typed conv (pre-existing) |
| App/Goals/.../Action/this.cs | 87.3% | 110/126 | Uncovered 16–23 = header/regions; `%__data__%` Set path covered |
| App/modules/event/on.cs | 100% | 34/34 | |
| App/modules/variable/set.cs | 96.6% | 56/58 | Uncovered 33 = type-convert error message (pre-existing) |
| App/modules/http/download.cs | 100% | 2/2 | (Handler is a partial class; body largely in DefaultHttpProvider) |
| App/modules/http/providers/DefaultHttpProvider.cs | 84.9% | 1286/1514 | Uncovered 149–152, 204–207 — pre-existing stream/signed paths |
| App/modules/ui/providers/FluidProvider.cs | 87.2% | 280/321 | Uncovered include layout/loader branches — pre-existing |
| App/Actor/Context/this.cs | 85.1% | 372/437 | Pre-existing gaps |
| App/modules/cache/wrap.cs | 100% | 60/60 | |
| App/modules/loop/foreach.cs | 97.5% | 78/80 | |
| App/this.cs | 67.6% | 309/457 | Pre-existing gaps |

Raw data: `v2/coverage.json`.

---

## Findings summary

| ID | Severity | Wave | Type | Summary |
|---|---|---|---|---|
| F3-1 | major | W3 | weak-assertion | `variable.set` return value untested in C# |
| F3-2 | major | W3 | missing-coverage | `Action.RunAsync` no-mutation contract untested |
| F3-3 | major | W3 | missing-coverage | Variables.Set aliasing-without-clone untested |
| F4c-1 | critical | W4c | missing-coverage | Builder prompt rules are dormant; not observable by suite |
| F2-1 | minor | W2 | missing-coverage | Enum resolution failure path untested post removal |
| F3-4 | minor | W3 | weak-assertion | FluidProvider kvp.Key vs data.Name untested |
| F4b-1 | minor | W4b | missing-coverage | No MaxDownloadSize / OnProgress / signed download (pre-existing) |

## Verdict: needs-fixes

Hand back to **coder**, not security. The three major W3 gaps (F3-1, F3-2, F3-3) are the priority. F4c-1 is coder-or-architect's call — it's a known-to-coder gap, I'm just codifying that the "Wave 4 done" claim overstates it.

Once W3 gaps are closed, suite still has 35 failing PLang tests but those are Wave 6 territory (architect's next triage after the dormant W4c is resolved).
