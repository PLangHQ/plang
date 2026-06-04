# tester — lazy-deserialize — v3 plan

## Scope
Validate test quality for coder v3 (lazy `Data`, signing round-trip, shallow-clone
binding). Codeanalyzer v2 already PASS'd; coder reports 272/272 plang + 4021/0 C#.

## Approach
1. Clean rebuild (stale-binary trap) → run both suites, confirm green + deterministic.
2. `.pr` builder-false-green check on every LazyDeserialize test (text vs action).
3. Verify the goal "smoke" tests' deferrals to C# strict tests are honest (the goal
   tests check *that* an error/invariant holds; the *exact* error/value is claimed to
   live in C#). Audit each claimed C# anchor for existence + assertion strength.
4. Hunt the untested half: `ead0caa83` changed `variable.set` AND `list.add` to
   ShallowClone. `list.add` got a regression (`SignedDataSurvivesInList`); the
   symmetric `variable.set` List/Dict arm did not. Probe it empirically.

## Result
PASS — see `result.md` / `verdict.json`. Suites green & deterministic, no builder
false-greens, all 9 C# deferral anchors strong, the one coverage gap probe-confirmed
benign (real behavior correct, just unpinned).
