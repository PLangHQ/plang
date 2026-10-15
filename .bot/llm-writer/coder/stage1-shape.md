# Stage 1 shape — the llm.Writer, the length setting, the full-match cut

Coder: tester (plang-f2). Scope: no change to text or IWriter (Ingi gates the "text with
values in it" door). Full-match only; partial templates follow once the door exists.

## Components (all llm-module-owned, no core change)

1. **`llm.Writer`** — an `IWriter` the llm module owns (`app/module/llm/type/writer/this.cs`),
   beside json/text/formal. Renders like the text writer EXCEPT character content
   (`String`/`Content`/`Raw`) is capped at the length setting; when cut, it appends a marker
   `--- N more characters ---` (N = characters dropped). Structure (`BeginArray`/`BeginObject`)
   delegates like text→json **for now** — structural cuts (columns, row-cut) are stage 2.
   `Format => "llm"`, `EmitsSchema => false`. No value asks for it; the writer owns the cut.

2. **`llm.setting.length`** — `[Out, Store] number` on `app/module/llm/setting/this.cs`, the max
   for a text as **characters**. Default proposed: **20,000** (tunable; a dev raises it). Reached
   as `%!llm.setting.length%`, named by dot path.

3. **Wire-in at the llm boundary** — the llm module renders each message's **content value**
   through `llm.Writer` instead of grabbing the pre-flattened string
   (`Message.Value().Clr<List<LlmMessage>>()`, OpenAi.cs:85). A row's content is a `text.@this`
   template before conversion; for a full-match `%doc%`, `text.Output` forwards the handed writer
   to the bound value, so `llm.Writer` sees the text and caps it. Partial templates still render
   through text's internal text.Writer (no structural cut) until the door lands.

## Tests (red for their own reason) — test/plan/llm-writer/module/llm/

- **ShortTextGoesWhole** — content `%name%` (full-match), under max → content is the value whole,
  no marker. RED without the writer: a short value must NOT get a marker.
- **LongTextIsCutWithAMarker** — content `%doc%` (full-match), over max → exactly `length` chars
  then `--- N more characters ---` with N = dropped count. RED without the cut: over-max text
  rides whole today.
- **LengthSettingMovesTheCut** — set `%!llm.setting.length%` small, same `%doc%` → cut moves to
  the new max. RED without the setting wired: the cut must follow the setting, not a constant.

## OBP validation

| surface | check | result |
|---|---|---|
| llm.Writer | a writer the llm module owns; no value asks which writer; no `writer.Format` switch in a value | the cut lives in llm.Writer only |
| the length | one llm setting, named by dot path | `%!llm.setting.length%` |

## Checks tests can't show (I'll run + report)

- The builder's own prompts are NOT llm message values — llm.Writer governs `llm.query` message
  content only, never the builder's prompt files, so decider/builder prompt bytes don't move.
- llm tests' recorded answers don't move where their values are under the limit.
- gate by name; PLNG001/002 at 0; plang suite no worse than runtime2.

## Open sub-decisions for the architect

- a) length metric = **characters** of the rendered content (not tokens). OK?
- b) default **20,000 chars** — your/Ingi's to set.
- c) test scope **full-match only** for stage 1 (the plan's `summarize %doc%` is a partial
     template → its cut lands with the door). A partial template that renders to a single text
     string IS handed to `llm.Writer.Content` whole, so it could be capped as a whole now —
     include that, or keep stage 1 strictly full-match? I lean strictly full-match to avoid a
     test that half-depends on the pending door.
- d) `llm.Writer.Format` token = `"llm"`. OK?
