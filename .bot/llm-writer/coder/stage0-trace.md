# Stage 0 trace — where a value becomes LLM message text today

Coder: tester (plang-f2), llm-writer branch. Date: 2026-10-15.

## The door

A message's `Content` is a **`text.@this` template** (`app/type/item/text/this.cs`). `%var%`
inside it becomes text through that type's render, in two sub-doors:

1. **Full-match** `Content = "%orders%"` — `text.Value`/`text.Output` (this.cs:141-151, 242-246).
   The bound value answers through **its own door** (`resolved.Value()` / `resolved.Output(writer)`),
   so the whole content *becomes that value*, rendered by whatever writer it's handed.
2. **Partial** `Content = "rank these %orders%"` — `text.Rendered` (this.cs:156-196).
   One pass: each embedded `%var%` is written in place via `bound.Output(w, View.Out, context)`,
   where **`w` is a `text.Writer` constructed inside `Rendered` (this.cs:163) — hard-coded.**

## The writer

Today, always `app.type.item.text.Writer` (`app/type/item/text/writer.cs`). There is no llm writer.
- leaf (text/number/bool/datetime): **bare**, whole — no length limit.
- list / dict: the text writer delegates structural content to the **json writer** → full JSON,
  **all rows/keys, no cut, no column/type header**.
- binary/bytes: `[N bytes]` (writer.cs:76) — already not inlined as raw bytes.
- path (file/url/image): writes its **path string** (`path/this.cs:234` `Write → w.String(ToString())`),
  *not* `[image/png, 2.1 MB]`. Images attached via `message.Images` are handled separately in
  `OpenAi.ResolveImage` (base64 data-URI); that path is not the `%var%`-in-content path.

## The pinch (the finding that shapes stages 1–3)

`llm.query` / `OpenAi` only ever read the messages as **already-flattened strings**:
`(await action.Message.Value()).Clr<List<LlmMessage>>()` (OpenAi.cs:85). `Message.Value()`
resolves each content template *through the internal `text.Writer`* before the llm module sees
anything. So:

- **Full-match `%x%`** can already route through a chosen writer: `item.Output(writer, …)` is
  existing public surface, and `text.Output` forwards the passed writer to the bound value.
  An llm writer could cut a full-match list/dict/image **without touching core**.
- **Partial `"… %x% …"`** cannot: `text.Rendered` builds its own `text.Writer` internally and
  hands the llm boundary only the flat string. To limit a structural ref embedded in a partial
  template, `text.Rendered` would have to render through a **passed-in** writer — and `text` is a
  **locked core surface**. The plan's stage 2 (`rank these %orders%`) and stage 3 (`what is %photo%`)
  are partial templates, so they hit this.

## What's missing today (= the stages)

- a length limit for text (stage 1) — llm setting.
- list/dict column+type header, cut at 100 with `--- N more rows ---` (stage 2).
- image/file as `[mime, size]`, never inlined, in `%var%`-in-content (stage 3).
- `%x.trim%` giving the llm view for a list; text trim unchanged (stage 4).
