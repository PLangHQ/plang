# Tester v1 — Summary

## What this is

Tester's execution of architect/v1's Phases 0–2: build prerequisites, Tests/ folder restructure, and Phase 2 baseline. The feature is "get all PLang tests green" on `runtime2-green-plang-tests`. My slice was the structural ground-work before triage (Phase 4) and fix dispatch (Phase 5) happen.

User approved two amendments to architect's plan before I started:
- Add a lightweight **pre-baseline snapshot** so we could compare before/after the restructure.
- Use a **single commit for the whole restructure** instead of per-bucket commits.

## What was done

**Phase 0 — Build prereqs (DONE).**
- `dotnet build PlangConsole/PLangConsole.csproj` → 0 errors, 923 warnings.
- `plang` CLI runs; smoke-built `Tests/Math/` (2/3 goals built; 3rd hit transient LLM JsonParseError — not a Phase 0 blocker).

**Pre-baseline (DONE).**
- Discovered `plang build` run from project root (`/workspace/plang`) walks the *whole* repo, not just `Tests/`. Hits `Publish/Nuget.goal` which uses a non-existent `dotnet` module → whole build aborts. Scoped to `Tests/` with `--app={"create":true}` — also aborts, on `Tests/FromJson/` using non-existent `json` module.
- **User told me FromJson is obsolete** (runtime handles JSON conversion). Deleted `Tests/FromJson/` and `Tests/Runtime2/` (orphan).
- Ran `plang --test={"format":"junit","timeout":5}` on current .pr state → **162 tests: 96 pass / 48 fail / 18 stale**. Saved as `v1/pre-baseline.md`.

**Phase 1 — Restructure (DONE, one commit).**
Executed the layout from `architect/v1/folder_structure.md`:
- Created `Tests/Modules/`, `Tests/App/`, `Tests/Builder/` (reserved, .gitkeep).
- `git mv` every old bucket to its new home. Preserved sub-folders 1:1 — did not flatten folders-with-helpers into single files (architect's structure showed file names; practically, most `Tests/Condition/<X>/` folders contain helper goals that would be lost on flattening).
- Regrouped `Condition/` under `If/`, `Compound/`, `Operators/`, `Files/` (22 subfolders).
- Merged `Foreach/` under `Loop/Foreach/`; `ContextVars/` under `Variable/ContextVars/`.
- Moved old top-level `Builder/` → `Modules/Builder/` (these are module-action tests), reserved new empty top-level `Builder/` for pipeline integration tests.
- Commit `58cf7f77`: 1309 renames, 6 deletions (FromJson + Runtime2), 1 addition (gitkeep).

**Phase 2 — Baseline (DONE).**
- Monolithic `plang build` from `Tests/` kept fail-fasting on LLM JsonParseError or ActionNotFound → switched to a **per-folder build loop**. Each `.test.goal` folder is already its own plang app (carries its own `.build/app.pr`), so one folder's failure no longer blocks others.
- 141 folders found; **135 built ok, 6 failed** (6 build-failures are recorded in `baseline.md`).
- Final `plang --test` → **161 tests: 109 pass / 48 fail / 0 timeout / 4 stale**.
- Pre→post: `+13 pass`, `−14 stale`, `fail unchanged` — restructure added 13 wins from rebuilds, didn't regress any previously-green test. Stale dropped because per-folder rebuilds reached more folders than the monolithic run ever did.

## Key decisions and why

- **Preserve folders 1:1 instead of flattening to files.** `Tests/Condition/Basic/` contains helpers (`SetFalse.goal`, `SetTrue.goal`, `SetWrong.goal`) that the test imports. Flattening to `Tests/Modules/Condition/If/Basic.test.goal` would either lose them or force naming collisions with siblings. Decision documented in the Phase 1 commit.
- **Per-folder build loop instead of monolithic build.** See `baseline.md` "Why a per-folder build loop". Each folder's plang app is isolated; one LLM flake doesn't cascade.
- **Deleted FromJson and Runtime2.** User confirmed FromJson is obsolete; architect's plan already called for Runtime2 deletion.
- **Did not retry more than once on LLM JsonParseError.** List and Math hit it twice in a row → real builder bug, not a flake. Handed to architect Phase 4.

## Findings surfaced for architect Phase 4

Detailed in `v1/baseline.md` build-failures section and `test-report.json` findings 1–6:

1. **Missing `text` module** — `Http/DownloadFile` uses `text.write`. Either restore the module or delete/rewrite the test.
2. **Builder modifier-routing regression** — `Signing/Expired` and `Signing/TimedOut` emit `timeout.after.after` (double suffix). `Signing/NonceReplay` emits `signing.error.handle`. The LLM is concatenating modifier actions onto the preceding module/action string instead of emitting them as separate entries. BuildGoal.llm prompt needs a rule.
3. **Chronic JsonParseError on List and Math** — LLM returns non-JSON for these specific goals, consistent across retries. Phase 4 should `!debug=BuildGoal:6` and read the raw response.
4. **Signing test state leakage** — 9 signing tests fail with `Identity 'testSigner' already exists`. Identities persist across test runs in the inherited SystemDirectory. Test-level cleanup needed; not a signing handler bug.
5. **Loop/Foreach concatenation bug** — `foreach` iteration count shows as `"0 + 1 + 1 + 1"` string instead of `3` integer. Either the accumulator handler is appending instead of adding, or the assertion path mishandles numeric compare.

## Code example — restructure pattern

```
# Before
Tests/Condition/Basic/Condition.test.goal
Tests/Condition/Basic/SetFalse.goal
Tests/ContextVars/Basic/ContextVars.test.goal
Tests/Runtime2/File/test_output.txt   (orphan)
Tests/FromJson/FromJson.test.goal     (obsolete — runtime handles json now)

# After
Tests/Modules/Condition/If/Basic/Condition.test.goal        (folder preserved w/ helpers)
Tests/Modules/Condition/If/Basic/SetFalse.goal
Tests/Modules/Variable/ContextVars/Basic/ContextVars.test.goal
                                      (Runtime2/ deleted, FromJson/ deleted)

# Top-level buckets now:
Tests/
  Modules/    — tests per PLang module
  App/        — cross-cutting App layer (CallStack, RecursionDepth, etc.)
  Builder/    — reserved empty for pipeline integration tests (.gitkeep)
```

All 1309 renames tracked via `git mv` so history survives. Post-restructure per-folder `plang build` refreshed .pr files at their new paths.

## What's next

- **Architect** — Phase 4 triage, using `baseline.md` and `test-report.json`. Classify the 48 fails and 6 build-failures into runtime-handler / builder-prompt / test-authoring / env buckets.
- **Coder** — Phase 5 fix dispatch after architect classifies.
- **Tester (me again)** — after each dispatch batch, rerun the affected subset and re-write the baseline.
- One meta thing worth flagging: the current `plang build` behavior (fail-fast on first error, app.pr not persisted until full success) makes any large tree fragile. Architect might want to spec a continue-on-error builder mode for CI, or at least confirm that per-folder build is the intended CI idiom.
