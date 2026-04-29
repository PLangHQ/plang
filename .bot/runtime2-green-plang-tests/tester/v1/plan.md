# Tester Plan v1 — Build + Restructure + Baseline

Architect's plan (`architect/v1/plan.md`) assigns me Phases 0, 1, and 2. This is my execution plan for that slice.

## Scope of this session

1. **Phase 0** — Build prerequisites.
2. **Pre-baseline snapshot** (small addition to the architect's plan — see rationale below).
3. **Phase 1** — Execute the Tests/ folder restructure per `architect/v1/folder_structure.md`.
4. **Phase 2** — Post-restructure baseline → `baseline.md`.

I'll stop at the end of Phase 2, hand off to architect for triage (Phase 4). If anything in Phase 0 breaks (build failure), I stop and escalate to coder per the plan.

## One amendment I want to propose

Architect's plan has Phase 2 (baseline) *after* Phase 1 (restructure). I'd like to add a **lightweight pre-baseline snapshot** between Phase 0 and Phase 1 — just `plang --test` with junit output, counts and pass/fail list only, no categorization.

**Why:** Without a "before" picture we can't tell whether a test that fails post-restructure was already failing before the move or broke because of the move. With a pre-snapshot, Phase 1's per-bucket `plang --test` confirmation becomes meaningful (we can compare before/after instead of just looking at the after).

**Cost:** One extra `plang --test` run. Output saved as `pre-baseline.md` — list of pass/fail paths only, ~10 min.

I'll only proceed with this if you approve; otherwise I'll skip it and follow the architect's plan literally.

## Detailed steps

### Phase 0 — Build prerequisites

1. `dotnet build` the solution on .NET 10.
2. Confirm `PlangConsole/bin/Debug/net10.0/plang.exe` runs (`plang.exe --help` or `--version`).
3. Pick one known-simple test goal and run `plang --build` on its folder — smoke that the builder works end-to-end.
4. If any of these fail: stop, write findings into `v1/plan.md` or a blocker file, escalate to coder.

### Pre-baseline snapshot (optional, awaiting approval)

1. `plang --build` from project root (full tree).
2. `plang --test={"format":"junit","timeout":5}` from project root.
3. Parse `.test/results.json` + `.test/junit.xml`.
4. Write `v1/pre-baseline.md` — counts only + list of failing paths. No root-cause analysis.

### Phase 1 — Tests/ folder restructure

Per architect's `folder_structure.md`. I'll commit **one bucket per commit** so a single bad move can be reverted without unwinding others.

**Order** (architect didn't specify; I'll go simple-first to catch mechanics issues early):
1. Delete `Tests/Runtime2/` (orphan).
2. Create new top-level dirs: `Tests/Modules/`, `Tests/App/`, `Tests/Builder/` (empty).
3. `git mv` each old bucket to new location, in alphabetical order of old name:
   Actor, Assert, Builder, Cache, CallStack, Condition (with regrouping), ContextVars, Crypto, DeepNavigation, Error, Event, File, Foreach, FromJson, GoalCall, Http, Identity, ListOps, Llm, Loop, Math, Mock, Output, RecursionDepthLimit, Retry, ReturnMapping, Settings, SetupGoal, Signing, StartupParams, StepResult, TestModule, Ui, Variable.
4. Per bucket:
   - `git mv` old → new path.
   - Grep the moved subtree for any `goal.call` path literals or `startGoal` references that point outside the subtree; update if any found.
   - `plang --build` on the new path. Read a sample `.pr` to confirm builder isn't breaking.
   - `plang --test` scoped to new path (if scope is supported) or full suite with filter.
   - `git add` + commit: `Tests restructure: <OldName> → <NewName>`.
5. Condition/ regrouping — architect calls for If/, Compound/, Operators/, Files/ subdirs. This is more than a pure move; I'll do it in its own commit after the plain Condition rename, so the file-move and the reorganization are separable in history.
6. Loop/Foreach merge — `Foreach/` becomes `Modules/Loop/Foreach/`; any existing `Modules/Loop/` contents merge. Separate commit.
7. Final full-tree `plang --build` + `plang --test` to catch anything cross-cutting I missed.

**`.build/` folders** — per architect's notes they regenerate on rebuild. I won't manually delete them before move; `git mv` on a folder moves what's tracked, and `plang --build` refreshes `.pr` files at the new paths.

**Stop-and-escalate conditions in Phase 1:**
- `plang --build` errors on moved bucket — might be a path-resolution bug in the builder → escalate.
- Moved tests fail that were green pre-move — document in commit message, continue (architect will triage in Phase 4), don't revert the move.
- A bucket has broken `goal.call` path literals I can't cleanly update — document, escalate to architect for path strategy.

### Phase 2 — Baseline

1. Full-tree `plang --build` (with default cache behavior — non-cache rebuild is Phase 6 architect's job).
2. `plang --test={"format":"junit","timeout":5}` from project root.
3. Parse results, write `v1/baseline.md` with the architect's requested table:

   | Category | Count | Paths |
   |---|---|---|
   | Pass | N | — |
   | Fail — assertion | N | path + expected vs actual |
   | Fail — runtime error | N | path + error key/message |
   | Timeout | N | path |
   | Stale | N | path |
   | Build failure | N | path |

4. For each failing test include the raw data the architect needs for Phase 4 triage: step text, `.pr` excerpt, runtime error or assertion delta.

## What I will NOT do

- Not fix any failing test. That's Phase 4/5 (architect + coder + possibly me back for test re-authoring).
- Not manually edit `.pr` files.
- Not change handler code.
- Not regroup Condition/ files beyond what `folder_structure.md` prescribes.
- Not tag any test as skipped/excluded. Architect decides that in Phase 4.

## Deliverables on this branch at end of v1

- `v1/plan.md` (this file, reviewed)
- `v1/pre-baseline.md` (if approved)
- `v1/baseline.md`
- `v1/summary.md`
- `v1/changes.patch`
- Separate git commits per moved bucket (traceable history)
- `.bot/runtime2-green-plang-tests/test-report.json` updated with Phase 2 counts

## Risks / open questions

1. **Pre-baseline — yes or no?** (One question, answer approves or skips).
2. **Scope filter for `plang --test`** — does the test runner accept a path filter so I can run only the moved subset per bucket? If not, per-bucket re-runs will re-run the entire suite. I'll check the CLI flags during Phase 0 and mention in plan update if scoping isn't available.
3. **Build cache during restructure** — if `plang --build` uses LLM output cache (it does by default), the builder shouldn't burn fresh LLM calls on every bucket move. Architect's Phase 6 final run uses `cache:false` for the real fresh verification — that's not my job.
4. **Commit volume** — ~34 buckets ≈ 34 commits. Noisy but traceable. I think that's the right trade-off; say if you want me to batch instead.

## Estimated wall-clock

- Phase 0: ~5 min.
- Pre-baseline (if approved): ~10–20 min.
- Phase 1: ~60–90 min mostly waiting on `plang --build`/`--test`.
- Phase 2: ~20 min (includes writing baseline.md).

Approve the plan (and say yes/no on pre-baseline) and I'll start.
