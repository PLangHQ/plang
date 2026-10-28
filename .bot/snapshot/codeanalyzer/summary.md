# codeanalyzer summary — snapshot

**Version:** v2. Verdict: FAIL (small), coder next.

**What this is:** review and bench of the per-app event list (941df01dd..0cd33f185): `app.event`, one list per app; a sub app is born holding its maker's bindings as `binding.inherited`; `actor.on` gone; Before/After in one pass.

**What was done:** bench row `snapshot-0cd33f185` (and a request-only rerun): sub birth 99 KB, root birth 2332 KB (edge of band), sub request 518 KB, root request 3813–3828 KB, held flat; the parent 6e9db97b4 measures 3829 root request, so the diff moves nothing. With the maker holding 4 bindings on 4 items, sub birth is 105 KB (+6, band 5), 108 KB with 16: the birth copy costs by event name, not by binding. `Tools/Bench/Program.cs` gained `BENCH_MAKER=n`. Findings in `review.md` ("Per-app event list"): 1 the birth copy's name sweep, 2 one `EachGoalIsTheAppsOwn` ptest flake under load (alone and in two quiet runs it passes), 3 dead `Inner` in `Reach`, 4–7 low (string door with `!`, inherited mirrors scope/filter, `declared` name, overload names).

**Next:** the coder takes 1, 3 and loops 2 under load; Ingi picks 4. Also noted for the architect: root request was 3555 KB at ab239e127 and is 3829 KB at the parent, +274 over the band, between 1314cd233 and 6e9db97b4 (no request row there). 10a's review waits for Ingi's yes.

**Example:**
```csharp
foreach (var name in events.Names)                          // 11+ names per bound item
    if (events[name] is { } @event)                         // a shared closed event for each name nothing is bound on
        foreach (var when in System.Enum.GetValues<When>()) // a new array per name
```
