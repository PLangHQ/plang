# Plan — consolidate the formal render onto `text.Writer` + templates; then strip the 13 `[JsonConverter]`s

Branch: `navigation-driven-record-builder`. Implements `architect/stage2-formal-render-answer.md` + `…-addendum.md` (Q1–Q5 ruled, four trace findings folded in). This is the last piece before the converter strip; the strip lands at the END of this piece.

## Why

The catalog's **formal value render** (`string→literal, dict/list→JSON`) is hand-copied into **four** C# sites, each hitting the `[JsonConverter]` via `JsonSerializer.Serialize(v)` — the last thing keeping those converters alive. The copies are kept in sync by hand (`Default.FormatValue`'s comment: *"Mirrors … so they stay consistent"*) and disagree in practice (the goal template emits formal syntax; its C# fallback emits JSON). Ingi's law: **presentation lives in plang templates, never C# string-formatting; the value describes itself, the template loops it.**

The value already owns its one render door — `Write(IWriter)` — and the **text channel's writer IS the formal renderer**: a top-level scalar renders bare (`hello`, `42`, `true`), structural content renders as JSON, both through the one writer (`channel/serializer/text/writer.cs`). So `| formal` collapses to `buffer → text.Writer → w.Value(item) → string`. No `.FormalString()` (vetoed — a second door on the value), no new writer, no per-type formal anything.

## The design

1. **`| formal` = `text.Writer`.** The Fluid `formal` filter (`Fluid.cs:105`) stops type-switching and drives `text.Writer` over a buffer with the value. The incumbent space/comma **quote rule dies UNREPLICATED** (addendum §1) — the grammar's parens delimit (`name(value), name2(...)`); a `)` inside a value is an accepted known edge (LLM-facing previews, nothing parses them back). Goldens pin the NEW clean, quote-free output (no-backward-compat — the builder prompt change is accepted).

2. **Async `Value` accessor on `data.@this`** (addendum §2 — the #1 addition). `{{ p.Value | formal }}` binds **nil** today: nothing opens a Data reached by reflection mid-render (it only ever worked via Scriban→C# fallback). Register a `ValueTask` member accessor on `data.@this` so `{{ p.Value }}` calls `await d.Value()` lazily (Fluid supports async accessors). This makes the goal template's existing grammar actually run. *Follow-up (plan may defer):* with the accessor live, the eager bind loop (`Fluid.cs:122`) can thin to binding the Data itself.

3. **Views expose their backing native** (addendum §3). Fluid dismantles values at binding into `NativeDictView`/`NativeListView` (`:203,:246`), so `| formal` receives Fluid wrappers/views/raw scalars — never an item (that's why `UnwrapFluid` exists). The views already hold their backing (`NativeDictView(dict.@this d)`); expose it (internal) so the filter reaches the value and drives `text.Writer`; a raw scalar goes straight to `w.Value(raw)` (bare at depth 0). **`UnwrapFluid` and `FormatFormalValue` die.**

4. **Fix `text.Writer.Value`'s missing item arm** (addendum §4 — a latent bug, own small test). `json.Writer.Value` dispatches an item to its own door (`case item.@this v: v.Write(this)`, `json/writer.cs:184`); `text.Writer.Value` has no item case, so a top-level item falls to `default → Structural() → json-quoted` while `item.Write(textWriter)` renders bare — the two entrances disagree. Add `case item.@this v: v.Write(this);` before the default in `text/writer.cs`.

5. **One `actionFormal` partial + per-host wrappers** (Q2). The formal LINE grammar — `{{ a.module }}.{{ a.action }}{% for p in a.parameters %} {{ p.name }}([{{ p.type }}] {{ p.value | formal }}){% endfor %}` (modifiers via `|`) — lives once. Three thin host wrappers `{% include %}` it. Location resolves relative to the calling goal's dir via `PlangFileProvider` (`Fluid.cs:319-329`) — pin the partial under `os/system/builder/templates/` and include with the right relative path.

6. **Fluid only; Scriban removed** (Q3). `goal/Methods.cs:2,22` is the only production Scriban site — delete the parse, render `goalFormatForLlm.template` through the Fluid provider, drop the Scriban package ref if nothing else holds it.

7. **Throw, no fallback** (Q4). A missing/failing template throws to the top of the app (the `Console.WriteLine` process-boundary last resort). `FormatForLlmFallback` dies.

## The wiring gap to resolve first (build + Describe are C#, sync)

The goal format already has a template path (needs only rewiring to Fluid). But two hosts render formal **from C#, synchronously**, with no template today:

- **build trace-backfill** — `Default.cs:692` `step.Formal = RenderFormal(prior.Actions)` (sync, inside `ApplyBuiltStep`).
- **catalog Describe** — `module.cs:383-385` `renderer.Render(s)` (sync, inside a `.Select` building example Data).

Moving these to Fluid means invoking `ITemplate.Render` (async) from C#. **Decision the plan commits to:** these hosts construct a programmatic template render through the App's registered `ITemplate` provider (the same `Fluid` instance `code/this.cs:262` registers), passing the actions/example as the data and the host wrapper as the template — NOT by hand-hopping through a `ui.render` action record. This introduces a **sync→async ripple** at both call sites (`RenderFormal`'s caller path in `ApplyBuiltStep`; the `.Select` in `Describe`). Both callers are already in async methods — thread the `await`. *(If the programmatic entry doesn't cleanly exist on `ITemplate`, adding a minimal `Render(templateName, data, context)` overload is in scope — flag if it grows.)*

## Demolition worklist (by when each dies)

### Dies with the filter rewire (step: `| formal` → `text.Writer`)
- `ui/code/Fluid.cs` — `FormatFormalValue` (`:154`, incl. the `:158-162` quote rule), `UnwrapFluid` (`:278`). The `formal` filter (`:105`) STAYS, rewired.
- `NativeDictView`/`NativeListView` (`:203,:246`) — GROW an internal backing accessor; the classes stay.

### Dies with the template consolidation (step: partial + wrappers, Fluid unification)
- `build/code/Default.cs` — `RenderFormal` (`:712`), `RenderActionFormal` (`:724`), `FormatValue` (`:744`). Replaced by the trace-backfill wrapper including `actionFormal`; `step.Formal` set from the render.
- `type/spec/render/@this` — `Render(ExampleSpec)` (`:30`), `RenderActionFormal` (`:41`), `RenderValueFormal` (`:74`), `BuildActionRecord`, `ConvertValueForJson` (`:153`). The class collapses to invoking the Describe wrapper (or dies whole if the wrapper subsumes it).
- `goal/Methods.cs` — `FormatForLlmFallback`, `FormatActionsJson` (mine, interim), the Scriban parse (`:2,22`). `BuildFormatData` thins (the async `Value` accessor removes the `p.Peek()` pre-bake). `FormatForLlm` rewired to Fluid.

### Dies with the strip (step: last, after zero item-through-STJ remains)
- The 13 `type/item/<name>/Json.cs` + their `[JsonConverter]` lines; `type/this.json.cs` + the type-entity converter (per its `Wire`-dependency check — nativize `writer.cs:88` `BeginRecord` to emit `{name,kind?,strict?}` via primitives first). `dict/Json.cs` **stays** (live: `channel/serializer/Json` uses it) — re-verify after the perimeter migration whether its remaining consumers still need it; if the STJ `application/json` serializer's own item paths are gone, revisit. `item/serializer/json.cs` STAYS (ruling 8).

### Stays (explicit)
- `channel/serializer/text/writer.cs` (grows the item arm) — the whole formal render.
- `channel/serializer/json/writer.cs`, the wire path.
- The `formal` Fluid filter (rewired), `goalFormatForLlm.template` (rewired), `Fluid.Render` + views (grow native accessor), `data.@this` (grows async `Value` accessor).

## Incumbent leaf-traces (call sites + disposition)

| call site | today | after |
|---|---|---|
| `Fluid.cs:106` `formal` filter | `FormatFormalValue(input.ToObjectValue())` | `buffer→text.Writer→Value(view.Native ?? raw)→string` |
| `Default.cs:692` `step.Formal = RenderFormal(...)` | sync C# formal string | `await`-render the trace-backfill wrapper → string; `RenderFormal`/`RenderActionFormal`/`FormatValue` deleted |
| `module.cs:385` `renderer.Render(s)` | sync `spec/render` | `await`-render the Describe wrapper per example; `spec/render` collapses |
| `Methods.cs:26` `Scriban…RenderAsync(data)` | Scriban (no `formal` filter → fallback path) | Fluid render of `goalFormatForLlm.template` (now includes `actionFormal`, `formal` real via §2 accessor) |
| `Methods.cs` `FormatForLlmFallback` | C# string-build, JSON values | deleted (throw-no-fallback) |
| `text/writer.cs:71-95` `Value` | no item arm → item json-quoted | `case item.@this v: v.Write(this)` → bare/own-form; own test |
| `json/writer.cs:88` `BeginRecord` type entity | `JsonSerializer.Serialize(record.Type, _options)` | writer primitives `{name,kind?,strict?}`; the 14th converter dies |

## Sequencing within the piece
1. `text.Writer.Value` item arm + its test (contained bug fix, lands first).
2. Async `Value` accessor on `data.@this` + views expose native; rewire the `formal` filter to `text.Writer`; delete `FormatFormalValue`/`UnwrapFluid`. Golden: the `formal` filter emits clean quote-free output.
3. `actionFormal` partial + rewire `goalFormatForLlm.template` (Fluid); delete Scriban from `Methods.cs`, `FormatForLlmFallback`, `FormatActionsJson`. Golden: goal LLM format.
4. Trace-backfill + Describe wrappers; delete `Default.RenderFormal`/`FormatValue`, collapse `spec/render`. (sync→async ripple.) Goldens: `step.Formal`, catalog examples.
5. Strip the 13 converters + nativize `writer.cs:88` + the 14th. Baseline suites vs recorded reds; zero `Json.cs` under `type/item/` except `kind/json/` (and `dict/Json.cs` per its live-consumer re-check).

Error-templates piece (`perimeter answer §4`) after this, back-to-back or interleaved — coder's judgment (shared `ui.render` experience argues back-to-back).

## Open risks to watch
- **The sync→async ripple** at build trace-backfill + Describe — how far it propagates (both callers are already async; expected shallow, verify).
- **Partial resolution relative to the calling goal's dir** — the three hosts run from different goal dirs; the include path must resolve for all (may need an absolute-from-os-root convention, not a goal-relative one). Pin during step 3.
- **`dict/Json.cs`** — the one converter with a live consumer (the STJ `application/json` serializer). Re-verify at step 5 whether the perimeter migration removed its last item-serialize path; if not, it stays and the "zero Json.cs" goal is "zero except kind/json AND dict".

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `\| formal` = `text.Writer` + `Value(item)` | value has ONE door; the writer is the syntax variant; no new member/class, no `.FormalString()` | ✓ |
| quote rule deleted unreplicated | a rule nobody set deliberately dies with its copies; `)` edge accepted, not pre-engineered | ✓ |
| async `Value` accessor | the template reaches the value through the value's own door, lazily — no eager Peek-copy pipeline | ✓ |
| views expose backing native | the wrapper hands over the whole box; `UnwrapFluid` (raw rebuild) dies | ✓ |
| one `actionFormal` partial | the grammar exists once; hosts own only their wrapping (their real difference) | ✓ |
| Scriban removed | one template engine; a second is a fork | ✓ |
| throw-to-top | one failure policy across all template rendering | ✓ |
| strip at the end | converters die only when the last item-through-STJ site is gone; no garbage interim | ✓ |
