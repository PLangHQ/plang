# Auditor v1 Plan — Final Audit Before Merge

## Scope
Final code integrity review of all changes on `runtime2-builder-onerror-fix` vs `runtime2`. This branch:
1. Renames `RetryOverSeconds` to `RetryOverMs` end-to-end (C#, .pr schema, builder prompt, tests, docs)
2. Strengthens `BuildGoal.llm` prompt with two CRITICAL rules (onError preservation, literal value preservation)
3. Adds 4 new PLang test suites (ErrorRetryOnly, ErrorGoalFirst, ErrorMixed, OnErrorMultilingual)
4. Rebuilds all .pr files via plang builder (v0.2 format)

## Pipeline Context
- codeanalyzer v2: PASS
- tester v2: PASS (67/68 PLang, 1511/1511 C#, only pre-existing ConditionCompound failure)
- security v1: PASS (no critical/high findings)

## Review Checklist
1. OBP compliance of C# changes (ErrorHandler.cs, Methods.cs, GoalMapper.cs)
2. Rename completeness — any stale RetryOverSeconds references in production code?
3. Retry calculation correctness in Methods.cs
4. Builder prompt accuracy — do examples match the schema?
5. Test quality — do PLang tests verify intent, not just pass?
6. .pr file integrity — rebuilt by builder, not manually edited?
7. GoalMapper conversion — ms-to-ms passthrough correct?

## Approach
- SKIP APPROVAL per instructions — implement immediately
- Read all changed files, verify pipeline results, write findings
