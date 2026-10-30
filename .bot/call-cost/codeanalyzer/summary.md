# codeanalyzer summary — call-cost

**Version:** v3. Verdict: PASS, tester next (v1 stage 1, v2 stage 2, v3 stage 3 at b274f0406).

**What this is:** the call-cost branch makes the runtime cheaper per call, stage by stage (`.bot/event-action/codeanalyzer/call-cost.md`). Stage 3: a kept settings answer (by version), `shortcut.Name` a birth fact, `goal.list.Find`'s candidate lists kept until a goal comes in, a frame's `Children`/`property` made at first ask.

**What was done:** bench at each stage's hash. A goal run again: 227 → 201 → 145 → 109 KB (−52%); sub request 441 → 371 → 306 → 284; sub birth 101 → 54; held flat. Reviews in `review.md` (v1–v3). Stage 6 (the type list's tables, names only, read at first ask) is scoped in `startup-tables.md` with a prototype (names-only ~2 ms vs `typeof` tables ~40 ms vs the scan) and the DLL check; prototype sources in `prototype/`.

**Next:** measure each stage on its own hash (`Tools/Bench/run.sh`, `Bench repeat`, `Bench phases`, `BENCH_TICKS=1 repeat`). The per-call `Func` site is `type/reader/this.cs:101`.

**Example:**
```csharp
Find(_runtimeTyped, Shipped.Tokens, k => (typeName, k, view), kind)   // a closure and a delegate per reader lookup; the keys are data
```
