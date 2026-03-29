# Coder v4 Plan — Fix Auditor Findings

Address all 6 auditor findings (2 major, 2 minor, 1 nit, 1 behavioral).

## Fixes

### 1. MaxToolCalls batch-overshoot (MAJOR)
Slice toolCalls to remaining budget before execution: `toolCalls.Take(MaxToolCalls - toolCallCount)`.

### 2. Empty Data.Ok() on loop exit (MAJOR)
Return last content + metadata (Model, ToolCallCount, Truncated=true) instead of empty Data.Ok().

### 3. Numeric boxing inconsistency (MINOR)
RestoreFromCache: TryGetInt32 → TryGetInt64 to match ParseToolArguments.

### 4. MaxToolCalls test assertions (MINOR)
Exact CallCount with round-by-round documentation. Assert Truncated property.

### 5. Redundant null ternary (NIT)
`action.OnStream != null ? action.OnStream : null` → `action.OnStream`.

### 6. ParseToolArguments silent empty (NIT)
Surface JsonException as a __parse_error__ Data entry so it flows as tool error text.
