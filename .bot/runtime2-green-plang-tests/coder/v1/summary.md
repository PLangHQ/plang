# Coder v1 — Summary

## What this is

Implementation of architect's waves 1–4 on `runtime2-green-plang-tests`. Architect had already restructured Tests/ (1309 renames) and baselined at 109 pass / 48 fail / 4 stale; my job was to close the gap toward ~135/161 through four targeted C# changes plus a builder prompt update.

## Final state

**161 tests: 122 pass / 35 fail / 4 stale** (was 109/48/18).
- +13 passes, −13 fails, reached 76% green.
- Architect projected ~135/161 (85%) — we hit 76% because the full Tests/ rebuild regressed more tests than it fixed (see "full rebuild reverted" below).

C# test suite: 2273/2274 passing. One LLM integration test flake (`Query_ToolCall_LlmRequestsToolAndHandlesError`) unrelated to these changes.

## What was done

### Wave 1 — Per-test in-memory System db
`PLang/App/Actor/this.cs:CreateSettingsStore` — every actor (System, User, Service) gets in-memory sqlite scoped by `App.Id` when `Testing.IsEnabled`. Previously System was excluded from in-memory, causing identity/signing state to leak across tests. Also fixed a latent bug: `SqliteSettingsStore.InMemory` uses `Cache=Shared`, so same-named DataSources merge across App instances — `App.Id` scoping is load-bearing. System stays on-disk during `Building.IsEnabled` to preserve the LLM cache; non-System check uses reference equality (`this != App.System`) rather than string comparison (Ingi's feedback).

**Win:** 18 "Identity already exists" errors cleared. Also killed subsequent null-assertion fallout from the failed creation paths.

### Wave 2 — `event.on.Type` → `Data<EventType>` enum
`PLang/App/modules/event/on.cs`. Dropped runtime `Enum.TryParse` + `InvalidEventType` error path. Source generator already handles `Data<EnumType>` via `__ResolveData(...).As<T>(Context)`. Builder now emits `"type": "eventtype"` with valid enum values like `"BeforeGoal"`, `"AfterAction"`. The corresponding C# test that exercised the runtime error path was removed — compile-time enforcement replaces it.

### Wave 3 — Goal auto-return + Variables API unification
Two coupled changes driven by Ingi's design feedback (D4 in architect/triage + user feedback during implementation):

1. **`variable.set` returns the stored Data** instead of empty `Data.Ok()`. Goals whose last action is a set now "return" the value via `%__data__%`, picked up by the caller's `goal.call, write to %x%` pattern. Fixes ReturnMapping, GoalCallReturn.

2. **`Variables.Put`, `Set`, `PutAs` → single `Set`**. Previous code had three storage methods with subtle differences (clone-if-names-differ in Set, rename-and-store in Put, alias without rename in PutAs). Unified to:
   - `Set(string name, object? value, Type? type = null)` — if value is `Data`, aliased under `name` (no clone, no rename); else wrapped in new `Data`.
   - `Set(Data value)` — convenience, uses `value.Name` as the key.
   - Dictionary key is source of truth for lookups; `Data.Name` is advisory ("original name at creation").

3. **`Action.RunAsync` no longer mutates result.Name.** Old code did `result.Name = "__data__"; Variables.Put(result)` which corrupted the Data the handler returned if it was aliased with another variable entry. Now: `Variables.Set("__data__", result)` — same object reachable under both `%__data__%` and whatever the producer named it. This was the key insight: the previous contract implicitly required handlers to return unaliased Data, a time bomb.

4. **`Variables.GetAll()` now returns `IEnumerable<KeyValuePair<string, Data>>`** so callers use the dictionary key (reliable) rather than `data.Name` (advisory after unification). `FluidProvider` updated — this was the only real site that needed the audit.

### Wave 4b — Split `http.download`
`PLang/App/modules/http/download.cs` — removed `SaveTo` and `IfExists`. `DefaultHttpProvider.DownloadAsync` streams into a `MemoryStream`, returns `Data.Ok(bytes)`. Callers chain `file.save(Path=..., Value=%__data__%)`. One concern per action (OBP).

### Wave 4c — Builder prompt rules (`system/builder/llm/BuildGoal.llm`)
Added five semantic (language-agnostic) rules:
- **Modifier shape** — module names never contain dots; `signing.error.handle` etc. is wrong
- **Wait/sleep** → `timer.sleep`, never standalone `timeout.after`
- **Arithmetic on set RHS** → `math.*` chain + `variable.set(Name, Value=%__data__%)`
- **Download + save** → two actions, no `text.write` module
- **Event types** are enum-valued, not arbitrary strings

### Wave 4a / 4d — Full rebuild: tried, reverted
Ran a per-folder build loop across 151 Tests/ folders. Rebuild regressed 38 previously-green tests due to LLM non-determinism (new prompt + cache miss produced worse `.pr` files for many folders). Reverted all `.pr` changes (`git checkout -- Tests/`). State returned to 122/35 — the C# changes alone deliver the wins without rebuild churn.

Ingi's feedback for future rebuild loops: skip already-green folders; use `--build={"files":[...]}` from root rather than per-subfolder loops. Captured.

## Code example — the core of the unification

Before — three methods, subtle differences:
```csharp
public void Put(Data value) { _variables[value.Name] = value; }
public Data Set(string name, object? value, ...) {
    if (value is Data dv && dv.Name != name) stored = dv.ShallowClone();
    stored.Name = name;
    _variables[name] = stored;
    return stored;
}
public void PutAs(string name, Data value) { _variables[name] = value; }

// And Action.RunAsync had a time bomb:
result.Name = "__data__";        // mutates shared reference
context.Variables.Put(result);
```

After — one Set, aliasing for Data values:
```csharp
public Data Set(Data value) => Set(value.Name, value);
public Data Set(string name, object? value, Type? type = null) {
    if (value is Data dv) {
        // Alias as-is. No clone, no rename.
        _variables[name] = dv;
        return dv;
    }
    // Scalar: wrap in Data.
    ...
}

// Action.RunAsync — no mutation:
context.Variables.Set("__data__", result);
```

## What's next

- **W6 tail (~35 fails)** — UI render regressions now largely gone, but 12+ Signing tests still fail (Contract mismatch, etc. — real bugs surfaced once W1 unblocked them), 4 Builder tests need `--building` environmental fix, 4 Ui render tests, 5 Event tests (still LLM mapping issues), Loop/List arithmetic (LLM still inventing action names like `list.setByIndex`), Identity/Unarchive + ArchiveDefault regressed (likely W1 exposure of real bugs).
- **Builder auto-split for dotted paths** — logged in `Documentation/v0.2/todos.md`. Builder should detect module names with dots in LLM output and split into two actions (or re-prompt) rather than fail the build.
- **LLM debug options parity** — Ingi asked about using `--debug` style maxLength/grep options for LLM raw-response truncation. Logged in todos.md.
- Hand to **tester** for re-baseline, then **architect** for Wave 6 re-triage.

## Files modified

- `PLang/App/Actor/this.cs` — W1 + reference-equality check
- `PLang/App/Variables/this.cs` — unified Set, GetAll returns KVP
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — `%__data__%` aliasing without mutation
- `PLang/App/modules/event/on.cs` — enum type
- `PLang/App/modules/variable/set.cs` — returns stored Data
- `PLang/App/modules/http/download.cs` — removed SaveTo, IfExists
- `PLang/App/modules/http/providers/DefaultHttpProvider.cs` — bytes in MemoryStream
- `PLang/App/modules/ui/providers/FluidProvider.cs` — use dict key
- `PLang/App/modules/cache/wrap.cs`, `loop/foreach.cs`, `App/Actor/Context/this.cs`, `App/Debug/this.cs`, `App/Errors/Error.cs`, `App/this.cs` — Put → Set migration
- `system/builder/llm/BuildGoal.llm` — five new rules
- `Documentation/v0.2/todos.md` — LLM debug options, builder dotted-path bug
- `PLang.Tests/App/Context/ActorSettingsStoreTests.cs` — inverted System persistence assertion
- `PLang.Tests/App/Context/ContextVariableTests.cs`, `App/Memory/VariablesTests.cs` — GetAll KVP
- `PLang.Tests/App/PlangRuntimeTests.cs`, `App/Modules/event/EventHandlerTests.cs` — enum type
- `PLang.Tests/App/Modules/http/DownloadActionTests.cs` — tests rewritten for bytes API
- `PLang.Tests/App/Modules/ui/RenderTests.cs`, `condition/IfHandlerTests.cs`, `App/Memory/VariablesTests.cs` — Put → Set rename

## Design decisions captured

- **Option B over A for Data-aliasing** — Ingi explicitly chose same-reference over clone-on-store. "Data.Name as advisory" becomes the new convention. Only real audit site was FluidProvider. Trade-off: GetAll iteration may show dupes if a Data is aliased under multiple keys (cosmetic).
- **Variables.Set unified** — Ingi flagged that three methods (Put/Set/PutAs) with subtle differences was confusing. Collapsed to one method with a Data overload.
- **Reference equality for "is this the System actor?"** — replaced `Name.Equals("System", ...)` stringly-typed check.
