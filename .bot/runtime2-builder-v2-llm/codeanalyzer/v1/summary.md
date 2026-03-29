# Code Analysis v1 — Summary

## What this is
5-pass code analysis of the new LLM module on `runtime2-builder-v2-llm`. The module adds `llm.query` action with OpenAI-compatible provider supporting tools, caching, streaming, conversation continuity, and structured output.

## What was done
Analyzed 7 files (6 new, 1 modified). 6 of 7 files are CLEAN — the action record, types, provider interface, GoalCall additions, and provider registry changes all follow established patterns exactly. `OpenAiProvider.cs` (874 lines) has 3 blocking findings and 4 minor ones.

**Blocking findings:**
1. Bare `catch` blocks (lines 587, 765) silently swallow NullReferenceException/OOM — use negative catch filter or scoped `catch (JsonException)`
2. Sync-over-async `.GetAwaiter().GetResult()` in `ResolveConfig` (line 734) — make async

**Coverage gaps:**
3. `ParseToolArguments` default fill-in (lines 465-472) — zero test coverage
4. `MapPlangTypeToJsonSchema` type mappings — only `string` exercised

**Minor:**
5. Duplicate httpAction construction, dead BuildStreamProxy wrapper, decomposed params in ExecuteToolAsync

## Code example
The bare catch pattern that needs fixing (OpenAiProvider.cs:587):
```csharp
// Before (masks NullReferenceException, OOM, etc.)
catch { /* Fall through to base64 assumption */ }

// After (established PLang pattern)
catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
{ /* Fall through to base64 assumption */ }
```

## Files modified
None — analysis only.

## Status
**NEEDS WORK** — send back to coder for 3 blocking fixes + 2 test additions.
