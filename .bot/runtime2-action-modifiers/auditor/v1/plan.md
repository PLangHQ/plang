# Auditor v1 Plan — runtime2-action-modifiers

## Context

Action modifiers branch promotes `onError`, `cache`, and `timeout` from step-level properties to per-action modifier actions using `IModifier.Wrap()` pattern, right-to-left fold, and builder grouping pipeline.

Previous reviewers:
- **Codeanalyzer v1**: PASS — 0 OBP violations, 1 medium (error.handle silent success), 3 low
- **Tester v4**: PASS after 4 rounds — full coverage achieved
- **Security v1**: PASS — 1 medium (GoalCall race), 3 low (negative Ms, unbounded retry, non-thread-safe stack)

## Audit Focus

1. **Cross-file contracts**: Does Action.Modifiers survive serialize → deserialize → runtime? Does Step.Clone properly deep-copy modifier chains? Does GoalCall mutation in error.handle affect concurrent callers?

2. **Architectural fit**: Does the modifier fold pattern follow OBP correctly? Is the builder GroupModifiers pipeline properly integrated?

3. **Review quality assessment**: Did codeanalyzer miss anything? Are tester's tests actually covering the changed paths? Did security rate severity correctly?

4. **Foundation ripple**: Changes to Action, Step, Data affect everything downstream — verify no contracts broken.

## Steps

1. Read all prior bot reports (done)
2. Read core modifier runtime files (done)
3. Read builder + serialization pipeline (done)
4. Verify cross-file contracts: Clone family, GoalCall mutation, Modifiers deserialization
5. Run test suite to verify green
6. Write findings, verdict, and reports
