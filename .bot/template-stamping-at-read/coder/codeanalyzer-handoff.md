# Coder → Codeanalyzer handoff — branch `template-stamping-at-read`

Forked from `compare-redesign`. Two bodies of work landed: (A) a test-execution-path
migration, and (B) a small set of production changes that fell out of `%Now%` navigation
questions. **B is where the OBP scrutiny belongs** — A is large but mechanical.

All six C# suites green at handoff (Data, Modules, Types, Wire, Generator, Runtime →
`failed: 0`). PlangConsole builds clean. One pre-existing flaky test noted below.

---

## B. Production changes — review these closely

### B1. `datetime.@this` gained navigable members
`PLang/app/type/datetime/this.cs`
- Added scalar parts: `Millisecond`, `Ticks` (long), `DayOfYear`, `DayOfWeek`.
- Added compound parts that carry **their own plang type**:
  `Date → date.@this`, `TimeOfDay → time.@this`, `Offset → duration.@this`.
- Why: `%Now.Ticks%` / `%Now.Date.Year%` resolve by reflecting `datetime.@this`'s members
  (`Data.GetChildValue`). The type only exposed `Year…Second`, so `%Now.Ticks%` returned
  NotFound. The type is the owner of its navigable surface — members live on it, mirroring
  the existing `Year…Second`.
- **Look at:** the compound parts return freshly-constructed item instances per access.
  That's intended (they're projections of an immutable value), but confirm it's not a smell
  in your read. No `[Out]`, so they don't ride the wire (serialization is explicit
  `Write(w.DateTimeOffset)`).

### B2. `Data.Clr<T>(fallback)` lifted onto the base `Data`
`PLang/app/data/this.cs`
- The typed `.NET-edge read` (`await Value()` + `item.Clr<T>()` + fallback) lived **only on
  the generic `Data<T>`** (line ~1035, the `@this<T>` class). Its body uses nothing
  `T`-specific. Moved it to base `Data` so `Variable.Get(name)` (returns base `Data`) can use
  the typed door.
- **Why it matters:** this was the blocker that forced production to use the untyped
  `Variables.GetValue` + cast. `Data<T>` inherits the method now; behavior identical.

### B3. Production reads system vars via the typed door (untyped `GetValue` retired)
`PLang/app/this.cs:518`, `module/identity/code/Default.cs:304`, `module/signing/code/Ed25519.cs:40,41,75`
```csharp
// before                                       // after
(DateTimeOffset)(await ...GetValue("NowUtc"))!  await (await ...Get("NowUtc")).Clr<DateTimeOffset>(default)
(await ...GetValue("GUID"))!.ToString()!        (await (await ...Get("GUID")).Clr<Guid>(default)).ToString()
(await ...GetValue("goalFile")) as string       await (await ...Get("goalFile")).Clr<string?>(null)
```
- Signing/verify (`Ed25519.cs:75`) had a deliberate `unset → UtcNow` fallback — preserved as
  `Clr<DateTimeOffset>(DateTimeOffset.UtcNow)`. **Security-critical path** — Wire + Modules
  signing/identity suites pass, but worth your eyes on the fallback semantics.

### B4. `Variables.GetValue` removed from runtime
`PLang/app/variable/list/this.cs`
- Had **zero production callers** after B3; it was runtime code kept alive only for ~103 test
  call sites. Deleted. The convenience moved to a test-side extension with the same signature:
  `PLang.Tests/Shared/VariablesTestExtensions.cs` (uses internal `Clr` via existing
  `InternalsVisibleTo`). All 103 test sites compile unchanged.

### B5. Latent bug fixed — bracket-index resolution
`PLang/app/variable/list/this.cs` `ResolveVariablesInPath`
- The one internal `GetValue` caller (sync `Regex.Replace` callback for `addresses[idx]`) was
  calling the **async** `GetValue` **un-awaited** — interpolating a raw `ValueTask` into the
  path string (`resolved != null` always true). Now does a sync root lookup (call-frame wins
  over actor store, mirroring `Get`'s precedence) + `.Peek()` (the existing sync door).
- **Look at:** I considered a `PeekValue` helper first — rejected as a redundant verb+noun
  wrapper over `Peek`. Inlined instead. Confirm the inline lookup duplicating `Get`'s
  frame/store precedence is acceptable (vs. a shared sync primitive) — that's the one spot I'd
  flag as a possible future extraction.

---

## A. Test-execution-path migration (mechanical, large)

**Problem:** tests that hand-built a `Goal`/`Step`/`PrAction` and ran it via `RunGoalAsync`/
`steps.RunAsync` **bypassed the real read** — params came back unstamped/untyped, unlike a
`.pr` off disk.

**Helpers added** (`PLang.Tests/Shared/`):
- `Make.Goal/Step/Action/Param` — concise construction; params born-typed from value.
  Wire-parity overloads: `Param(…, kind, strict)`, `WithDefaults(…)`, `Goal(name, path, …)`,
  `Modified(inner, …modifiers)`, `Step(text, indent, …)`.
- `RealGoalLoad.ViaChannel(app, goal)` — serialize via `builder.PrWrite` → MemoryStream →
  stream channel (mime `application/plang-goal`) → goal reader. Loads exactly like a `.pr`.
- `TestApp.Create` used throughout (in-memory settings store) instead of `new app`.

**Migrated** (real read path): Runtime/Testing cluster (ConditionIfBranchIndex,
OrchestrateBranchCoverage, DebugSmoke, AfterActionPayload, PlangRuntime, EventHandler,
IfHandler), Data/Core (EngineTests, StartGoalTests incl. Defaults, PrPipeline path tests),
Modules (Foreach*, StepsSubStep, IfErrorOrchestration, FileHandler).

**Deliberately NOT migrated** (documented, not oversight):
- `TestAction.Create().RunAsync()` single-action isolation tests — `TestAction` already stamps;
  different axis from the hand-built-goal trap.
- Wire snapshot/resume/callback + Generator source-gen tests — the hand-built object *is* the
  thing under test.
- `SetupTests` — `Goal.Setup.RunAsync` with noop/param-less actions; zero born-typeable params,
  so migration is pure churn.

**New tests:** `NowVariableTests.cs` (Runtime) — covers `%Now%` value + `.Ticks`/calendar parts +
compound-part plang types + chained navigation.

---

## Verified / known state
- All six C# suites `failed: 0` after the final push.
- **Pre-existing flaky:** `Data` → `Diff_DiffModeOverLargeListDoesNotOom` (CallStack diff-mode
  memory assertion). Red on baseline, environmental (passed on some runs). No production code on
  this branch touches CallStack diff — not a regression here.
- PLang `--test` suite: not re-run this session (changes are C#-side + test-helper); worth a pass
  if you want end-to-end confirmation.

## Stale-doc cleanups I did NOT do (flagged for docs, not blocking)
- CLAUDE.md still says lazy params use `GetParameter(name).As<T>(Context)` — `As<T>` is gone from
  production; the generator emits `await __d.Value<T>()`.
- Two comments in `PLang/app/data/this.cs` (~lines 64,158) still mention `As<T>`.
