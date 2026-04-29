# Plan — Get All PLang Tests Green (v1)

## Goal

162 `.test.goal` files under `Tests/` — we want a clean run of `plang --test`: **0 Fail, 0 Timeout, 0 Stale**. `Skipped` only when a test is intentionally tagged out (network/llm) *and* the reason is documented.

Branch: `runtime2-green-plang-tests` (cut from `runtime2` at `c51dcebb`). Single branch, no PR — merged at the end.

## Why this is a fleet problem

Two large changes recently landed on `runtime2`:

1. **Action modifiers** (`250d3878`) — `onError`/`cache`/`timeout` became per-action wrappers. `.pr` shape changed for every test using them.
2. **Testing module merge** (`c51dcebb`) — assert/condition/discover/run/report/elseif/notContains, plus handler behavior tweaks.

Expect a large `Stale` cohort, a smaller builder-mapping cohort, a long tail of real runtime bugs, and a handful of authoring bugs. Drain them in that order because each pass unblocks the next.

## Triage protocol — IMPORTANT

When a test fails, **we do not assume the handler is right and the test is wrong, nor the reverse.** Architect presents four things to the user:

1. The PLang step text from the `.goal`
2. The `.pr` the builder produced
3. What the handler actually did (output/error)
4. What the test expected

User confirms: test wrong, handler wrong, builder wrong, or environmental. Only then is the fix dispatched. No silent "fix the test to match" or "fix the handler to match."

## Phases

**Restructure first, then tests.** Fixing tests in the old sprawl while planning to move them is wasted motion. We reshape Tests/ under the rule from `folder_structure.md`, then baseline on the new tree.

### Phase 0 — Build prerequisites — **tester**

- `dotnet build` the solution on .NET 10.
- Confirm `PlangConsole/bin/Debug/net10.0/plang.exe` runs.
- Smoke `plang --build` on a single known goal.

**Stop:** if build breaks, escalate to coder. No further work until the runtime compiles.

### Phase 1 — Tests/ folder restructure — **architect proposes (done), tester executes**

See `folder_structure.md`. All operations via `git mv` to preserve history. One commit per top-level bucket so reverting is clean. Delete `Tests/Runtime2/` outright.

Per-bucket procedure:
1. `git mv` into new layout.
2. Update any `.goal` files that call other goals by path literal (cross-directory calls may break; in-subtree relatives survive).
3. `plang --build` on the moved subset — regenerates `.pr` files at new paths.
4. `plang --test` on the moved subset — confirm no regression from the move itself.
5. Commit.

Finish with a full-tree `plang --build` + `plang --test` to pick up anything missed.

### Phase 2 — Baseline — **tester**

1. `plang --build` from project root.
2. `plang --test={"format":"junit","timeout":5}` — 5s per test. Normal tests are <1s; 5s catches genuine slowness without masking regressions. Tests that legitimately need more (llm, slow http) get a per-test timeout override or a tag-based exclude.
3. Read `.test/results.json` + `.test/junit.xml`.
4. Write `.bot/runtime2-green-plang-tests/architect/v1/baseline.md` — categorised counts + path list per category:

| Category | Count | Paths |
|---|---|---|
| Pass | N | - |
| Fail — assertion | N | path + expected vs actual |
| Fail — runtime error | N | path + error key/message |
| Timeout | N | path |
| Stale | N | path |
| Build failure | N | path |

**Decision point.** Architect reads baseline, recommends next phase. If Pass >90%, direct triage. If lower, prioritize the dominant category first.

### Phase 3 — Drain Stale — **tester**

Rebuild; re-run. Any test still `Stale` after two clean rebuilds = hashing or non-determinism bug → escalate to coder.

### Phase 4 — Triage — **architect (me) + user**

For each remaining Fail/Timeout:

- Tester provides the raw data (`.pr`, `.goal`, runtime error or assertion delta).
- Architect classifies candidate: stale / builder-mapping / runtime / authoring / env.
- For every test, architect presents the four-thing protocol (step / `.pr` / handler result / expected) to the user for confirmation. Clusters of same-root-cause tests can be confirmed together.
- Output: `triage.md` — one row per failing test: path, category, root cause, proposed owner.

Network/LLM tests are **not tagged out by default**. Run them, report failures, show the user.

### Phase 5 — Dispatch — **coder + tester + architect**

Based on triage:

- **Runtime-handler bugs** → coder, clustered by module (e.g. "condition/elseif + else", "http.download timeout modifier"). One coder session per cluster.
- **Builder-prompt changes** → architect drafts the prompt edits + examples, coder implements, full rebuild + re-test. Batched because prompt changes force a rebuild of everything.
- **Test-authoring bugs** → tester re-authors (only when user confirmed the test was wrong). Then rebuild + re-run that subset.
- **Tag additions** (env deps) → architect drafts the tag list, tester applies to `.goal` files.

After each dispatch batch, tester re-runs the affected subset (or the whole suite if a prompt change happened).

### Phase 6 — Iterate — **tester + architect**

Loop 4→5 until Fail/Timeout/Stale all zero. Final confirmation:

1. `plang --build={"cache":false}` — fresh LLM calls, verify prompt changes are stable.
2. `plang --test` — green.
3. `v1/summary.md` — final counts, list of intentionally `Skipped` with reasons.

### Phase 7 — Final audit — **auditor**

Auditor reviews the branch: final green state, triage decisions that became code changes, any tests tagged-out, structural coherence of the Tests/ tree.

## Bot pipeline summary

| Phase | Owner |
|---|---|
| 0 Build | tester (escalate to coder) |
| 1 Tests/ restructure | architect proposes (done), tester executes |
| 2 Baseline | tester |
| 3 Drain Stale | tester |
| 4 Triage | architect + user |
| 5 Dispatch — runtime bugs | coder |
| 5 Dispatch — builder prompt | architect drafts, coder implements |
| 5 Dispatch — test re-author | tester |
| 5 Dispatch — tags | architect drafts, tester applies |
| 6 Iterate | tester + architect |
| 7 Final audit | auditor |

## What this plan is NOT

- Not hand-editing `.pr` files.
- Not assuming the handler is right. Every disagreement goes through the user.
- Not tagging tests out to hide bugs. Tagging is only for genuine env deps.
- Not one commit. Phases produce separate commits; everything lands on this branch; user merges at end.

## Deliverables on this branch

- `baseline.md` — starting state
- `triage.md` — per-test classification + owner
- `folder_structure.md` — Phase 6 proposal
- `summary.md` — what happened, final counts
- Code commits by phase/cluster
- Final `plang --test` output showing green

## Open questions — CLOSED

- ~~Scope~~ — `Tests/**/*.test.goal`, includes TestModule, includes setup.
- ~~Timeout~~ — 5s default; flag >1s; per-test override for legit slow cases.
- ~~Network/LLM~~ — run them, show failures to user.
- ~~Delegation~~ — coder owns runtime fixes.
- ~~PR strategy~~ — single branch, user merges at end.
- ~~Folder case~~ — PascalCase for folders + files (callable goals convention). Fixtures stay lowercase.
- ~~File naming~~ — keep `{scenario-name}.test.goal`, multiple per folder OK.
- ~~Restructure timing~~ — first (Phase 1), before baseline.
- ~~`Runtime/` vs `App/`~~ — `App/` (matches c# `PLang/App/`).
- ~~`Builder/` placement~~ — module-action tests under `Modules/Builder/`, top-level `Builder/` reserved for pipeline-level integration tests.
