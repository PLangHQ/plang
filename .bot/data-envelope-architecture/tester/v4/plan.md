# Tester v4 Plan — Phase 4 Envelope Pipeline

## What I'm testing

1. Run full test suite on Phase 4 code (coder v4)
2. Analyze 17 new envelope pipeline tests for quality
3. Check error path coverage in Decompress
4. Check RehydrateNestedData correctness
5. Verify round-trip data fidelity

## Key checks

- Decompress() exception handling: corrupt GZip data, invalid JSON
- Error path coverage: invalid inner Data, null byte[], corrupt compressed bytes
- RehydrateNestedData: multi-level nesting, edge cases
- Wrap/Unwrap context propagation
- Compress → Decompress round-trip: value, type, and metadata fidelity
- Properties lost through compression cycle
- Compress() called on unwrapped data behavior
