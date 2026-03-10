# v1 Review Summary

User feedback on v1 assessment:

1. **Don't just validate the architect's list** — think independently about what tests the builder fix needs. The architect's plan is a suggestion, not a spec.
2. **`throw error + on error call` is invalid** — you can't throw and catch in the same step. That's not a real gap.
3. **Missing pattern: `on error retry X times over Y`** — with time in milliseconds, not seconds. Current prompt says seconds but it should be ms.

These corrections led to identifying 3 real gaps in the PLang test coverage for onError builder patterns.
