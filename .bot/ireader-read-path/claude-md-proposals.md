## coder — v1 — 2026-06-19
**Target:** /CLAUDE.md
**Why:** This branch built the `IReader` read path and, in doing so, established (with Ingi) that deserialization has **two distinct read modes** that must not be conflated. A future bot will otherwise try to "unify" them and reintroduce the ceremony this branch deliberately rejected. The distinction is canonical and load-bearing.
**Proposed change:**

Add under Runtime2 Conventions:

- **Two read modes — token-stream vs whole-payload.** Deserialization splits by *how the bytes arrive*, and the two are both correct, not a migration in progress:
  - **Token-stream pull** — the `.pr`/wire structural read. `Wire.ReadBody` drives a `json.Reader` (`ref struct Reader : app.channel.serializer.IReader`, holds `Utf8JsonReader` by value, threaded by ref) and the declared type pulls its value token-by-token via `app.type.reader.ITypeReader.Read<TReader>(ref TReader, kind, ctx) where TReader : IReader, allows ref struct` — no `JsonElement` DOM. Registered at `App.Type.Readers.Typed(type, kind)`; per-type `serializer/Reader.cs` classes (bool/guid/duration/number/text/list/dict shipped). Containers stream their own slots (`item.serializer.json.ReadSlot`); the element walk lives on the container, not Wire.
  - **Whole-payload content decode** — `file.read`'s path. The channel stamps `{type,kind}` from mime → `item.source` (raw `string`/`byte[]`) → `source.Value` → the type's `Read(object raw, kind, ctx)` (`serializer/Default.cs`/`csv.cs`, registered at `Readers.Of`). The raw is already materialised; the type decodes it whole (csv→table, json-string→dict, bytes→image). A token reader buys nothing here — do **not** wrap the raw in a degenerate `IReader` to "unify" the registries; that indirection just returns its own input. `Readers.Of` is the right content-decode contract, not transitional debt.
  - `IReader` is for self-describing token streams (json now; protobuf/cbor later as siblings). Whole-payload formats (csv grid, raw image bytes) stay on `Read(object raw)`.
