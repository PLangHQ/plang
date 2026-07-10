# Decision — the json channel drives the writer directly (B)

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-json-channel-write-path-gap.md`. The gap was real — the ruling never specced the channel's bare-value write. Your lean (B) is confirmed.

## The ruling

**`channel/serializer/Json.cs::SerializeAsync` stops handing the value to STJ.** The channel is the I/O layer — it picks the serializer and the value writes itself (Ingi's one-serialization-world ruling, operationalized):

```csharp
// after — no STJ in the value path, no converters, no new types:
var writer = new json.Writer(stream /*, bare: no {name,type,…} envelope */);
await data.Write(writer, View.Out, ctx);    // the existing door (data/this.Output.cs)
```

- A **plang value** → its own `Write(IWriter)` (the complete path your inventory confirmed).
- A **host / foreign object** (including plang values nested mid-graph inside a CLR object) → the clr carrier → the `*`-kind `Output` — the **declared-face rule**: contract for plang types, transparent dump for foreign, `[Sensitive]` masked, nested values writing themselves via `WriteReflected`. This is (A)'s mid-graph story, already owned by machinery that exists — STJ's converter dispatch was never needed for it.

## Why not (A)

A bare-value `JsonConverter` driving `value.Write` keeps **STJ as the driver** with our writer re-entered through a shim — the second world alive as a bridge, plus a converter type whose only job is re-entry (a middleman by construction). (C) was never available: the Stage-5 sweep deletes the 16 files; this reroute must land at or before deletion.

## Sequencing note

Land this rewrite **before** stripping the 13 `[JsonConverter]` attribute lines (your corrected wiring map: the attributes are what fire today — fully-qualified, so grep for `System.Text.Json.Serialization.JsonConverter` when sweeping). `dict.Json`'s explicit registration at `Json.cs:52` dies in the same edit.

## The asymmetry, logged not acted on

After (B), the json channel is writer-driven while the **plang channel still drives through STJ + the `Wire` converter** — the same converter-driven pattern being removed everywhere else. That is its own future cleanup: **one todo, no scope creep in this stage.** (Added to `Documentation/v0.2/todos.md`.)

## Acceptance

- `application/json` channel output byte-identical for the representative set (scalar, dict, list, nested) before/after the rewrite — pin with a golden test before stripping the attributes.
- A foreign-POCO-with-nested-plang-value writes through the `*`-kind path (the mid-graph pin).
- After the attribute strip: grep-zero on `System.Text.Json.Serialization.JsonConverter(typeof(Json))` under `type/`.
