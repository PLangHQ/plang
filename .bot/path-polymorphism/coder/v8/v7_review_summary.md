# tester v7 review of coder v6/v7 — summary

**Verdict:** NEEDS-FIXES. 5 of 6 v5 findings honestly closed (two
mutation-verified). 5 new gaps, all missing-coverage on surfaces the v6 fix
+ typed-returns sweep introduced.

| ID | Class | Surface | Tester's ask |
|---|---|---|---|
| F4 | major | parked `.test.goal2` | Rename to `.test.goal`, drop stale `.pr` |
| N1 | major | `GoalCall.GetGoalAsync` slash walk | 4 canonical cases |
| N2 | major | `builder.actions` Actions filter | 3 cases (named/empty/unknown) |
| N3 | minor | inverted `File.Exists` in `Builder.RunAsync` | NoAppFound headless guard |
| N4 | minor | `Action.ReturnTypeName` | Representative slice + sanity sweep |
| N5 | process | missing `baseline-tests.md` | Write it this round |

What this v8 does: tests-only. Adds 17 new C# tests + one renamed PLang
`.test.goal`. No production code touched. New baseline: C# 2906/2906,
plang 204/204, 0 stale.
