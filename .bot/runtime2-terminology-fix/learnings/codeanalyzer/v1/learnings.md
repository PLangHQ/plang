# Learnings — runtime2-terminology-fix Code Analyzer v1

## 1. Verify "zero remaining" claims independently

The coder summary stated "Zero remaining references to HandlerError in production/test/generator code." Independent grep found `ErrorInfoTests.cs:198,204` still using `"HandlerError"`. Always verify completion claims with your own search — self-reported metrics can miss edge cases, especially in test data that isn't exercising the renamed code path.

## 2. Test data is a rename surface

When renaming terminology, test data is a distinct surface from production code and test assertions. Production code produces the string, test assertions check for it, but test *data* (constructor arguments for mock objects) can use the old string without causing failures — the test still passes because it's testing formatting behavior, not the specific key value. This makes test data stragglers invisible to "does it build + pass" verification.

## 3. Mechanical renames are low-risk but not zero-risk

Pure find-and-replace renames across 140 files have minimal logic risk. The main risks are: (a) string literals in non-obvious places (source generator, error keys), (b) named tuple field renames that break call-site property access, (c) test data that uses old terminology as arbitrary values. The rename here was well-executed — only one straggler found.

## 4. Cross-branch scaffolder artifacts go stale

When branch A's scaffolder creates skeleton files with namespace references, and branch B renames those namespaces, the scaffolder artifacts on branch A become stale. This is expected — `.bot/` artifacts are per-branch snapshots, not maintained across branches. Flag for whoever merges, but don't treat as a finding.
