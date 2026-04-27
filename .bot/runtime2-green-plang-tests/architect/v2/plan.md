# Architect v2 — Plan: F4c-1 Surgical Rebuild (coder dispatch)

## Context

Tester v2's F4c-1 (critical): the five BuildGoal.llm prompt rules coder landed in v1 are **dormant** — coder's full Tests/ rebuild regressed 38 tests and was reverted, so the rules never produced new `.pr` output. Tests the rules target still fail.

My v1 review (`v1_review_summary.md`) accepted option (a): surgical per-folder rebuild with hand review. This plan defines what coder rebuilds, how to judge the output, and when to stop.

## Architect's role here is to specify, not execute

I do not build, rebuild, or run tests. Coder runs `plang build`; tester runs `plang --test`. This document is coder's dispatch — a per-folder list with the rule each rebuild should satisfy and the hand-review criteria coder applies before committing.

## Goal

Rebuild ~11 currently-failing Tests/ folders. For each, coder:

1. Deletes the current `.pr` files in that folder.
2. Rebuilds that folder via `plang build '--build={"files":[...]}'` from the `Tests/` root (Ingi's stated invocation).
3. Reads the new `.pr` and **hand-reviews against the "Expected rule outcome" column below**.
4. Keeps if it matches. `git checkout -- <folder>/.build/*.pr` to restore if it doesn't, then logs the drift.

Coder commits only the `.pr` files that pass hand review. Writes a concise per-folder log (rebuilt / kept / restored + diagnosis) for the ones that drifted — that feeds Wave 6 decisions and any prompt-rule iteration we do later.

## Pre-conditions (coder verifies before starting)

- `dotnet build PlangConsole/PLangConsole.csproj` is clean on .NET 10.
- Branch `runtime2-green-plang-tests` at `04521e7e` (coder v2) with working tree clean.
- Tester v2 baseline is 128/35/5. Any number different from that before starting means state drifted — stop and ask.

## Target folders (11)

Invocation (per Ingi): from the `Tests/` folder as root — `plang build '--build={"files":["<path-from-Tests/>"]}'`.

### Modifier-shape rule — 3 folders

Rule reminder (from `BuildGoal.llm` line ~192): *"Modifier actions live in the flat action list as separate entries directly after the action they wrap. Module names never contain dots."*

| # | Folder | Current failure | Expected rule outcome |
|---|---|---|---|
| 1 | `Tests/Modules/Signing/Expired` | `timeout.after.after` — build fails | Step `wait for 60 ms` → standalone `timer.sleep(Ms=60)` action (NOT `timeout.after.after`). Step `archive identity 'testSigner', force, on error ignore` → `identity.archive` with an `error.handle` modifier in its `modifiers` array. No module name contains dots. |
| 2 | `Tests/Modules/Signing/TimedOut` | `timeout.after.after` — build fails | Identical pattern to Expired. |
| 3 | `Tests/Modules/Signing/NonceReplay` | `signing.error.handle` — stale | Step expressing `verify ... on error ...` → `signing.verify` action with `error.handle` as a separate entry in its `modifiers` array. Module `signing` stays `signing`, not `signing.error.handle`. |

### Arithmetic-on-set rule — 2 folders

Rule reminder (line ~156–162): *"When a step expressing a `set` assignment has arithmetic operators on the right side, or expresses accumulation intent, emit a `math.*` action chain producing the result in `%__data__%`, then a `variable.set(Name, Value=%__data__%)`."*

| # | Folder | Current failure | Expected rule outcome |
|---|---|---|---|
| 4 | `Tests/Modules/Loop` | `%count%` becomes string `"0 + 1 + 1 + 1"` | `CountItem.goal` step `set %count% = %count% + 1` → two actions in sequence: `math.add(A=%count%, B=1)` then `variable.set(Name=%count%, Value=%__data__%)`. The old single-action `variable.set(Name=%count%, Value="%count% + 1")` is the bug. |
| 5 | `Tests/Modules/Loop/Foreach/Dictionary` | Same string-concat pattern on the iterator accumulator | Same two-action chain shape. |

### Download+save rule — 1 folder

Rule reminder (line ~165–172): *"`http.download` fetches bytes into `%__data__%` — it does not persist to disk. A step expressing download-then-save intent maps to two actions: the download, then `file.save`. There is no `text.write` module."*

| # | Folder | Current failure | Expected rule outcome |
|---|---|---|---|
| 6 | `Tests/Modules/Http/DownloadFile` | `text.write` not found — stale | `download ..., save to ...` → two actions: `http.download(Url=...)` then `file.save(Path=..., Value=%__data__%)`. `http.download` no longer takes `SaveTo` post-W4b. |

### Enum-event rule — 3 folders

Rule reminder (line ~194): *"`event.on.Type` accepts only the enum values shown in the action schema. Never invent arbitrary strings."* Plus Wave 2 changed the handler's `Type` param to `Data<EventType>`, so the source generator's type info alone should drive the LLM — this rule is belt-and-suspenders.

| # | Folder | Current failure | Expected rule outcome |
|---|---|---|---|
| 7 | `Tests/Modules/Event/Basic` | `Unknown event type: 'beforeGoalCall'` at runtime | `Type` param value is one of `BeforeGoal` / `AfterGoal` / `BeforeStep` / etc. — never `beforeGoalCall`. |
| 8 | `Tests/Modules/Event/Priority` | `Unknown event type: 'before'` | Same — valid enum value. |
| 9 | `Tests/Modules/Event/Remove` | `Unknown event type: 'output.write'` (wrong slot) | `Type` is an enum value. If step expresses "remove event with action pattern output.write", that string goes into `ActionPattern`, not `Type`. |

### Diagnose-first — 2 folders (JsonParseError)

| # | Folder | Current failure | Approach |
|---|---|---|---|
| 10 | `Tests/Modules/Math` | `Response is not valid JSON` across retries | Coder runs `plang build '--build={"files":["Modules/Math"],"cache":false}'` first — if still fails, the LLM raw response needs inspection. If coder can view the raw response (via debug flags or logs), read it and surface what's happening — markdown wrapping? truncation? If not inspectable in this session, log + defer. Do **not** spend more than one retry + one raw-response look on this. |
| 11 | `Tests/Modules/List` | Same | Same approach. |

## Coder's hand-review procedure per folder

For each folder:

1. `git status` clean baseline.
2. Delete `.pr` files: `rm Tests/<folder>/.build/*.pr` (keep `traces/` and `manifest.json`).
3. From `Tests/` as root: `plang build '--build={"files":["<folder-path-relative-to-Tests>"]}'`.
4. Read the regenerated `.pr` file(s).
5. **Check each rule-expectation line in the table above literally.** Does the action list match? Are modifier actions in the modifiers array, not concatenated into module names? Are arithmetic steps a two-action chain?
6. Decision:
   - **Match** → move to next folder. Do not commit yet — coder commits all at end in one batch.
   - **Drift** → `git checkout -- Tests/<folder>/.build/*.pr` to restore. Log: what the LLM produced instead of the expected shape, which rule seems to have failed to land, any hypothesis about why.
7. Keep a running per-folder log (`v2/rebuild_log.md` or inline in coder's summary.md) — architect reads this to decide follow-up prompt work.

## Stop conditions (coder pauses and asks Ingi / architect)

- **Any passing test regresses after a rebuild.** Restore and stop.
- **More than 3 of the 11 folders drift from the rule.** Systemic — don't keep pushing. Architect wants to see the outputs before deciding if the rules need re-drafting or a builder-level validator is needed.
- **A single folder needs more than 1 rebuild retry + 1 cache-bust retry to look right.** Log and move on; don't burn LLM budget.
- **JsonParseError on Math/List persists** after cache-bust and one raw-response look. Defer.
- **Any unexpected build failure on a previously-clean folder** (not in the target 11) — something regressed between coder v2 and now. Stop.

## Success criteria

- At least 7 of the 11 target folders rebuild clean and match their expected rule outcome.
- Tester's post-rebuild run (separate session) shows 128 pass → 135+ pass.
- Zero regressions from the 128 pass-set.
- Clear written diagnosis for any folder that drifted — so architect can decide prompt iteration vs. builder-level dotted-path validator as a follow-up.

## After coder finishes

- **Tester v3** re-baselines. Confirms count, checks for regressions, spot-checks one or two `.pr` files against the same rule-outcome table.
- **Architect v3 (me)** does Wave 6 triage on what's still failing with Ingi — the tail that has nothing to do with these five rules (UI render, ListOps2, Builder env tests, etc.).

## Deliverables

Coder produces:
- Rebuilt `.pr` files (committed as one batch).
- `.bot/runtime2-green-plang-tests/coder/v3/plan.md` — coder's own plan acknowledging this dispatch.
- `.bot/runtime2-green-plang-tests/coder/v3/summary.md` — per-folder rebuild log (folder, verdict, rule, drift notes).
- `.bot/runtime2-green-plang-tests/coder/v3/changes.patch`.

Architect produces this session:
- `v2/v1_review_summary.md` — ✓ done.
- `v2/plan.md` — this file.
- After coder+tester land: `v2/summary.md` + `v2/result.md` (post-coder analysis).

## Out of scope

- `os/apps/Installer/InstallDependencies.goal` — lives outside `Tests/`, naturally excluded by the Tests/-root invocation. Will be handled when that app gets attention.
- Full Tests/ rebuild. The 38-regression experience is fresh; this dispatch is surgical, bounded.
- Prompt-rule edits. If the rebuild reveals rule weakness, architect drafts edits in a **future** session after seeing coder's per-folder log — do not edit `BuildGoal.llm` speculatively during rebuild.
- Golden-eval infrastructure (tester v2's option b). Separate architect task, logged as follow-up.
- Wave 6 triage of the remaining ~20 fails. After this lands and tester re-baselines.

## Approval

Plan approved by Ingi with two clarifications (captured above):
1. `plang build` is invoked from `Tests/` as root, not project root.
2. `os/apps/*` is out of scope (naturally excluded by the Tests/-root invocation).

Ready to dispatch to coder.
