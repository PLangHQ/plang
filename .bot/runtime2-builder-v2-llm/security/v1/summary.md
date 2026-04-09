# Security Analysis v1 — Summary

## What this is
Security audit of the new LLM module (`query` action + `OpenAiProvider`) added on `runtime2-builder-v2-llm`. The module sends queries to OpenAI-compatible APIs with support for tool calling, caching, streaming, validation, conversation continuity, and image input.

## What was done
Full blue team (attack surface mapping) + red team (exploit sketches) analysis of all 7 attack surfaces:

1. **Tool call loop** — SECURE. Tool names whitelisted at line 373 via `Find()` against declared tools. Unknown names return error string, no execution. MaxToolCalls (default 10) caps iterations.

2. **Image file read** — MEDIUM. `ReadAllBytes` at line 551 has no size limit. OOM on multi-GB files. The OOM exception is explicitly excluded from the catch filter (line 572).

3. **Conversation continuity** — MEDIUM. No limit on accumulated message count. Long sessions grow unbounded.

4. **Regex on LLM output** — LOW (accepted). Simple patterns, no timeout, unlikely to backtrack catastrophically.

5. **API key handling** — Clean. Standard Bearer token, user-configured endpoint.

6. **Cache integrity** — Clean. SHA256-keyed, local SQLite, disabled with tools.

7. **JSON deserialization** — Clean. System.Text.Json throughout, all failures return error Data.

### Verdict: PASS — 0 critical, 0 high, 2 medium, 3 low

## Key findings

| # | Severity | Issue | File:Line |
|---|----------|-------|-----------|
| 1 | Medium | Image ReadAllBytes no size limit → OOM | OpenAiProvider.cs:551 |
| 2 | Medium | Conversation continuity no length limit | OpenAiProvider.cs:54-56 |
| 3 | Low | Tool error messages sent to external API | OpenAiProvider.cs:393 |
| 4 | Low | Validation retry injects error into prompt | OpenAiProvider.cs:288 |
| 5 | Low | Regex without timeout on LLM output | OpenAiProvider.cs:611-612 |

## Code example — Tool whitelist (the key security control)

```csharp
// Line 373: LLM returns a tool name, we look it up in the DECLARED list
var goalCall = action.Tools?.Find(t => t.Name == toolCall.Name);
if (goalCall == null)
{
    result = $"Error: unknown tool '{toolCall.Name}'";  // No execution!
}
```

This is correct — the LLM can only trigger goals the user explicitly declared as tools.

## Recommendation
PASS. Suggest running the **auditor** next.
