# Tester v2 Plan — Phase 2 Context + Lazy Derivation

## What I'm testing

Coder v2 added Phase 2: Type gets context + lazy navigation (Kind, Compressible, ClrType), Data gets late-bound context, Variables/PLangContext stamp context. 23 new tests were added (6 KindOf, 12 Data, 5 Variables).

## Steps

1. Run full test suite — verify all pass
2. Verify Phase 1 findings carry forward (not fixed)
3. Analyze Phase 2 test quality:
   - Does context propagation chain have complete coverage?
   - Are new KindOf tests sufficient?
   - Do Data lazy derivation tests catch subtle bugs?
   - Is Methods.cs context stamping tested?
4. Check for new bugs introduced by Phase 2
5. Write test-report.json

## Key questions

- Does Add() update _allKinds and _mimeToKind? (KindOf depends on these)
- Is DynamicData Type derivation correct when no explicit type is given?
- Is the Methods.cs context stamping covered by any test?
- Are there race conditions in context stamping?
