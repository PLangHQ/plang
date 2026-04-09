# Auditor v2 Plan — Recheck Coder v4 Fixes

## Scope
Verify all 6 fixes from coder v4, with fresh-code skepticism (don't just verify "does it match the suggestion").

## Checklist
1. MaxToolCalls slice — does the sliced assistant message stay consistent with tool results? Does the API see a valid conversation?
2. Loop exit result — does `lastContent` actually contain useful content, or is it null (since LLMs typically don't return content alongside tool_calls)?
3. Numeric boxing — TryGetInt64 in RestoreFromCache
4. Test assertions — exact CallCount, Truncated property
5. Ternary simplification
6. ParseToolArguments error — does the error propagate correctly through ExecuteToolAsync?
