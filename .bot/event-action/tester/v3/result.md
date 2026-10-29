# tester v3 on event-action — stage 5 (6703697fb..5d0ac4382+) — FAIL

Pattern first: **one door / the value answers (12)** and **a birth fact, not a search (11)**; **held memory never grows with the run**.

## Run (clean tree, `./dev.sh build` first)
- C#: Modules 1495, Types 1039, Wire 641, Data 877, Generator 191, Runtime 1443 — all green, 0 failed.
- plang `--test` from `test/`: 320 total, 319 pass, 0 fail, 0 timeout, 0 stale, 1 skipped (DurationPartsAndCompare — in the baseline). `git status` clean afterwards (no .pr rewritten).
- Baseline (`coder/baseline-failures.txt`): no red carried; no new red names.

## Findings (by pattern)

1. **Pattern 12 (one answer) — major, confirmed.** A variable event bound through the plain `event.Add(event)` (what `on.event` uses, for `after %count.on.set%`) never fires. `Add(name, event)` is the only door that records the name in `_named`; `On(name)` consults `_named` only, and falls to the variable type's list, which the event's Item (variable `count`) does not belong to. Probe P1 (`TesterProbeTests.cs.txt`): plain `Add(code event on variable count, "set")`, then `Variable.Set("count",1)` → 0 runs, expected 1. Not a regression (the old `Level` made a throwaway), but a bind that silently never runs, reachable by the path `%count.on.set%` (item `Get` → `On(this)` hands any item its moments). Also: `%count.on.set%` read makes an `_on[variable count]` list beside `_variable["count"]` (a variable's equality is now its name) — two lists for one name, and one more `_on` entry per name ever read through `.on` (grows with names). Fix: one door — bind on a variable item records its name (Add decides from `event.Item is variable`), `On(item)` of a variable answers `On(name)`; or refuse `on` on a variable. Test: P1 red→green.
2. **Test 3 of the held set (a bind after the name's list loaded joins it) — major, confirmed false green.** Mutation (Made drops `_variable.Values`, reverted): all 5 `VariableEventsTests` and all of `CollectionEventsTests` stay green; my P4 (bind `a`, set, bind `b`, set → a,a,b) goes red. Every coder test binds before the first set. Add P4.
3. **Pattern 11 — first ask, minor/coverage.** Covered by my probes, all pass: first ask is a set on a fresh app (P6); 50 concurrent first sets (P7); name case (P3); remove fires the name's remove (P5); a set made inside count's own event (P2, ≤2 runs, no loop). **Not run:** a sub app's first ask, a `Restore`, a first ask from a goal start. The coder's tests have no first-ask-from-a-caller-kind case at all; add at least sub app and restore.
4. **Cost, minor.** `Named()` is unchanged: a fire still parses `%name%` (28.7 KB measured on the old path). Test 4 only measures a miss (a set of another name), which is cheap now; a hit and a type-level event on every set still pay the parse. Not measured on this branch; the suggestion in variable-events.md (parser answers a bare root name cheaply) stands.
5. **Load failure is sticky, minor.** `Load` sets the exception on `_load`; every later ask returns the faulted task, so a throw in one system-event load fails all the actor's sets/goals for good. Probably unreachable (`Run` catches goal failures); say so or reset.

Held tests: 1 (bind makes no list) — not asserted by the coder's tests directly (bytes test 4 only); 2 — partly (P2,P6,P7 mine pass); 3 — finding 2; 6 — the order test passes; 7 — `UnbindingTheLastEvent…` passes and kills the entry.

Probe file (not in the tree): `.bot/event-action/tester/TesterProbeTests.cs.txt`. Mutation reverted, tree clean.

Verdict: **FAIL** — findings 1 and 2.
