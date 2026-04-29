# Architect v2 — Summary

## What this is

v2 handles tester v2's critical finding **F4c-1** — the five BuildGoal.llm prompt rules coder v1 landed are dormant. Full Tests/ rebuild regressed 38 tests; coder reverted. Tests the rules target still fail.

My output this session: a coder dispatch plan for a **surgical rebuild** — 11 currently-failing folders, rebuild each in isolation, hand-review the `.pr` output against the specific rule that should apply, keep or restore per folder. Bounded scope, no blast radius — worst case a folder stays failing since it already is.

## What was done

- Read tester v2's review and findings.
- Verified coder v2's F3-1/2/3 test anchors are in place (`settests.cs`, `VariablesTests.cs`) so the ground is stable for rebuilds.
- Walked the prompt diff (`ce0de138`) to confirm the five rules are semantically correct — the 38-test regression reads as LLM variance on cache miss, not bad rules.
- Drafted `v1_review_summary.md` capturing tester v2's F4c-1 framing + the architect+Ingi decision (accept option (a): surgical; log option (b) golden-evals as separate follow-up).
- Drafted `v2/plan.md` — full coder dispatch: the 11 target folders grouped by which rule they exercise, the expected `.pr` shape per folder, hand-review procedure, stop conditions, success criteria.
- Caught my own scope slip mid-session (started to run `dotnet build` myself) — Ingi corrected: **architect specifies, coder builds, tester tests**. Revised plan accordingly so coder owns all invocations and the hand-review.

## Key decisions captured in the plan

- **Invocation form**: from `Tests/` as root, `plang build '--build={"files":["<path>"]}'`. Ingi's stated pattern; naturally excludes `os/apps/*`.
- **Target list**: 11 folders — 3 Signing (modifier shape), 2 Loop (arithmetic), 1 DownloadFile (download+save), 3 Event (enum, belt-and-suspenders since W2 typed the handler), 2 Math/List (JsonParseError — diagnose-first, do not force).
- **Commit policy**: coder commits only what passes hand review; `git checkout -- <folder>/.build/*.pr` restores any rebuild that drifted from the expected rule.
- **Stop conditions**: any regression from the 128-pass set; more than 3 of 11 drifting; a single folder needing >1 retry + 1 cache-bust.
- **No speculative prompt edits during rebuild** — if rules drift, coder logs it, architect drafts edits in a *later* session with full data.

## Code example — the hand-review pattern

Each folder in the plan has a target shape. For `Tests/Modules/Loop`, `CountItem.goal` step `set %count% = %count% + 1`:

```
Expected after rebuild:
actions: [
  {"module":"math","action":"add","parameters":[{"name":"A","value":"%count%"},{"name":"B","value":1}]},
  {"module":"variable","action":"set","parameters":[{"name":"Name","value":"%count%"},{"name":"Value","value":"%__data__%"}]}
]

Currently in the .pr (the bug):
actions: [
  {"module":"variable","action":"set","parameters":[{"name":"Name","value":"%count%"},{"name":"Value","value":"%count% + 1"}]}
]
```

Coder compares literally. If the new .pr matches the "expected" shape → keep. If still the "currently" shape → restore + log.

## What's next

- **Coder v3** — executes the dispatch. Rebuild + hand-review + commit. Delivers per-folder rebuild log.
- **Tester v3** — re-baselines after coder v3, confirms count, checks for regressions, spot-checks rebuilt `.pr` files.
- **Architect v3 (me)** — Wave 6 triage on what's still failing after the surgical rebuild lands, with Ingi. The tail (UI render, ListOps2, Builder env tests, Condition/Compound scope, etc.) — none of which this rebuild touches.

## Files written

- `v2/v1_review_summary.md` — incoming review
- `v2/plan.md` — coder dispatch
- `v2/summary.md` — this file
- `v2/changes.patch` — diff (to be generated)
- `summary.md` (bot root) — updated with v2 entry

## What v2 is NOT

- Not implementation. Architect doesn't build.
- Not a prompt-rule edit session — that's a follow-up if the rebuild log shows rule weakness.
- Not golden-eval infrastructure — separate follow-up task.
- Not Wave 6 triage.
