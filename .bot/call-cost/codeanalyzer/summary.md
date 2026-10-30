# codeanalyzer summary — call-cost

**Version:** v2. Verdict: PASS, tester next (v1 stage 1 PASS; v2 stage 2, 4dcfa3f36..1878bef03).

**What this is:** the call-cost branch makes the runtime cheaper per call, stage by stage (`.bot/event-action/codeanalyzer/call-cost.md`). Stage 1: lazy step cache, frame `Parameter`/`Tags`/`Errors`, `Broken()` once. Stage 2: a type is found, not made (`type.Kinded`, number's births with the app's type, `Depth`/`Count`/readers by reference).

**What was done:** bench at each hash. A goal run again: 227 → 201 → 145 KB (−36%); sub request 441 → 371 → 313; sub birth 101 → 54; held flat. Review in `review.md` (v1, v2): stage 2's findings are `list.Count` throwing for a bare-typed list (probed; latent), and the 127 `new type(...)` sites left, of which `bool`, the number reader, the dict/list readers and `data.Create` are hot and not yet found.

**Next:** measure each stage on its own hash (`Tools/Bench/run.sh`, `Bench repeat`, `Bench phases`, `BENCH_TICKS=1 repeat` for the types left).

**Example:**
```csharp
internal @this Kinded(kind.@this kind) => Types!.Made(this, this, kind.Name);   // the type for a name and a kind, found; no object made to ask for it
```
