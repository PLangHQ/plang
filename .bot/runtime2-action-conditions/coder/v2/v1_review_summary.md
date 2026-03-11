# v1 Review Summary

## Auditor Verdict: FAIL (2 major findings)

### Finding #1 (major): If.Run() doesn't catch evaluator exceptions
- `evaluator.IsTruthy()` and `evaluator.Evaluate()` can throw `NotSupportedException`, `ArgumentException`, `OverflowException`
- Method returns `Task<Data>` — must return Data on every path per project convention
- Fix: wrap in try/catch returning `Data.FromError(new ServiceError(..., "EvaluationError", 400))`

### Finding #2 (major): Compare.Run() has same issue
- Same unguarded evaluator call
- Same fix pattern

### Finding #3 (minor): WiderNumericType falls back to byte for unknown types
- Should fall back to decimal (safe widening) instead of byte (unsafe narrowing)

### Finding #4 (nit): No negative test for Compare not setting __condition__
- Compare is pure evaluation — should verify it doesn't set the signal

## Also noted by security review
- Finding #4: Compare() returns 0 for non-IComparable types — silently treats incomparable as equal in relational ops
