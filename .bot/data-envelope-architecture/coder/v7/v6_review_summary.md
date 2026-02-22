# Tester v8 Review of Coder v6

## Resolved
- **v7 Finding #2**: GetChild depth through MemoryStack — test added, verified honest
- **v7 Finding #3**: fromJson deep nesting — tested (hits STJ MaxDepth=64 before our limit=128)
- All 4 code analyzer cross-concern fixes verified clean

## Still Open
1. **Major (blocking)**: `ResolveVariablesInPath` cycle detection has zero test coverage. `_resolvingVars` HashSet cycle guard is a security feature with no regression test. Open since v7.
2. **Minor**: `Clr()` depth boundary not tested at 20/21 (existing test uses 25, but no boundary test).
3. **Minor/observation**: `JsonDepthExceeded` catch in fromJson.cs is unreachable — STJ's MaxDepth (64) < our limit (128). Defensive code, not a bug.

## Carry-forwards from v6
- Thread safety concurrent test (major)
- Inner context in RehydrateNestedData (minor)
- Numeric type widening through compress/decompress (minor)
