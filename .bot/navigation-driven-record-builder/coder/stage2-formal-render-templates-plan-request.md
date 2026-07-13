# Plan request — consolidate the formal-value render (4 copies) onto one owner; move presentation to plang templates

Branch: `navigation-driven-record-builder`. Coder, finishing the `[JsonConverter]` strip (`architect/stage2-json-serializer-fork-answer.md` + `stage2-perimeter-and-error-templates-answer.md`). **Ingi ruled the full-template direction** for the remaining perimeter sites (not the interim writer-render): *"never format text in C#; the object describes itself, the template loops it."* This is the same "presentation lives in plang templates" law as the error-templates piece (§4 of the perimeter answer). Requesting a design ruling + confirming a `plan.md` before I implement.

## State (committed, green, paused)
- Door + `TryConvert` deleted; tests re-homed (A+B).
- Finish-C step 1 — dead STJ surface removed (`dict.Json` registration, `RawOptions`, `_snapshotClone`).
- Finish-C step 2 — `goal/Methods` builder LLM preview drives `json.Writer` (interim; golden-pinned clean).
- **Paused before**: remaining perimeter sites + the 13-converter strip.

## The finding that reshapes it: the formal-value render is duplicated 4× across 3 build/catalog contexts

The formal syntax is `module.action Name([type] value), Name2(...)`. The **value** rendering (`string→bare/quoted, %var%→bare, bool→true/false, number→invariant literal, dict/list→JSON`) is copied into:

| # | site | context | dict/list branch |
|---|---|---|---|
| 1 | `ui/code/Fluid.FormatFormalValue` (backs the **`formal` Liquid filter**, `\| formal`) | ui viewer / template render | `JsonSerializer.Serialize(v)` |
| 2 | `build/code/Default.FormatValue` (+ `RenderActionFormal`) | build **trace-backfill** (rebuild prompt) | `JsonSerializer.Serialize(v)` |
| 3 | `type/spec/render.RenderValueFormal` | **catalog example** render (Describe) | `JsonSerializer.Serialize(...)` (`:90,101,111`) |
| 4 | `goal/Methods` — `FormatForLlmFallback` C# + my interim `FormatActionsJson` | goal LLM format (no-template fallback) | (emits **JSON**, not formal — inconsistent with #1's template path) |

Every dict/list branch is exactly the converter-firing site. #2's own comment admits the hand-syncing: *"Mirrors the rules used by Default.FormatValue … so they stay consistent."*

The goal LLM format is **already** a Liquid template (`goalFormatForLlm.template`) looping `step.Actions` with `{{ p.Value | formal }}` — so the target shape half-exists; the value render just still type-switches + `Serialize` in C#.

## Existing engine inconsistency (surfaced, needs a call)
`goal/Methods.cs` renders the Liquid-syntax `goalFormatForLlm.template` via **`Scriban.Template.Parse` + RenderAsync** — but `| formal` is a **Fluid** filter (`Fluid.cs:105`), registered only on the Fluid path. So either Scriban silently drops `| formal` (previews degrade) or the template errors → `FormatForLlmFallback`. The goal format and the ui `formal` filter are on **different engines**. Unifying on `ui.render`/Fluid (and deleting the Scriban call) looks in-scope for this piece.

## Plan sketch (the code shape — for your refinement)

**A. `formal` becomes value-owned — the value describes itself.** The `formal` filter stops type-switching in C# and asks the value for its formal form (scalar→literal, container→its own `json` Output, clean + `[Sensitive]`-masked):
```csharp
options.Filters.AddFilter("formal", (input,_,__) =>
    new StringValue(AsItem(input).FormalString()));   // item owns Formal; no Serialize, no converter
```

**B. The formal *syntax* lives in ONE template**, looping self-describing objects (goal template already does this; Default/ui/spec stop hand-rolling):
```liquid
{% for a in actions %}{{ a.module }}.{{ a.action }}{% for p in a.parameters %} {{ p.name }}({% if p.type %}[{{ p.type }}] {% endif %}{{ p.value | formal }}){% unless forloop.last %}, {% endunless %}{% endfor %}{% endfor %}
```

**C. Demolition:** `FormatActionsJson`, `Default.FormatValue`+`RenderActionFormal`, `Fluid.FormatFormalValue`'s type-switch, `spec/render`'s value branch, `FormatForLlmFallback`'s C# → the value-owned `formal` + templates. **Then strip the 13 converters** (nothing in C# serializes items).

## Design questions for you

1. **Where does the ONE formal renderer live?** A value-owned format/`IOutput` per item (`formal`, sibling of `text`/`json`), or a filter that just delegates to the value's existing `json`/`text` Output (the formal-vs-json delta is only scalar quoting: `foo` vs `"foo"`, `%var%` bare)? If value-owned, does `formal` warrant its own `IOutput`, or is it "json for containers, literal for scalars" computed in one place?
2. **Do build / ui / spec-catalog truly consolidate to one template + one filter**, across their three hosts (build trace-backfill, ui viewer, catalog Describe)? Or one shared *filter/value-format* but separate templates per host?
3. **Engine**: unify all four on `ui.render`/Fluid and delete `Methods.cs`'s Scriban path? Or keep Scriban for the goal format and register a Scriban `formal` too?
4. **Fallback policy**: throw-on-missing-template (matching the error-templates ruling) or keep a C# fallback? (`FormatForLlmFallback` exists precisely because the current template can fail.)
5. **Sequencing vs the error-templates piece** (§4): same law, overlapping `ui.render` wiring — one combined "presentation → templates" piece, or two? Does the converter strip block on this, or can it land first with the perimeter previews reflecting until templates arrive?

## Recommendation
`plan.md`-worthy (template files + locations, `ui.render` wiring, the value-owned `formal`, the 4-site C# demolition, then the strip). I'll write it once Q1–Q5 are ruled. If you'd rather the strip land first (previews reflecting in the interim) and the template consolidation follow as its own piece, say so — that unblocks the converter removal now.
