# tester v2 — call-cost stages 3–4 (5f22f6b79) — PASS

Patterns: 11 (made on the first ask), 5 (a birth fact), 12 (one answer), "a run never writes an object its app shares".

## Suites
- C# (`./dev.sh test`): Modules 1500, Types 1047, Wire 641, Data 881, Generator 191, Runtime 1502 — all green.
- plang `--test` (from `test/`): 323 total, 322 pass, 0 fail, 0 stale, 1 skipped (the baseline skip). Git tree clean after (no `.pr` rewritten).
- `coder/baseline-failures.txt` is present now: no failing names. No new red names.

## Probes (`TesterCostProbe2Tests.cs.txt`, run from `PLang.Tests/Runtime/App/Context/`, removed after)
| | asks | result |
|---|---|---|
| S1 | set then clear: context and child answer "not set" again | pass |
| S2 | a child's own value answers for it alone (sibling, parent keep "not set"); clear; a grandchild reads it | pass |
| S3 | across actors: user's value never answers for the system's; system's answers for user's | pass |
| S4 | a sub app and its maker keep their own answers either way | pass |
| S5 | absent `Decimals` follows the setting, then the default; a given 0 beats a setting | pass |
| F1 | goal added after a Find kept its candidates is found (beside, far) | pass |
| F2 | same-.pr goal replaced after a Find: the new one | pass |
| F3 | 16 adds racing 16×50 finds: no exception, every goal found after | pass |
| F4 | a beside goal added after a Find still beats a newer far one | pass |
| R1e | the kept resolver is the module's own app's System context, same object before and after a sub app's run, neither sub nor root | pass |
| R2 | 32 threads at a class's first run: one answer | pass |
| R3 | unknown property refused on every run | pass |
| R1, R1d(×2) | weak ref to a disposed sub app after an action ran in it | red alone, **green in the full sweep, and red the same at 1878bef03** — a GC-timing artefact of my probe, not a finding; R1e is the sound form |

## Mutations (announced; each reverted; `git status` clean)
- Drop `_found = null` in `goal.list.Add`: my F2 and F4 red, and shipped tests red too (4 in Runtime/Modules).
- Drop `Move()` from `Save`/`Remove`: 60 shipped reds — well guarded.
- `Keeper` ignores a scope's own values: my S2 red and 5 shipped option tests red — guarded.
- `IsRead` → `return true`: **every suite stayed green.** See finding 1.

## Findings by pattern
1. **Minor, pattern 12 / dead guard.** `IsRead` (`actor/setting/this.cs`) can be dropped with nothing red. By reading, `Read` ends with `Move()`, so an answer kept during the rows' read is stale at once: the guard looks redundant. Remove it, or pin the window.
2. **Minor, pattern 5.** `AGoalFoundAgain_ReadsTheKeptGoals` pins bytes only. A stale `_found` is caught by distant tests, not a named one. F4 is the pin to add.
3. **Minor, pattern 11.** The generated `Resolve` for a required parameter that is given but doesn't resolve reads the step's row twice (the `HasValue` branch, then the final `else`'s `__View`). A row whose path calls a method reads twice. Not exercised: I could not observe the count through a shipped action. Reuse the first read.

Not covered by anything: a Save/Remove row's effect on a kept option is covered only via 60 incidental reds, no named test (fine, caught).
