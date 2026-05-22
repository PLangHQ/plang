# Test designer plan — filesystem-permission

This branch ships PLang's filesystem permission system AND unifies suspend/resume across the runtime. Test surfaces are larger than for a single-concern branch — pay attention to both.

## Read first

1. **`summary.md`** — cross-cutting design decisions. 5-minute read.
2. Each stage's "Tests" section as you size that stage's work.

## Where to start

**Stage 1's tests** can be designed and written immediately — types are self-contained, no engine integration, no mocks needed. Coder lands stage 1 first; your tests are ready when they do.

Stages 2a and 3 can be designed in parallel; their test surfaces are independent.

## Stage-by-stage test plan

| Stage | Test kind | Where | Notes |
|-------|-----------|-------|-------|
| 1 | C# unit | `PLang.Tests/App/FileSystem/PermissionTests/` | Pure coverage matrix; no mocks. |
| 2a | C# unit + plang `--test` integration | `PLang.Tests/App/CallbackTests/` (rewritten) + `Tests/Callback/` (extended) | Largest surface. Engine, channel, resume. |
| 2b | C# unit | `PLang.Tests/App/FileSystem/PermissionTests/AuthorizeTests/` | Mock `actor.Permission.Find/Add` + the channel's `Ask`. |
| 3 | C# unit + integration (real sqlite) | `PLang.Tests/App/FileSystem/PermissionTests/` (alongside stage-1 tests) | Real `App.SettingsStore` round-trip; per-actor isolation. |
| 4 | C# unit + plang `--test` | `PLang.Tests/App/FileSystem/` + existing `Tests/App/modules/file/` (kept green) | Volume: ~11 methods × {in-root, out-of-root × stateful, stateless} combinations. Plan a parametrized fixture, not one test per case. |
| 5 | plang `--test` end-to-end | `Tests/Permission/` (new) | Real apps-tree fixture under `Tests/Permission/_fixtures/apps/`. Six-step integration test. |

## Test surfaces per stage

### Stage 1 — pure types

Unit tests, no mocks:

- **Verb coverage matrix:** every sub-option combination for Read / Write / Delete. `Covers` reads naturally for both default-full and narrowed records.
- **Match-mode dispatch:** Exact / Glob / Regex behaviour. Unknown enum value → false (fail-closed).
- **`Permission.Covers`:** grant vs request with broad/narrow records on both sides.
- **JSON round-trip:** a `Permission` record serialises and deserialises losslessly.

### Stage 2a — engine round-trip

The biggest test surface in the branch. Subgroups:

- **`Type.Exit()` extension:** true for `Ask`, false for `string`/`byte[]`/etc.
- **`Synthetic` flag:** defaults to true; source-generator-emitted PR actions have it `false`. Push records onto the Call frame.
- **`Data.ShouldExit` combinations:** each flag individually and combined.
- **`Goal.RunFrom`:** runs from `(stepIdx, actionIdx)` through remaining actions in step, then remaining steps.
- **`Step.RunFrom`:** runs from `actionIdx` through remaining actions in step.
- **`Snapshot.Resume` (single goal):** integration test with a 3-step goal whose first step suspends; resume completes the goal with the bound answer.
- **`Snapshot.Resume` (cross-goal):** the canonical `Start` → `AskAQuestion` example from stage 2a's tests section. Output must be `"Hello\nAsking\nAlice"` across capture+resume.
- **`output.ask` routes through `Channel.Ask`:** Stream blocks; Message produces `Data<Ask>`. Position-capture lands on the outermost real step frame for nested invocations.
- **`action.RunAsync(ctx)` replaces `App.Run`/`App.RunAction`:** survey, assert no production references to the dead symbols remain.

Tests adapt the existing `Tests/Callback/AskWithVars` and `AskVarsResumeBindsValue` to the new shape — the old "set `%!ask.answer%` and run goal" pattern still works; the assertions stay valid.

### Stage 2b — Path.Authorize

C# unit tests, mock the storage view and the channel:

- Grant exists → `Data.Ok` immediately (no `output.ask` call).
- Stateful answer `"a"` → signs with `AlwaysExpiry`, `Add`, returns Ok.
- Stateful answer `"y"` → signs with no expiry, `Add`, returns Ok.
- Stateful answer `"n"` → returns `Data.Fail(PermissionDenied)` with the constructed `Permission`.
- Stateful answer `"garbage"` → recurses with `"Invalid answer 'garbage'. "` prefix on next question.
- Stateless mock → returns `Data<Ask>` unchanged (Type.Exit() check; stage 2a's machinery handles the rest).
- Constructed `Permission` carries `AppId = Context.App.Id`, `Actor = Context.Actor.Name`, `Path = this.Absolute`, `Verb = requested verb`, `Match = Match.Exact`.

### Stage 3 — storage

C# unit + integration (real sqlite, `:memory:` or temp file):

- **Round-trip:** add a signed grant; `Find` returns it; signature validates.
- **Per-actor isolation:** user grant and system grant; each actor's `Find` returns only its own.
- **Two-home unification:** in-memory grant + persisted grant coexist; `Find` returns the right one.
- **Verb narrowing:** full-allow covers narrow request; Read-only doesn't cover Delete.
- **Glob matching:** glob grant matches exact-path request; non-matching glob doesn't.
- **Revocation:** in-memory and persisted, both removable.
- **Signature failure:** corrupted signature → `Find` skips.

### Stage 4 — FS surface

Volume-heavy. Plan a parametrized fixture:

For each FS method (~11 of them):
- In-root path → succeeds.
- Out-of-root path against Stream channel (stateful, piped answer) → succeeds synchronously, grant stored.
- Out-of-root path against Message-like channel (stateless mock) → returns `Data<Ask>`.

Plus:
- `Move`/`Copy` bundled consent — one missing grant produces a one-prompt callback; two missing grants produce one bundled prompt covering both paths.
- Existing `Tests/App/modules/file/` goal tests continue to pass against the new shape.

### Stage 5 — end-to-end

Plang `--test` style under `Tests/Permission/`:

The 6-step integration test from `stage-5-messages-end-to-end.md` (no grant → suspend → grant "a" → store → re-query no prompt → restart still no prompt → revoke re-prompts → narrowed grant rejects wider request).

Test-driver supplies canned answers (`"a"`, `"y"`, `"n"`); signing/Snapshot pipeline stays real.

## Build / test runners

Two distinct runners — don't conflate:

```bash
# C# tests — fast, recompiles in place
dotnet run --project PLang.Tests

# PLang tests — uses pre-built PlangConsole binary; rebuild from clean
# to avoid the stale-binary trap (see CLAUDE.md).
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

Never delete `Tests/**/.build/` — those are tracked `.pr` files.

## Conventions

- One concern per test file.
- Use the `*Tests` suffix on folder/namespace when mirroring `PLang/App/X` → `PLang.Tests/App/XTests/`. The aliases on `PLang.Tests/GlobalUsings.cs` (`Data`, `Variables`) shadow nested PascalCase namespaces — avoid `PLang.Tests.App.Data` etc.
- Plang test files are `*.test.goal` under `Tests/` (uppercase). Never under `tests/`, `.bot/`, `os/`.

## Followups (intentional v1 gaps)

These are tracked in `Documentation/Runtime2/todos.md` and are NOT something to test for now:

- Replacing `!ask.answer` sentinel with `Answer` parameter.
- `output.ask` structured options.
- `Snapshot.ResumeChain` cleanup.
- Per-channel snapshot serializers (error trail inclusion etc.).
- `App.Snapshot()` → `Snapshot.@this.Capture(ctx)` relocation.

Tests that *would* exercise these don't exist yet — the v1 spec is `!ask.answer` sentinel + text-only ask.
