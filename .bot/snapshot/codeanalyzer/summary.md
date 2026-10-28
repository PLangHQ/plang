# codeanalyzer summary — snapshot

**Version:** v3. Verdict: FAIL (one blocking), coder next.

**What this is:** the final review and bench before the snapshot merge: 0cd33f185..1268c81f7 (decisions 260–322): inherited bindings by level, the goal lookup's exact address and beside-caller first, shortcuts as files, `%!event%` saved, the two-number resume, frames holding their own position, sign's view, the datetime write, public-setter-only writes, plain rows bare in the snapshot view, the snapshot's one write door.

**What was done:** bench row `snapshot-1268c81f7` (`DOTNET_TieredPGO=0`): sub birth 99 KB, root birth 2352 KB, sub request 539–540 KB against 522 for the parent re-measured the same way (+18, band ±10), root request 3885–3893 (+41, band ±120), held flat, ptest 301/0, sweep red none. Bisected the sub request on bytes: d67194934 (each frame holds its own position) adds it. Cause proven with a probe (`Bench index`): the action frame's constructor runs `action.Index`, a linear search through `Count` (a `number` item) and `this[i]`, which allocates about 1.6 KB per call (11,264 B for one pass over the 7 actions of the request goal). Other findings in `review.md`: literal type names in the snapshot view's `Record`, `Under` as a late stamp, `Module`/`Name` copied on every live frame, `Goal()` materialized twice per `Find`, `_names` vs `Names`.

**Next:** the coder hands the place to the frame at birth (the chain loop knows `at`), then I re-measure the sub request (target ≤ 525 KB). The merge waits on Ingi's go.

**Example:**
```csharp
Action = action;
index = action.Index ?? -1;   // Step.Code.IndexOf(this): ~1.6 KB allocated per call, on every action pushed
```
