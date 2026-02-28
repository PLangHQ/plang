# Security Learnings — runtime2-setup-goal v1

## 1. Substring error matching creates a permanent-skip risk

**Context:** `IsTolerableError` uses `message.Contains("already exists")` to tolerate idempotent DDL errors during setup.

**Lesson:** When tolerating errors by string matching, the consequence isn't just "wrong error handling for one run" — it's **permanent**. The tolerated step gets recorded as executed and will be skipped on ALL future startups. A false positive means the step never succeeds and never re-runs. This is qualitatively different from a one-time error tolerance.

**Implication:** Error tolerance in run-once systems should use more specific patterns (regex with word boundaries, error codes, or error types) rather than bare substring matching. The cost of a false positive is high because it's irreversible without manual database intervention.

## 2. Setup context propagation through goal.call is security-neutral

**Context:** `context.Setup` propagates through `goal.RunAsync` to called goals, giving them run-once semantics too.

**Lesson:** This is safe because `Steps.RunAsync` checks `context.Setup != null` before applying run-once logic. During normal execution (after setup completes), `context.Setup` is null, so non-setup goals behave normally. The key insight: the propagation only adds constraints (run-once), never removes them. It can't be used to bypass anything.

## 3. Throw-vs-Data pattern in CallStack.Push is safe but fragile

**Context:** `CallStack.Push()` throws `CallStackOverflowException` (not `Data`), but it's called on `Goal.Methods.cs:53` outside the try/finally block.

**Lesson:** This works because: (a) if Push throws, the frame was never pushed, so Pop in finally is a no-op on the correct frame; (b) the exception propagates up to `Step.Methods.cs:49`'s `catch(Exception)` in the caller. But it's fragile — if someone adds a code path that calls `Goal.RunAsync` directly without an outer try/catch, the exception would be unhandled. The pattern relies on callers catching it, not on the method itself being safe.

**Recommendation for future:** Consider making `CallStack.Push` return a `Data` result, or wrapping it in Goal.RunAsync's try block.

## 4. Carry-forward findings need tracking across branches

**Context:** The DeserializeValue `InvalidOperationException` gap was first identified on `runtime2-system-datasource` and carries forward to every branch that includes DataSource.

**Lesson:** When a finding persists across branches, it should be tracked in memory as a "standing issue" until fixed. Otherwise each security audit re-discovers and re-reports the same thing. Added to MEMORY.md as a standing item.
