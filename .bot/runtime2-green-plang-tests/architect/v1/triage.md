# Phase 4 Triage — v1

Architect's classification of the 48 fails + 6 build-failures + 4 stale items from tester's baseline. Read the preamble first — the design decisions made in the architect+Ingi session determine the wave strategy below. Coder executes waves 1–4 as one dispatch; wave 5 is parked; wave 6 returns after tester re-baselines.

Source data: `tester/v1/baseline.md`, `test-report.json` (findings 1–6).

---

## Design decisions (architect + Ingi, 2026-04-21)

These are the rulings that shape the waves. Coder reads this before touching code.

### D1 — Tests are independent. System db is in-memory, per-test.
Previously the test runner shared one file-backed system store (sqlite on disk). Every test inherited cached state (identities, settings, cache, data) from any prior test. That's the real root of the "Identity 'testSigner' already exists" flood — the test runner, not the handler. The fix: the test runner creates an in-memory sqlite `System` for each `.test.goal`. Lives for the duration of that test's steps; discarded when the test ends; next test starts fresh. This is not a per-bug patch, it's the correct isolation model.

### D2 — Strong typing beats prompt patching.
`event.on.Type` is `Data<string>` today, which is why the LLM invents plausible names (`beforeGoalCall`, `before`). Make it `Data<EventType>` (the existing enum). The source generator exposes the enum values to the builder; the LLM picks a valid one. Matches the repo rule `memory/design_enums_over_strings.md`.

### D3 — No handler magic. Builder is the place where natural language becomes deterministic code.
For `set %x% = %x% + 1`, the correct fix is to instruct the LLM to emit a `math.*` action chain, not to teach the `set` handler to parse arithmetic strings. PLang's law: runtime is deterministic, builder's job is semantic mapping.

### D4 — Goal auto-returns last `%__data__%` when there's no explicit `return`.
Coder implements this as a general runtime behavior. A goal's last-action result (which always lands in `%__data__%`) becomes the goal's return value if no explicit `return` step. Fixes the null-return cluster without 6 individual test-authoring commits, and is a cleaner design. Coder: flag any cases where this would cause surprising side effects (e.g. a side-effect-only last step) during implementation — discuss with Ingi.

### D5 — `http.download` downloads, `file.save` saves. Split the action.
`http.download`'s `SaveTo` parameter is impure — it both fetches and persists. OBP says one responsibility per action. Remove `SaveTo`; `http.download` returns the bytes in `%__data__%`; `file.save(Path=..., Value=%__data__%)` persists them. Builder prompt handles the chain for `download X, save to Y`. This ripples through the provider implementation and any existing goals that used `SaveTo`.

### D6 — Setup.goal is scope-isolated. SetupGoal test is premature.
Setup runs independently on startup; variables it sets do not leak into the app's runtime scope. `Tests/App/SetupGoal/Start.test.goal` assumes the opposite. Setup's full semantics in runtime2 are not yet designed — **park the test**, don't delete it. When Setup gets a formal spec, this test gets rewritten to assert something observable (file, settings entry, identity).

---

## Dispatch waves

Coder executes waves 1–4 in order, one commit per wave (or one large commit if cleaner). No rebuilds needed until wave 4.

### Wave 1 — Per-test in-memory System db

**Goal**: Every `.test.goal` runs with its own fresh, ephemeral sqlite `System` store. No file persistence, no cross-test inheritance.

**Where**: test runner — the code path that `plang --test` uses to set up each test's PLangContext / Engine / Actor.System. Find where `SystemDirectory` / system sqlite is opened when a test runs. Change it to:
- Open an in-memory sqlite connection (`Data Source=:memory:`) scoped to this test invocation.
- Run any schema bootstrap that normally runs on a fresh system db.
- Dispose/close when the test ends (and the next test gets another fresh one).

**Scope note**: This is *system-db* isolation only. Filesystem-level operations (file.save, download to disk) continue to hit the real filesystem. Tests that write to a `downloads/` folder are unaffected — they already handle their own cleanup via explicit `delete file ...` steps, and those steps work.

**Expected win**: ~18 tests (9 Signing + 9 Identity) turn green. Possibly more — any latent cross-test flake tied to shared system db disappears.

**No rebuild needed.**

### Wave 2 — `event.on.Type` becomes `EventType` enum

**Goal**: Stop the builder from hallucinating event type strings.

**Where**: `PLang/App/modules/event/on.cs:22`:
```csharp
[IsNotNull]
public partial Data.@this<string> Type { get; init; }
```
Change to:
```csharp
[IsNotNull]
public partial Data.@this<EventType> Type { get; init; }
```
Add `using App.Events;` at the top. Remove the `Enum.TryParse<EventType>(...)` runtime check — a typed field makes it redundant.

**Expected win**: 3 Event tests (Basic, Priority, Remove) build + run correctly. The source generator should now expose the valid enum values to the builder on its next build.

**Rebuild**: Event/ subtree only.

### Wave 3 — Goal auto-return last `%__data__%`

**Goal**: A goal that runs to completion without an explicit `return` step returns the value in `%__data__%` at goal-end.

**Where**: goal execution path — the code that runs `Goal.Steps.RunAsync` and returns the final `Data` object to the caller (the caller being either the engine's top-level or a `goal.call` action). At goal exit, if there's no explicit return, return the current `%__data__%` value from memory as the goal's result. That becomes what `goal.call, write to %result%` captures.

**Coder discussion item**: For goals whose last step is a pure side-effect (e.g. `write out "done"`), what does `%__data__%` carry? If the output handler writes the rendered string into `%__data__%`, the auto-return is harmless unless captured. If it writes null, auto-return is also a no-op. Verify before landing.

**Expected win**: ~5–8 tests in the "Expected non-null, Actual null" cluster turn green (specifically the ones whose helper goals rely on a last-step set without explicit return). A few unrelated null-failures remain — those are not goal-return issues and end up in Wave 6.

**No rebuild needed.**

### Wave 4 — Builder prompt + http.download split

This is the biggest wave. It combines an API change in `http.download` with a cluster of prompt rules that together fix the modifier-routing, arithmetic-set, and module-misroute bugs. Because prompt edits force a full rebuild of every test's `.pr`, everything else piggybacks on this rebuild.

#### 4a — Split `http.download`

**Where**: `PLang/App/modules/http/download.cs`

- Remove `SaveTo` parameter.
- The action returns downloaded bytes in `Data.@this` (the existing Ok/FromError plumbing).
- Update `IHttpProvider.DownloadAsync` and `DefaultHttpProvider` so they no longer accept a save-to-disk step; they just produce bytes.
- `OnProgress`, `Unsigned`, `SignOptions`, `Headers`, `TimeoutInSec` stay.

`file.save` already exists (`PLang/App/modules/file/save.cs`) with `Path` + `Value` — no change needed there.

**Ripples**:
- Any app goal using `download X save to Y` will fail to build until it's rebuilt. That's fine — Wave 4 includes a full Tests/ rebuild.
- If there are any non-Tests goals (system/, os/apps/) using `SaveTo`, audit and update them — grep for `http\.download.*SaveTo` to catch.

#### 4b — Builder prompt edits

**Where**: `system/builder/llm/BuildGoal.llm`

Draft rules (architect; coder may refine phrasing to match file's existing voice):

**Rule — Modifier shape.**
> Modifiers wrap a preceding action. They live in the action's `modifiers` array. Never concatenate modifier names into the module path. Module names never contain dots.
>
> Correct: `{"module":"signing","action":"sign","modifiers":[{"module":"error","action":"handle",...}]}`
>
> Incorrect: `{"module":"signing.error.handle","action":"sign"}` — the dotted path invents a non-existent module.

**Rule — Arithmetic on `set` RHS.**
> When a step says `set %x% = <expr>` and `<expr>` contains arithmetic operators (`+`, `-`, `*`, `/`, `%`), do not store `<expr>` as a string value. Emit two actions: a `math.*` action computing the expression (its result flows into `%__data__%`), then a `variable.set(Name=%x%, Value=%__data__%)`.
>
> `set %count% = %count% + 1` →
> ```
> [{"module":"math","action":"add","parameters":[{"name":"A","value":"%count%"},{"name":"B","value":1}]},
>  {"module":"variable","action":"set","parameters":[{"name":"Name","value":"%count%"},{"name":"Value","value":"%__data__%"}]}]
> ```
>
> Same rule applies to phrasings like `add 1 to %count%` — two actions, same shape.

**Rule — Module routing: wait vs timeout.**
> `wait for N ms` and similar sleep/pause intents map to `timer.sleep(Ms=N)`. `timeout.after` is a *modifier* that wraps another action with a deadline; it never appears as a standalone action.

**Rule — Module routing: download + save.**
> `download URL, save to PATH` maps to two actions: `http.download(Url=URL)` producing bytes in `%__data__%`, then `file.save(Path=PATH, Value=%__data__%)`. Do not invent a `text.write` module.

**Rule — Event types are enumerated.**
> `event.on.Type` is an enum. Valid values: `BeforeGoal`, `AfterGoal`, `BeforeStep`, `AfterStep`, `BeforeAction`, `AfterAction`, `OnError`, `OnVariableChange`, `OnBeforeGoalLoad`, `OnAfterGoalLoad`, `OnBeforeStepLoad`, `OnAfterStepLoad`, `OnCacheHit`, `OnCacheMiss`, `BeforeAppStart`, `AfterAppStart`. Never invent values like `beforeGoalCall` or `before`.

*(After Wave 2 lands, the source generator will surface these via the type system — this rule is belt-and-suspenders in case of regressions.)*

**Principle to carry across all rules**: PLang steps can be in any language. These rules describe *semantic intent* (arithmetic-on-set, wait-intent, download-and-save-intent), not specific English words. Coder should keep that in mind when phrasing.

#### 4c — Full Tests/ rebuild

After 4a + 4b land, run the per-folder build loop (tester's script at `/tmp/per_folder_build.sh` or re-implement inline) on the Tests/ tree. Every `.pr` regenerates under the new prompt.

#### 4d — List/Math JsonParseError

Separately from the prompt edits, Tests/Modules/List/ and Tests/Modules/Math/ fail with "Response is not valid JSON" across retries. The tester couldn't see the raw LLM response. **Coder: before running the Wave 4 rebuild, run one build on each with `!debug=BuildGoal:6` (or the equivalent `--debug={"goal":"BuildGoal","maxLength":0}`) and inspect the raw response.** Likely the goal text is forcing the LLM into a markdown/free-form output. If so, add a prompt rule to the BuildGoal step about strict JSON output (may already be there — might just need tightening for long List/Math goals).

Once the raw response is visible, either (a) the fix is another prompt rule (add to 4b) or (b) it's a specific goal-content issue (rare — Math/Math.test.goal currently passes, so most likely just List/ has a pathological goal).

**Expected Wave 4 wins**:
- 6 build-failure folders all build.
- Loop/Loop + Loop/Foreach/Dictionary → arithmetic-set now works.
- Http/DownloadFile + Http/DownloadSkip → download+save chain.
- 3 Signing build-failures (Expired, TimedOut, NonceReplay) → modifier shape correct.
- List/, Math/ → JSON diagnosis.

**Rebuild**: full Tests/ tree.

---

### Wave 5 — Parked

- **`Tests/App/SetupGoal/Start.test.goal`** — per D6 above. Do not delete. Add a short comment in the file (or a sibling `NOTES.md`) noting that Setup semantics are undefined in runtime2 and this test returns when the spec lands. No coder action required.

---

### Wave 6 — Return after coder finishes waves 1–4

Tester re-baselines after waves 1–4 land. I triage the remainder with Ingi. Best-guess contents of the tail (subject to reshuffling after the re-baseline):

- Builder-module tests (4: GetActions, GetTypeInfo, ValidateValid, ParseGoal) — need `--building` runtime flag or a tag-based include. Environmental.
- UI render tests (2: RenderCallGoal, RenderWithParams) — variable substitution wrong.
- ListOps2 — `set item at index` mutation not persisting (runtime handler bug).
- Condition/Compound And + Mixed — sub-goal scope leak assumption in tests. Likely test-rewrite, possibly compound-condition compilation bug.
- Goal/Relative — sub-directory .pr resolution broken.
- Goal/Return/GoalCallReturn — likely resolved by Wave 3 but verify.
- ContextVars/Basic + ContextVars/System — %engine.*% system variables unpopulated. Unrelated to goal-return.
- Crypto/HashBcryptVerify — bcrypt unsupported, either add the algorithm or remove the test.
- Test/Run/TestRunEnforcesTimeout — test-module internal, needs investigation.
- Test/Discover/TestDiscoverReportsStaleWhenPrMissing — likely resolved by Wave 1 (if the stale state was cross-test pollution) or by Wave 4 rebuild. Re-verify.
- Error/RetryOnly — "timed fail" — likely a deliberate assertion pattern, need to read the test.
- Event/Override — "File not found: nonexistent.json" — test setup issue.
- Signing/DotNavigation + Signing/NoIdentity — null assertion, may be resolved by Wave 1.
- Error/Types — null assertion, may be resolved by Wave 3.
- Actors/Context — null assertion, may be resolved by Wave 3.
- Stale: Condition/Files/{FileExists, FileNotExists} — likely resolved by Wave 4 rebuild.

---

## Per-test classification table

Categories:
- `W1` fixed by Wave 1 (per-test isolation)
- `W2` fixed by Wave 2 (event enum)
- `W3` fixed by Wave 3 (goal auto-return)
- `W4` fixed by Wave 4 (builder prompt + http.download split)
- `W5` parked (Setup)
- `W6` tail — re-triage after waves 1–4

### Build failures (6)

| Path | Error | Wave | Root cause |
|---|---|---|---|
| `Modules/Http/DownloadFile` | `text.write` not found | W4 | Builder misroute — replace with `http.download` + `file.save` chain |
| `Modules/Signing/Expired` | `timeout.after.after` | W4 | Modifier shape (wait → `timer.sleep`) |
| `Modules/Signing/TimedOut` | `timeout.after.after` | W4 | Same |
| `Modules/Signing/NonceReplay` | `signing.error.handle` | W4 | Modifier concatenated into module path |
| `Modules/List` | JsonParseError | W4 | Diagnose with --debug; likely prompt tightening for long goals |
| `Modules/Math` | JsonParseError | W4 | Same |

### Runtime error fails (29)

| Path | Error | Wave |
|---|---|---|
| `Modules/Signing/Expired/...` | `Identity 'testSigner' already exists` | W1 |
| `Modules/Signing/EmptyData/...` | same | W1 |
| `Modules/Signing/HeaderMismatch/...` | same | W1 |
| `Modules/Signing/CustomContracts/...` | same | W1 |
| `Modules/Signing/ProviderSwap/...` | same | W1 |
| `Modules/Signing/Roundtrip/...` | same | W1 |
| `Modules/Signing/TamperedData/...` | same | W1 |
| `Modules/Signing/WithHeaders/...` | same | W1 |
| `Modules/Signing/TimedOut/...` | same | W1 (once W4 lets it build) |
| `Modules/Identity/Create/...` | `Identity 'TestCreate' already exists` | W1 |
| `Modules/Identity/ArchiveNonDefault/...` | same pattern | W1 |
| `Modules/Identity/DotNavigation/...` | same | W1 |
| `Modules/Identity/Unarchive/...` | same | W1 |
| `Modules/Identity/Rename/...` | same | W1 |
| `Modules/Identity/SwitchDefault/...` | same | W1 |
| `Modules/Identity/Export/...` | same | W1 |
| `Modules/Identity/ArchiveDefault/...` | same | W1 |
| `Modules/Identity/GetByName/...` | same | W1 |
| `Modules/Event/Remove/...` | `Unknown event type: 'output.write'` | W2 |
| `Modules/Event/Basic/...` | `Unknown event type: 'beforeGoalCall'` | W2 |
| `Modules/Event/Priority/...` | `Unknown event type: 'before'` | W2 |
| `Modules/Event/Override/...` | `File not found: nonexistent.json` | W6 |
| `Modules/Goal/Relative/...` | `File not found: .build/sub/subgoal.pr` | W6 |
| `Modules/Builder/GetTypeInfo/...` | `Building is not enabled` | W6 |
| `Modules/Builder/GetActions/...` | `Building is not enabled` | W6 |
| `Modules/Builder/ValidateValid/...` | `Building is not enabled` | W6 |
| `Modules/Builder/ParseGoal/...` | Access denied | W6 |
| `Modules/Error/RetryOnly/...` | `timed fail` | W6 |
| `Modules/Crypto/HashBcryptVerify/...` | `Algorithm 'bcrypt' is not supported` | W6 |

### Assertion fails (19)

| Path | Expected vs Actual | Wave |
|---|---|---|
| `App/SetupGoal/Start.test.goal` | True vs null | W5 (parked) |
| `App/StepResult/StepResult.test.goal` | non-null vs null | W3 (probable; verify after) |
| `App/ReturnMapping/ReturnMapping.test.goal` | non-null vs null | W3 |
| `Modules/List/ListOps2.test.goal` | 99 vs 20 | W6 |
| `Modules/Loop/Loop.test.goal` | 3 vs "0 + 1 + 1 + 1" | W4 |
| `App/Actors/Context/...` | not-null vs null | W3 (probable) |
| `Modules/Error/Types/...` | "TestKey" vs null | W3 (probable) |
| `Modules/Signing/DotNavigation/...` | "ed25519" vs null | W1 (probable — after fresh db, the identity setup works) |
| `Modules/Signing/NoIdentity/...` | True vs null | W1 (probable) |
| `Modules/Goal/Return/GoalCallReturn/...` | non-null vs null | W3 |
| `Modules/Test/Run/TestRunEnforcesTimeout/...` | True vs null | W6 |
| `Modules/Test/Discover/TestDiscoverReportsStaleWhenPrMissing/...` | True vs False | W6 |
| `Modules/Variable/ContextVars/Basic/...` | non-null vs null | W6 (context vars system unrelated to D4) |
| `Modules/Variable/ContextVars/System/...` | non-null vs null | W6 |
| `Modules/Loop/Foreach/Dictionary/...` | 3 vs "0 + 1" | W4 |
| `Modules/Condition/Compound/Mixed/...` | "yes" vs null | W6 (sub-goal scope) |
| `Modules/Condition/Compound/And/...` | "both-true" vs null | W6 (sub-goal scope) |
| `Modules/Ui/RenderCallGoal/...` | "Error" vs "Result: {}" | W6 |
| `Modules/Ui/RenderWithParams/...` | full template vs partial | W6 |

### Stale (4)

| Path | Wave |
|---|---|
| `Modules/Signing/NonceReplay/...` | W4 (rebuild) |
| `Modules/Http/DownloadFile/...` | W4 (rebuild) |
| `Modules/Condition/Files/FileNotExists/...` | W4 (rebuild; stale hash only) |
| `Modules/Condition/Files/FileExists/...` | W4 (rebuild) |

---

## Rollup

| Wave | Tests expected to pass after wave | Notes |
|---|---|---|
| W1 (isolation) | +18 (9 Signing + 9 Identity) | plus ~3 probable assertion fails that were masked by the DuplicateName error |
| W2 (enum) | +3 (Event basic/priority/remove) | |
| W3 (auto-return) | +5–7 (null-return tests) | coder confirms count after implementing |
| W4 (prompt + split) | +2 loop arithmetic + 6 build-failures + stale rebuilds | |
| W6 (tail, later) | ~12 | re-triage after re-baseline |
| W5 (parked) | 1 (SetupGoal) stays in limbo until Setup spec lands | |

Best-case after coder lands waves 1–4: **~135 pass / ~26 fail / 0 stale** — roughly 85% green, up from 68%. Real number depends on how many tail items are side-wins from the big fixes.

---

## Order-of-operations for coder

1. Wave 1 (isolation) — no rebuild, easy to verify with a focused re-run of signing/identity tests.
2. Wave 2 (enum) — rebuild Event/ only; confirms the source-generator path for typed enums works end-to-end.
3. Wave 3 (auto-return) — no rebuild; coder may want to add a new C# test covering the behavior.
4. Wave 4 (http.download split + prompt edits) — biggest scope; includes the List/Math raw-response diagnosis before the prompt edits land. Run the per-folder build loop at the end. Hand off to tester for re-baseline.

One commit per wave is ideal (clean git history). Bundled is acceptable if the diff stays readable.

Coder: if any design decision here becomes awkward during implementation, **stop and ask Ingi** — do not silently re-interpret. Especially D4 (goal auto-return) and D5 (http.download split) where the design discussion was short.
