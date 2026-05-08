# Stage 22: `app-shortcuts-drop`

**Read first:**
- `plan/principles.md` — OBP smell about implicit per-actor reaches dressed up as app-level.
- `plan/scope-map.md` — Mixed-cases entry #4: `app.Variables` and `app.Context` shortcuts settled REMOVE 2026-05-08; fragile under parallel multi-Context execution.

**Goal:** Delete the `app.Variables` and `app.Context` shortcuts on `App.this.cs` (the two delegating properties that return "current actor's" Variables/Context). Sweep all callers to use an explicit actor — `app.System.Context`, `app.User.Context`, or `app.System.Context.Variables` — depending on what the call site means.

**Scope:**
- *Included:* delete the two delegating properties on `App.this.cs`; sweep ~88 callers (2 production + ~86 test) to choose the explicit actor per site.
- *Excluded:* anything else. Pure shortcut-removal stage.

**Deliverables:**

### Property deletions

`PLang/App/this.cs` — delete the two delegating properties (today around lines 222–223):

```csharp
// Today (lines 222–223):
public Actor.Context.@this Context => CurrentActor.Context;
public Variables.@this Variables => Context.Variables;

// After: both deleted.
```

`CurrentActor` itself stays — it's used elsewhere internally (e.g., when bootstrapping switches between System and User during App.Start). Only the shortcut properties go.

### Caller sweep

**Production sites (2):**

1. **`PLang/App/Actor/this.cs:137`** — inside the `MyIdentity` DynamicData lambda. Context gets used to call `IIdentityProvider.GetOrCreateDefaultAsync`. The comment says "resolves to the System actor's default identity" — so explicit System:

   ```csharp
   // Today:
   var result = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.Context }).GetAwaiter().GetResult();

   // After:
   var result = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.System.Context }).GetAwaiter().GetResult();
   ```

2. **`PLang/App/Errors/Error.cs:265`** — `var fallbackContext = app.Context;`. Used as a fallback context for error materialization. Errors are a System-level diagnostic concern (Trail is shared across actors per scope-map). Use `app.System.Context`:

   ```csharp
   // Today:
   var fallbackContext = app.Context;

   // After:
   var fallbackContext = app.System.Context;
   ```

**Test sites (~86):**

The dominant test pattern is `engine.Context` and `engine.Variables` — read by tests setting up state for assertions. Each call site needs a per-site judgment of which actor's Context applies. Heuristic:

- **`engine.Context = X`** assignments — replace with the right side: tests previously relied on the shortcut to write into "current actor's" Context. After removal, the test must explicitly pick `engine.User.Context` (the dominant testing actor) or `engine.System.Context`. **Default to `engine.User.Context`** unless the test name or surrounding code suggests System-level concerns.
- **`engine.Variables.Set(...)` / `engine.Variables.Get(...)`** — same pattern. Default to `engine.User.Context.Variables`.
- **`engine.Context.X`** reads — same pattern.

**Quick test classification heuristics:**

| Test directory | Default actor |
|----------------|---------------|
| `PLang.Tests/App/Modules/builder/` | User (build runs as User per stage 12) |
| `PLang.Tests/App/Modules/test/` (Tester actions) | User (Test discovers/runs goals as User) |
| `PLang.Tests/App/Core/PrPipelineTests.cs` | User (the dominant test harness pattern) |
| `PLang.Tests/App/Core/StartGoalTests.cs` | User |
| `PLang.Tests/App/CallStackTests/` | User unless test name says System |
| `PLang.Tests/App/Errors/` | System (errors are diagnostic, system-level) |
| `PLang.Tests/App/SnapshotTests/` | mixed — read each test |

When in doubt, default to **User**. If a test fails after the User pick, it's a hint the test was actually System-flow; switch to System.

### Verification after sweep

- `grep -rn "\bapp\.Variables\b\|\bapp\.Context\b" PLang/ --include='*.cs'` — zero hits.
- `grep -rn "\bengine\.Variables\b\|\bengine\.Context\b" PLang.Tests/ --include='*.cs'` — zero hits.
- All tests pass without the shortcuts.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- The two App-level shortcut properties are deleted.
- Greps above return zero hits.

**Dependencies:** None on stages 14/17/20/21 specifically. Independent.

## Design

### The smell this closes

`app.Context => CurrentActor.Context` and `app.Variables => Context.Variables` are conveniences that hide a per-actor dependency as an app-level reach. From the call site, `app.Context` looks like "the App's Context" — but it actually returns *whichever actor is current* at the AsyncLocal level. Under parallel multi-Context execution (web-pool runtime where the App is rented per request), the shortcut returns whichever Context is currently in flight on the calling thread — fragile and meaning-changing.

The principle in `principles.md` flags this directly: "Implicit per-actor dependencies dressed up as app-level reaches. A class that takes `App` and internally calls `_app.CurrentActor.Variables` is hiding a per-actor dependency. Make it explicit — take Context as a method parameter when the method needs per-actor state."

After this stage, every consumer is explicit about which actor's Context they need. No implicit "current actor" magic.

### Files touched

**Files modified:**
- `PLang/App/this.cs` — 2 properties deleted.
- ~2 production files + ~86 test files swept.

### Risk + dependencies

**Risk: low-medium.** The risk is in the per-site test classification — if a test gets "User" but actually needed "System," it'll fail at runtime with a NotFound or wrong-Context behaviour. The test failure is the safety net.

Possible failure modes:
1. **A test that depends on the AsyncLocal "current actor" behavior** — if the test sets `CurrentActor = X` then reads `app.Context` and expects X's context. After removal, the test must directly say `app.X.Context`. The build doesn't catch this; runtime test failure does.
2. **A grep miss on call sites** — caught by the build.
3. **The 2 production sites picked wrong actor** — both are System-level concerns (identity provider default, error fallback), so System is the right pick. If User turns out to be needed, runtime failure surfaces it.

**Dependencies: none.** Independent of all other Tier 4 stages.

### Tests

**No new tests required.** The sweep is the work.

**Existing test coverage:**
- Every test that uses `engine.Context` or `engine.Variables` exercises this path. Their pass after the sweep is the validation.

### Watch for (coder eyes-on)

- **Closures capturing `app.Context`** — the lambda at Actor/this.cs:137 captures `app.Context` *inside* the lambda body, so the capture happens at evaluation time (each MyIdentity access). After the rename to `app.System.Context`, the same closure pattern works. No closure-capture issues.
- **Tests that mutate `engine.Context.X` expecting it to land on a specific actor** — the implicit-actor magic was probably the reason. Rewrite to explicit actor.
- **The `CurrentActor` property** itself — stays. Only the two delegating properties go. App.Start still flips `CurrentActor` between System and User during boot.
- **Per-call-site choice User vs System** — when in doubt, User. Tests at boot-paths (StartGoalTests, builder mode) likely run-as-User.
- **Compile-time replacement** — most call sites can be done by simple find-replace of `engine.Context` → `engine.User.Context` (within a file), then run tests; flip outliers to System where the pattern fails.

### Stages that follow this one

- Stages 15 (compound-name-rename), 16 (static eviction), 18 (mime-table-split), 19 (Provider→Code) remain.
- Stage 18 may carve as a Tier 4 batch with this — see stage 18's brief.

### Out of scope

- Anything else on App's surface — `CurrentActor`, the actor properties (System, User), etc. all stay.
- Any change to test infrastructure (AppFactory, AppFixture, etc.) beyond updating sites that use the shortcut.

## Commit plan

```
runtime2-cleanup stage 22: drop app.Variables and app.Context shortcuts

App.this.cs had two delegating properties:
  public Actor.Context.@this Context => CurrentActor.Context;
  public Variables.@this Variables => Context.Variables;

Both return "current actor's" state via the AsyncLocal CurrentActor
mechanism. Convenient, but hides a per-actor dependency as an
app-level reach — fragile under parallel multi-Context execution
(web-pool runtime) where the calling thread's CurrentActor isn't
deterministic.

Both deleted. Callers explicit about which actor's Context applies:

Production (2):
  Actor/this.cs:137 (MyIdentity lambda) — System (System owns the
                                          default identity).
  Errors/Error.cs:265 (fallbackContext) — System (errors are
                                          diagnostic, system-level).

Tests (~86) — defaulted to engine.User.Context except where the
test name or surrounding context suggests system-level concerns
(Errors, SnapshotTests where appropriate).

CurrentActor stays. Only the two delegating properties go.
```
