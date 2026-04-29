# Coder v1 — Plan: Implement Architect Waves 1–4

## Goal

Implement the 4 dispatch waves architect v1 specified in `architect/v1/triage.md`, projected to take the suite from 109/161 green → ~135/161 (85%).

Order, per architect: W1 → W2 → W3 → W4. One commit per wave (or bundled if clean). After W4, full rebuild + hand to tester for re-baseline.

---

## Pre-work: two flagged items (must resolve before coding)

The architect explicitly told me to "stop and ask Ingi" on these. I have diagnosed them while scouting; here's what I found and the option I'd pick. **Please confirm before I start W3/W4.**

### Q1 (blocks Wave 3) — What should auto-return actually do?

The runtime already returns the last step's last-action result from `Steps.RunAsync` (`PLang/App/Goals/Goal/Steps/this.cs:130`). So D4 as literally written is already in place.

The real bug (ReturnMapping.test.goal, ComputeValue.goal):

```
ComputeValue
- set %answer% = 42
```
`variable.set` handler (`PLang/App/modules/variable/set.cs:57`) returns `Data()` — a successful-but-empty `Data` with no `Value`. That becomes `%__data__%`. The caller writes empty to `%result%`. Assertion "is not null" fails.

So either D4 means something other than what it literally says, or the fix touches `variable.set`.

**Three options:**

| | Change | Effect |
|---|---|---|
| **A** | `variable.set` returns the `Value` it set (instead of empty `Data`) | `%result%` gets `42`. Any `variable.set` step now "returns" its value. Most natural. |
| **B** | `Steps.RunAsync` at exit: if last result has no `Value`, look up `%__data__%` in memory and use that instead | Same problem — `%__data__%` is whatever the last action put there; last action was `variable.set` which put empty. No improvement. |
| **C** | Don't overwrite `%__data__%` when an action returns empty `Data` | `%__data__%` retains last meaningful value. But breaks the clear semantic "each action writes its own `%__data__%`". Fragile. |

**I propose Option A** (change `variable.set` to return its Value). It's small (one line), keeps `Steps.RunAsync` unchanged, and makes the intent explicit: a `set` step "returns" the value it just assigned. Side-effect-only handlers (`output.write`, etc.) continue returning empty which is fine when no caller captures it.

Does option A match what you meant by D4? Or do you want the runtime change (different from B) where `Goal.RunAsync` inspects memory differently?

### Q2 (blocks Wave 4) — D5 `http.download` split — existing app users

`SaveTo` is used in three `.goal` files only (grep confirmed):
- `Tests/Modules/Http/DownloadSkip/DownloadSkip.test.goal`
- `Tests/Modules/Http/DownloadFile/DownloadFile.test.goal`
- `os/apps/Installer/InstallDependencies.goal`

And in C# — `download.cs:21`, `DefaultHttpProvider.cs:144/153/155/195`.

No other app-level goals use it. Tests rebuild after W4; the `os/apps/Installer` goal is already `.pr`-built, so it needs a rebuild too.

**I propose:** Remove `SaveTo` from `download.cs`. `DefaultHttpProvider.DownloadAsync` returns raw bytes in `%__data__%`. Builder prompt rule handles `download X, save to Y` → two actions. The `os/apps/Installer` .pr regenerates via the Wave 4 full rebuild; if rebuild fails for Installer, I'll rewrite its single step.

Any `os/apps/` goals you don't want rebuilt right now? Otherwise I'll include the full tree in the W4 rebuild.

---

## Wave 1 — Per-test in-memory System db

### Problem

Test runner shares one file-backed system store across all tests. Identity/signing state leaks. "Identity 'testSigner' already exists" × 18 tests.

Also a latent bug: even today's `SqliteSettingsStore.InMemory("user")` uses `Cache=Shared` with name = actor name → **all in-memory User/Service stores across apps share the same db**. Not obvious in the current test set, but any test that wrote User-actor state would leak.

### Fix

`PLang/App/Actor/this.cs:110-122` — `CreateSettingsStore`:

1. When `Testing.IsEnabled`: all three actors (System, User, Service) go in-memory. System was previously excluded — flip that.
2. In-memory DataSource name must be **unique per App instance** so each child test gets its own isolated db. Use `App.Id` (a 12-char GUID already on every App) as the scope: `"{actorName}-{App.Id}"`.
3. When `Building.IsEnabled`: System stays on-disk (LLM cache must persist for build); User/Service stay in-memory with the unique-name fix.
4. Neither Testing nor Building: all actors on-disk (existing behavior).

### Code change sketch

```csharp
// Actor/this.cs — CreateSettingsStore()
private ISettingsStore CreateSettingsStore()
{
    // Test mode: every actor gets a fresh in-memory db per App instance.
    // App.Id scopes the DataSource name so parallel/sequential tests don't share dbs
    // through SQLite's shared-cache.
    if (App.Testing.IsEnabled)
        return SqliteSettingsStore.InMemory($"{Name.ToLowerInvariant()}-{App.Id}");

    // Build mode: User/Service in-memory (isolation), System on-disk (LLM cache persists).
    if (App.Building.IsEnabled && !Name.Equals("System", StringComparison.OrdinalIgnoreCase))
        return SqliteSettingsStore.InMemory($"{Name.ToLowerInvariant()}-{App.Id}");

    var dbDir = App.FileSystem.Path.Combine(App.AbsolutePath, ".db");
    var dbPath = App.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
    return new SqliteSettingsStore(dbPath, App.FileSystem);
}
```

### Verify
- Re-run signing + identity tests in `Tests/Modules/Signing/` and `Tests/Modules/Identity/`. Expect 18 "already exists" runtime errors to clear.
- Some of those (Signing/DotNavigation, Signing/NoIdentity) had null-assertion fallout from the creation error — should also clear.
- Run one test twice: state from run 1 must not affect run 2.

No rebuild. Commit.

---

## Wave 2 — `event.on.Type` → `EventType` enum

### Fix

`PLang/App/modules/event/on.cs`:
- Line 22: `public partial Data.@this<string> Type` → `public partial Data.@this<EventType> Type`
- Lines 43–45: delete the `Enum.TryParse` + error block; `Type.Value` is already the enum
- Line 55: `eventType` already typed — adjust so `EventType eventType = Type.Value;`
- Namespace imports: `using App.Events;` may already be imported (line 3).

### Verify
- Rebuild `Tests/Modules/Event/` subtree
- Check the new .pr files emit valid enum values (`BeforeGoal`, `AfterStep`, etc.)
- Run Event/Basic, Event/Priority, Event/Remove — expect green
- Spot-check: does the source generator already handle `Data.@this<EventType>`? The scouting confirmed yes (`LazyParamsGenerator.cs:286-362` handles `isDataWrapped` for any type including enums). If the rebuild fails with a codegen error, I'll stop and surface it.

Rebuild Event/ only. Commit.

---

## Wave 3 — Goal auto-return last `%__data__%`

**Gated on Q1 above.** Assuming Option A (variable.set returns its Value):

### Fix

`PLang/App/modules/variable/set.cs:54-57`:

```csharp
Context.Variables.Set(Name, Value,
    Type?.Value != null ? App.Data.Type.FromName(Type.Value) : null);

return Task.FromResult(Value);  // was: Task.FromResult(Data());
```

This makes `variable.set` return the value it set. Cascading effect: `%__data__%` after any `set` step holds that value; `goal.call` captures it if the called goal's last step was a `set`.

### Verify
- ReturnMapping, StepResult, GoalCallReturn, ActorContext — expect the null-vs-not-null assertions to flip to pass
- Error/Types — asserts `"TestKey"` — needs the set in the error goal to "return"
- No existing passing test should regress. Especially: tests that do `set %x% = foo` followed by assertion on `%x%` — those read `%x%` not `%__data__%`, unchanged.
- Add a focused C# test in `PLang.Tests/App/Modules/variable/SetActionTests.cs` (create if missing) asserting the Run() return carries Value.

No rebuild. Commit.

---

## Wave 4 — http.download split + builder prompt edits

### 4a — List/Math JsonParseError diagnosis (do first)

Before touching anything else, run:
```
cd Tests/Modules/List && plang --debug='{"goal":"BuildGoal","maxLength":0}' build
cd Tests/Modules/Math && plang --debug='{"goal":"BuildGoal","maxLength":0}' build
```
Capture the raw LLM response. If it's markdown-wrapped or oversized, either add a rule to BuildGoal.llm tightening JSON output for long step lists, or (more likely) find the specific step that breaks the LLM and treat it as a test-content issue. Report findings in `v1/list_math_diagnosis.md` before proceeding.

### 4b — Split http.download

`PLang/App/modules/http/download.cs`:
- Remove `SaveTo` and `IfExists` properties (IfExists is only meaningful with SaveTo)
- Update summary comment: "Downloads bytes from a URL. Does not persist — chain with `file.save` to write to disk."
- Return type becomes `Data.@this<byte[]>` (or `Data.@this` if the existing plumbing already works with byte[] in .Value)

`PLang/App/modules/http/providers/DefaultHttpProvider.cs:132-196`:
- Remove SaveTo/IfExists logic (lines 144–159, 183–193)
- Stream response into a `MemoryStream`, then `return Data.@this.Ok(stream.ToArray())`
- Keep progress callback plumbing on the bytes stream
- Keep signing/headers/timeout unchanged

### 4c — Builder prompt edits (`system/builder/llm/BuildGoal.llm`)

Append 4 new rules to the existing rule set (line 157 onward). Rules are semantic (language-agnostic), following the voice of the existing rules (which are already multi-lingual per repo convention). Five rules:

**Rule M1 — Modifier shape** (insert near existing "Action modifiers" rule at line 169):
> Modifier actions (`error.handle`, `cache.wrap`, `timeout.after`) live in the flat action list immediately after the action they wrap — they are not concatenated into the preceding action's `module` path. Module names never contain dots. If a step expresses error handling, caching, or deadline intent on a module like `signing` or `http`, emit two separate actions: `{"module":"signing","action":"sign",...}` then `{"module":"error","action":"handle",...}`. Never `{"module":"signing.error.handle",...}`.

**Rule M2 — Wait/sleep intent** (under Multi-Action Patterns):
> A step expressing pause/delay intent (`wait for N ms`, `sleep N seconds`, in any language) maps to `timer.sleep(Ms=N)`. `timeout.after` is a modifier that wraps another action with a deadline — it never stands alone.

**Rule M3 — Arithmetic on `set` RHS** (under Multi-Action Patterns):
> When a `set %x% = <expr>` step's RHS contains arithmetic (`+`, `-`, `*`, `/`, `%`), emit a `math.*` action chain then a `variable.set(Name=%x%, Value=%__data__%)`. The builder is the place where arithmetic becomes deterministic code — the runtime does not evaluate expression strings. `set %count% = %count% + 1` → two actions: `math.add(A=%count%, B=1)` then `variable.set(Name=%count%, Value=%__data__%)`. Same for phrasings that express accumulation (`add N to %var%`).

**Rule M4 — Download + save** (under Multi-Action Patterns, AFTER 4b lands):
> `http.download` fetches bytes into `%__data__%` — it does not persist to disk. A step expressing download-then-save intent (`download URL, save to PATH`, any language) maps to two actions: `http.download(Url=URL)` then `file.save(Path=PATH, Value=%__data__%)`. There is no `text.write` module.

**Rule M5 — Event types are enum-typed** (in Rules section):
> `event.on.Type` is an enum with a fixed value set shown in the action schema. Choose only from those values (e.g. `BeforeGoal`, `AfterStep`, `OnError` — see the action's type schema). Never invent arbitrary strings.

### 4d — Full Tests/ rebuild

Per-folder build loop over `Tests/**`. Read each generated `.pr` on a sample to verify prompt edits took effect:
- `Tests/Modules/Http/DownloadFile/.build/*.pr` — two actions (http.download + file.save)
- `Tests/Modules/Signing/Expired/.build/*.pr` — no `timeout.after.after`; modifier is separate
- `Tests/Modules/Loop/.build/*.pr` — foreach step emits math.add + variable.set chain
- `Tests/Modules/Event/Basic/.build/*.pr` — valid enum value

Also rebuild `os/apps/Installer/` (the one non-test SaveTo user).

### Verify

- 6 build-failure folders all build.
- Loop arithmetic tests pass (`Loop.test.goal`, `Foreach/Dictionary`).
- Signing Expired/TimedOut/NonceReplay build-failures clear (W1 took care of their runtime-side).
- Http/DownloadFile builds and runs (needs network — may still fail but for a different reason; report back to Ingi).

Commit. Hand to tester for full re-baseline.

---

## Non-goals (explicit)

- **No tests re-authored** (that's tester's job per architect's plan).
- **No .pr hand-edits** (law).
- **No W5 (SetupGoal parked) or W6 (tail) work** — separate passes.
- **No touching the 4 Builder-module tests** (`Building is not enabled` — W6 env fix).
- **No bcrypt** (W6).

## Files I'll touch

- `PLang/App/Actor/this.cs` (W1)
- `PLang/App/modules/event/on.cs` (W2)
- `PLang/App/modules/variable/set.cs` (W3, subject to Q1)
- `PLang/App/modules/http/download.cs` (W4b)
- `PLang/App/modules/http/providers/DefaultHttpProvider.cs` (W4b)
- `system/builder/llm/BuildGoal.llm` (W4c)
- `PLang.Tests/App/Modules/variable/SetActionTests.cs` — add test (W3)
- `.build/*` across Tests/ + `os/apps/Installer/` — builder regenerates (W4d)

## Commits

- `Wave 1: Per-test in-memory System db (App-scoped DataSource names)`
- `Wave 2: event.on.Type → EventType enum`
- `Wave 3: variable.set returns its Value (enables goal auto-return)`
- `Wave 4: Split http.download; builder prompt rules for modifiers, arithmetic, download+save, event enum`
- `Wave 4: Rebuild Tests/ + os/apps/Installer/ under new prompt`

If any commit turns out to be <5 lines of code, I'll fold it into the next. Git history stays clean.

---

## Blocker checklist before coding

- [ ] **Q1 answered** (Option A / something else for D4) — blocks W3
- [ ] **Q2 acknowledged** (rebuild os/apps/Installer included) — blocks W4
- [ ] Otherwise, I start W1 immediately after approval.
