# Coder v3 — Plan: F4c-1 Surgical Rebuild

## Context

Architect v2 (`.bot/runtime2-green-plang-tests/architect/v2/plan.md`) dispatched a surgical rebuild of 11 currently-failing Tests/ folders. The goal is to activate the five dormant `BuildGoal.llm` prompt rules I landed in v1 — they're code-present but unexercised because the full Tests/ rebuild in v1 regressed 38 tests and I reverted everything.

Strategy: rebuild folder-by-folder, hand-review each `.pr` against a specific rule-outcome expectation, keep only what matches. Bounded blast radius — worst case a folder stays failing, which it already is.

## Pre-conditions

Verified:
- ✅ `BuildGoal.llm` has the 5 rules (lines 164, 166–174, 176–177, 192, 194).
- ✅ Working tree is clean (the `junit_sensitive_masked.xml` delta is pre-existing from earlier).
- ✅ Branch at `969e75e5` (architect v2). Tester v2 baseline was 128/35/5 — I'll trust that and not re-run the full suite; tester v3 re-baselines after.
- ⏳ Will `dotnet build` and smoke `plang build` on one folder before committing to the rebuild.

## The 11 target folders (per architect v2/plan.md)

Grouped by which rule they exercise:

### Modifier-shape (3)
1. `Modules/Signing/Expired` — `wait for 60 ms` → `timer.sleep`; `on error ignore` → `error.handle` modifier (not dotted module)
2. `Modules/Signing/TimedOut` — same pattern
3. `Modules/Signing/NonceReplay` — `verify ... on error ...` → `signing.verify` + `error.handle` modifier

### Arithmetic-on-set (2)
4. `Modules/Loop` — `set %count% = %count% + 1` → `math.add` + `variable.set(Value=%__data__%)`
5. `Modules/Loop/Foreach/Dictionary` — same

### Download+save (1)
6. `Modules/Http/DownloadFile` — `download ..., save to ...` → `http.download` + `file.save`

### Enum-event (3)
7. `Modules/Event/Basic` — `Type=BeforeGoal` (not `beforeGoalCall`)
8. `Modules/Event/Priority` — valid enum value (not `before`)
9. `Modules/Event/Remove` — `Type` is enum; `output.write` goes in `ActionPattern`

### Diagnose-first (2) — budget-capped
10. `Modules/Math` — JsonParseError; one `cache:false` retry + one raw-response look, else log & defer
11. `Modules/List` — same

## Procedure (per folder)

1. `git status` clean.
2. `rm -f Tests/<folder>/.build/*.pr` (preserve `traces/`).
3. From `Tests/` as root: `plang build '--build={"files":["<relative-path>"]}'`.
4. Read each regenerated `.pr` file.
5. Compare against the expected-shape table in `architect/v2/plan.md`:
   - Module names contain no dots in the `module` field.
   - Modifier actions (error.handle, cache.wrap, timeout.after) appear as **separate entries in the flat action list, right after the action they wrap** — per W4's per-action modifier design.
   - Arithmetic-on-set produces a two-action `math.*` + `variable.set` chain.
   - `http.download` has no `SaveTo`; `file.save` follows.
   - `event.on.Type` value is a valid `EventType` enum member.
6. Decision:
   - **Match** → leave staged, move on.
   - **Drift** → `git checkout -- Tests/<folder>/.build/` to restore, log what the LLM produced instead.

## Commit policy

One final commit at end of successful folders, per architect: "coder commits all at end in one batch." Commit message will list kept/restored folders.

## Stop conditions (from architect v2/plan.md)

- Any passing test regresses → restore and stop.
- More than 3 of 11 folders drift → stop and hand back to architect with logs.
- Any single folder needs >1 retry + 1 cache-bust → log and move on.
- JsonParseError on Math/List persists after cache-bust + one raw-response look → defer.
- Unexpected build failure on a previously-clean folder (not in the 11) → stop.

## Deliverables

- `.bot/runtime2-green-plang-tests/coder/v3/plan.md` ← this file.
- `.bot/runtime2-green-plang-tests/coder/v3/summary.md` — per-folder rebuild log (folder, verdict, rule applied, drift notes).
- `.bot/runtime2-green-plang-tests/coder/v3/changes.patch` — `git diff runtime2..HEAD -- ':(exclude).bot'`.
- `.bot/runtime2-green-plang-tests/coder/v3/v2_review_summary.md` — one-paragraph summary of what tester v2 + architect v2 said for v2→v3 transition.
- Report.json session entry.

## Order of execution

1. Verify env (`dotnet build` + smoke one build) — ~3 min.
2. Modifier-shape cluster first (3 folders) — lowest LLM variance risk, shared rule.
3. Arithmetic-on-set (2).
4. Download+save (1).
5. Enum-event (3).
6. Diagnose-first Math/List (2) — strict budget.
7. Write per-folder log, commit, generate patch, push.

## Non-goals

- No BuildGoal.llm edits this session. If rules drift, log it — architect drafts edits in a future session.
- No full Tests/ rebuild. Only the 11 named folders.
- No test re-authoring.
- No Wave 6 triage.

## Open question for Ingi

None — plan follows architect's dispatch directly. Ready to proceed after approval.
