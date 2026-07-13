# Plan v2 — formal render, reconciled after the dead-code findings

Supersedes the stale premise in `formal-render-plan.md`. The original plan assumed the goal LLM template drove the formal consolidation. Tracing it out (with Ingi, 2026-07-13) inverted that:

## What the trace found (and what's already done)

- **`goal/Methods.cs` `FormatForLlm` was DEAD** — zero production callers. The builder formats the goal for the LLM *in plang*: `Plan.goal` → `render template ".../goalFormat.template"` (via `ui.render`). The live `goalFormat.template` is simple (`goal.Name` + `step.Text` + errors/warnings — **no actions, no formal**).
- So `goalFormatForLlm.template` (the one with `| formal`) was used **only** by dead `FormatForLlm`, and the `| formal` filter had that dead template as its **only** consumer.
- **DONE (committed, pushed, green):** deleted `Methods.cs` (`FormatForLlm`/`BuildFormatData`/`FormatActionsJson`/`FormatForLlmFallback`), `goalFormatForLlm.template`, the `| formal` filter + its step-2 supports (async `Value` accessor, views' `Native`), and all their tests. **`text.Writer`'s leaf-item arm stays** (an independent latent-bug fix, own test — `TextWriterItemArmTests`).

## The real remaining converter-firing sites (the only reason for this piece)

Two C# renderers reconstruct a formal STRING and `JsonSerializer.Serialize(v)` a structured param value (the converter-firing line):

1. **`build/code/Default.RenderFormal`** (`+RenderActionFormal`/`FormatValue`) — sets `step.Formal` as a **backfill** only: `if (string.IsNullOrEmpty(step.Formal)) step.Formal = RenderFormal(prior.Actions)` when reusing prior actions. (Normally `step.Formal` is the LLM's own `formal` line from `Compile.llm`, stored by `Start.goal`.)
2. **`type/spec/render`** — reconstructs formal strings for catalog Describe examples (`module.cs:383-385`).

## Decisions (Ingi)

- **No `| formal` filter.** A niche filter in Fluid pollutes the global filter set for every plang user; `formal` is too specific. No "special rendering" hooks in `ui.render` either.
- **The template builds the formal output by navigating the dict/list** — the actions/params are bound as **self-describing plang values** (dict/list), and the `actionFormal` template loops them to emit `module.action Name([type] value)`.
- **Structured param values (list-of-dicts → JSON) are rendered by the template looping the dict/list** (validated feasible — see below). Deep nesting (beyond ~one level) is an **accepted edge** (Liquid has no recursion), same spirit as the accepted `)` -in-a-value edge.
- **Quote rule dies** (space/comma quoting) — goldens pin the new clean output; no backward-compat.
- Throw on template failure; no C# fallback.

## Feasibility validated
A pure-Liquid template CAN emit a flat list-of-dicts as JSON — with literal-brace escaping (`{{ '{' }}`), manual comma control (`forloop.last` / a `first` flag), and `kv[0]`/`kv[1]` for dict entries. It's verbose but works for the realistic formal shapes (scalars + flat list-of-dicts like `Messages`). Discriminating scalar-vs-dict-vs-list per value in Liquid is the fiddly part the `actionFormal` template must carry.

## Remaining steps
1. **`actionFormal` template** (`os/system/...`): loops `actions` → `module.action` + params `Name([type] value)` + modifiers (`| mod...`). A `value` sub-render: scalar bare, list `[...]`, dict `{...}` (one level; brace-escaped, manual commas).
2. **Migrate `Default.RenderFormal`** → build `prior.Actions` as a plang list-of-dicts (self-describing: `{Module, ActionName, Parameters:[{Name,Type,Value}], Modifiers:[...]}`), `await app.Run(ui.render, actionFormal, goal=that)`, assign `step.Formal`. Delete `RenderFormal`/`RenderActionFormal`/`FormatValue`. (sync→async at the call site — its method is already async.)
3. **Migrate `spec/render`** the same way (catalog examples). Collapse the class.
4. **Strip the 13 converters** + nativize `json/writer.cs:88` (type entity `{name,kind?,strict?}` via primitives) + the 14th, per its `Wire`-dependency check. `dict/Json.cs` stays only if its live `channel.serializer.Json` consumer still needs it — re-verify. `item/serializer/json.cs` stays (ruling 8).

## Open risk
The `actionFormal` template's per-value scalar/dict/list discrimination in Liquid is fragile; if it gets unmaintainable, revisit the general-`json`-filter option (a broadly-useful filter, unlike niche `formal`) or value-owns-its-formal-string (C# `text.Writer`, the value's own render) — both were considered and set aside for "template loops," but the template's real complexity is the deciding evidence.
