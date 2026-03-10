# Test Designer v1 — runtime2-builder-onerror-fix

## Assessment

The architect's plan specifies 6 PLang test suites + a BuildGoal.llm prompt improvement. After merging runtime2 (which includes the `runtime2-plang-test-gaps` merge), all 6 test suites already exist with full .build/ artifacts.

## Coverage Analysis

| Architect's Test | Existing Coverage | Status |
|---|---|---|
| ErrorCall (on error call GoalName) | `Tests/Runtime2/ErrorCall/` — verifies error message reaches handler | COVERED |
| ErrorChain (error propagation through chain) | `Tests/Runtime2/ErrorNested/` + `ErrorChain/` — nested goals + retry chain | COVERED |
| ErrorProps (error variables in handler) | `Tests/Runtime2/ErrorProps/` — message, key, statusCode | COVERED |
| ErrorTypes (ignore + call + retry combos) | `ErrorHandling/` (ignore) + `ErrorTypes/` (throw shape) + `Retry/` (retry+ignore, retry+call) | COVERED |
| ErrorInHandler (handler itself throws) | `Tests/Runtime2/ErrorInHandler/` — outer handler catches propagated error | COVERED |
| CacheDynamicKey (cache returns stale data) | `Tests/Runtime2/CacheDynamicKey/` — stale value via unresolved cache key | COVERED |

## Additional existing coverage beyond architect's spec

- `ErrorOrdering/` — retry ordering (retry first, then call)
- `ErrorNested/` — inner goal has own handler, outer goal completes
- `Cache/`, `CacheKey/`, `CacheSliding/` — cache variants
- `ConvertErrors/` — error handling in convert operations

## Conclusion

No new PLang tests needed. No C# tests needed (the fix is a builder LLM prompt change — C# tests can't validate LLM behavior).

The only remaining deliverable is the BuildGoal.llm prompt improvement — a coder task.
