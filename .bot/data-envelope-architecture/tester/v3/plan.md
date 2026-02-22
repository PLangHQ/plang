# Tester v3 Plan — Verify Fixes + Phase 3

## What I'm testing

1. Verify coder's fix commit addresses v2 critical/major findings
2. Analyze Phase 3: Data partial class split + Out view (8 new tests)
3. Run full test suite

## Key checks

- Add() now updates _allKinds/_mimeToKind — verify with new tests
- Kind(null)/Mime(null) null guards — verify tests pass
- Name() backtick strip — verify with HashSet<string> and SortedSet<int> tests
- BuilderNames/ComplexSchemas — verify test coverage
- Phase 3 partial split — verify no behavior change (all existing tests still pass)
- Data.Envelope — verify Signature/Verified properties and [Out] attribute
- View.Out — verify enum and attribute exist
