# Security Analysis v1 — LLM Module

## Scope
New files on `runtime2-builder-v2-llm`:
- `PLang/App/modules/llm/query.cs` — action record
- `PLang/App/modules/llm/LlmMessage.cs` — message type
- `PLang/App/modules/llm/ToolCall.cs` — tool call carrier
- `PLang/App/modules/llm/providers/ILlmProvider.cs` — provider interface
- `PLang/App/modules/llm/providers/OpenAiProvider.cs` — OpenAI provider (~846 lines)
- `PLang/App/Engine/Goals/Goal/GoalCall.cs` — GoalCall additions (Description, Parallel)
- `PLang/App/Engine/Providers/this.cs` — ILlmProvider registration

## Approach
1. **Blue Team**: Map attack surface — external data boundaries, tool execution, image handling, regex, caching, conversation state
2. **Red Team**: Exploit sketches for each vector — prompt injection → tool execution, image path traversal, regex DoS, cache poisoning, conversation injection
3. **Verdict**: Rate findings against PLang threat model (user-sovereign, .pr files trusted)

## Key Question
The critical vector is the **tool call loop** — the LLM decides which goals to execute. But tools come from .pr files (trusted), and the LLM can only pick from the declared tool list. The question is whether the LLM can name a tool NOT in the list and get it executed.
