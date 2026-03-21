# Tester v1 Plan — runtime2-builder2-signing

## What I'm testing
Coder v1 fixed 7 OBP violations across signing, crypto, identity, and provider modules. 1795 C# tests pass. 15 PLang signing test goals exist.

## Approach
1. Run full C# test suite — record pass/fail counts
2. Run coverage on changed files
3. Analyze test quality:
   - Deletion test: for each code path, does a test catch it?
   - Weak assertions: `IsTrue`/`IsFalse` without checking Error.Key
   - False greens: tests that pass regardless of implementation correctness
   - Missing edge cases: null inputs, empty collections, error paths
4. Check PLang test existence for new modules
5. Write test-report.json with findings
