# codeanalyzer summary — event-action

**Version:** v4. Verdict: FAIL (narrow), coder next (v1 FAIL system-events load; v2 narrow FAIL first-ask mint, ruled explained by 69; v3 PASS at 584fef19c; v4 stages 6–8 and variable events on one store).

**What this is:** the review of 5d0ac4382..86d90db97: the variable events on one store (73), the type lookup keeping its answer (74), `%!app.variable["x"]%` (77) and Cover asking the variable (76), the equality sweep (72), `Sub`/`IsInheritable`, `app.parent.goal`, a goal pattern rooted as a path (84), `goal.App` born in the reader and `goal.Find(name, asker)`.

**What was done:** bench against runtime2 6703697fb: sub birth 101 KB, root birth 2267, sub request 441, root request 3898, held flat; all inside the bands, the old +28 KB gone. Findings by pattern in `review.md` (Review v4): 1 (12) the "caller or none" fork written twice (`goal/this.Item.cs:94`, `event/call/this.cs:72`) and five direct `goal.list.Find(name, caller)` sites beside the new door `goal.Find`; 2 (11) the variable made at every set and a linear scan per set; 3–8 lows.

**Next:** the coder collapses finding 1 (one door, or the architect rules the five direct sites stay); I re-read it. Then the tester, security (stage 8), auditor, docs.

**Example:**
```csharp
// both runtime sites
caller is { } ? await caller.Find(name, asker) : await asker.App.goal.list.Find(name, null)   // one ask, on one door
```
