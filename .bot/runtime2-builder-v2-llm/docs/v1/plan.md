# Docs v1 Plan — LLM Module

## Gaps Identified

1. **User-facing module docs** — MISSING: no `docs/modules/llm.md`
2. **Module index entry** — MISSING: LLM not listed in `docs/modules/index.md`
3. **Architecture docs — modules.md** — UPDATE: no llm row in Built-in Action Handlers table, no Details section
4. **Architecture docs — good_to_know.md** — UPDATE: no ILlmProvider entry, no provider type mapping
5. **XML docs** — REVIEW: source files already have XML docs (query.cs, LlmMessage.cs, ToolCall.cs, ILlmProvider.cs)

## Plan

1. Create `docs/modules/llm.md` with: actions, parameters, schema/format, tools, validation, streaming, conversation continuity, images, caching, response properties, provider config, examples
2. Update `docs/modules/index.md` — add LLM to I/O section
3. Update `Documentation/App/modules.md` — add llm row + Details section
4. Update `Documentation/App/good_to_know.md` — add ILlmProvider section + update provider interfaces list + update type name mapping
5. Verify XML docs on public types (already present)
6. Write verdict.json
