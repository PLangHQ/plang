# codeanalyzer summary — event-action

**Version:** v1. Verdict: FAIL, coder next.

**What this is:** the review of the event-action redesign (6703697fb..3266637da): events held on the actor that bound them, a list per item made on its first ask, system events loaded lazily, `variable.set` reading a reference once.

**What was done:** bench against the parent (runtime2 6703697fb): births in band, sub request 496 → 549 KB and root request 3840 → 4429 KB (both out of band), held flat. A probe (`Bench events`) puts the sub app's extra bytes on the system-events load: 50.6 KB for the system actor and 28 KB for the user, even where `system/event/` is absent. Findings by pattern in `review.md`: the load's cost (11/5), `actor.event` beside the `app.event` door (12), the moment-name question answered at three sites (12), `On(Catalog)` asked twice per action (12), `set.cs` half-applied read-once and its 145-line typed branch (12, return early), low items.

**Next:** the architect rules the price of decision 16 at this size; the coder fixes 1, 4, 5, 6; I re-measure the sub and root request. Then the queued call-cost job (`.bot/event-action/codeanalyzer/call-cost.md`).

**Example:**
```csharp
if (typeValue != null)        // set.cs:171, 145 lines, every path returns
{ ... }
return await name.Set(Binding(name, value), Context);   // the tail it should be the early return of
```
