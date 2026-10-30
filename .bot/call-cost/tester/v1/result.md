# tester v1 on call-cost — stages 1 and 2 (7bf093245..1878bef03) — PASS, with one major test gap and one process violation

Pattern first: made on the first ask (11); a birth fact, not a search (5); a run never writes an object its app shares.

## Run (clean tree, `./dev.sh build` first)
- C#: Modules 1500, Types 1042, Wire 641, Data 881, Generator 191, Runtime 1494 — all green.
- plang `--test` from `test/`: 323 total, 322 pass, 0 fail, 0 stale, 1 skipped (DurationPartsAndCompare — the same skip runtime2 carries). `git status` clean after.
- **Process violation:** `.bot/call-cost/coder/baseline-failures.txt` is missing. I did not reconstruct it. Nothing is red, so no name can be new red; the skip equals runtime2's.

## Probes (8, `.bot/call-cost/tester/TesterCostProbeTests.cs.txt`, not in the tree) — all pass
- P-A: 64 branches write a frame's first local at once behind a barrier → all 64 land.
- P-B: a frame read with nothing written: Holds/TryGet false, Error null, Tags asked by 32 branches → one object; Parameter and Errors are one object each, Errors empty.
- P-B2: Record on a frame whose Errors was never asked → Error is that error, once, however often recorded.
- P-C1/C2/C3: a goal judged at its first start, then (1) its Step list replaced by one holding a broken step, (2) a broken step added, (3) a step refused and then fixed → GoalBroken at the next start, then a clean start after the fix. (They pass because the step's Error write calls `Changed`, whichever door the step came in by.)
- P-D1: `{number, int}` is one object from `Kinded` asked twice, from a sub app, and as a number's own Type.
- P-D2: a sub app's own type with a kind of its own: the kinded type holds the layer's kind object, is one object on a second ask, a sibling sub app never has it, the parent never has the type.
- Mutated types: `type.kind` and `Template` are `init`-only, and no site writes either after birth (grep over PLang/): nothing can mutate a shared type through them.

## Mutation (reverted, tree clean)
`Parameter` and `Tags` made with a plain check-then-assign (not `LazyInitializer`): my P-A and P-B go red. BirthTests (18), the CallStack tests (35) and the rest stay green.

## Findings
1. **Test gap, major (11): the first ask under parallel branches is guarded by no shipped test.** The lazy `Parameter`/`Tags` are thread-safe only because of `LazyInitializer`; replacing it with a racy lazy keeps every committed test green. Add P-A and P-B (a frame's first local from many branches; Tags from many askers is one object). The plan names "parallel branches writing a frame's first local" as the risk, and the coder's BirthTests only pin bytes.
2. **Minor, test shape:** the BirthTests byte pins use loose ceilings (e.g. 2,000 B for a frame measured at 1,544; 9,700 for a context). A regression of a few hundred bytes passes. Tighten to measured + noise if the stage is to hold its gain.
3. **Minor, process:** missing `baseline-failures.txt` (above).

Not run by me: the bench (the codeanalyzer's); a sub app's step cache made at its first ask is pinned by the coder's `ASubAppsStepCache_IsMadeAtItsFirstAsk`, which I read but did not mutate.

Verdict: **PASS** (nothing red, no false green in the shipped behavior), finding 1 to fix before the stage is called done.
