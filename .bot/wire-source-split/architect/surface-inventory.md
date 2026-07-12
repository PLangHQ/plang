# Surface inventory — created / deleted / modified

Companion to [`plan.md`](plan.md) — the full design, code bodies, rulings, and demolition phasing live there; this is the flat member-level inventory for quick reference during implementation and final cleanup.

## New

| What | Where | Purpose |
|---|---|---|
| class `wire.@this : source` | `type/item/wire/this.cs` (new file) | the second source kind — still-encoded slice + capturing serializer; overrides `Read` (via `_reader.Read`), `Write` (verbatim `w.Raw`), `Declared` |
| method `source.Declared(type)` | `source.cs` | internal virtual re-birth under a new declaration (replaces the type reaching into `src.Raw`/`src.Format`) |
| overload `type.@this.Create(slice, ctx, ISerializer reader)` | `type/this.cs` | the capture door — mints a lazy `wire` |
| method `json.Reader.Slice()` | `channel/serializer/json/reader.cs` | verbatim token capture, quotes/escapes included (strictness ruling B; `RawValue` decodes strings so it cannot serve) |
| property `Serializers.Transport` | `channel/serializer/list/this.cs` | named door for the transport serializer — kills the `is plang.@this` type-check and feeds the wire mint site |
| method `channel.Read(byte[], ct)` *(name coder's)* | `channel/this.cs` | the receive door replacing `StampReadAsync` |
| interface `ITransport : ISerializer` (one member: the slice-decode `Read`) | `channel/serializer/` | plan §11 — plang-only door; wire's field + registry `Transport` typed as it |

Explicitly **not** created (died in earlier design rounds — do not resurrect): `ICreate.Format`/`Encoding`, any `IsJson`/`Structured` flag, dict/list literal arms, an octet-stream registration, the `value/this.cs` serializer rename.

## Deleted

Everything below gets `[System.Obsolete]` at step 0 (plan) and is deleted mechanically at the end.

| What | Where |
|---|---|
| `type.@this.RawFormat` | `type/this.cs:185-197` |
| `format` param on `type.Create` | `type/this.cs:261, :329` |
| `source._format` field, `Format` property, format ctor param | `source.cs:26/:46/:51/:68` |
| serializer-registry lookup + "channel not wired" throw in `source.Read` | `source.cs:179-188` |
| `Text.Mime` compare in `source.Write` | `source.cs:227` |
| `channel.StampReadAsync` / `StampValue` / `StampType` | `channel/this.cs:272-316` |
| `SerializeAsync(SerializeOptions)` + `ResolveForWrite` + the `SerializeOptions` carrier | `channel/serializer/list/this.cs:146-168, 195-202` |
| the wire reader's `deferredRaw`/`deferredFormat`/`born` locals + twin tail arms | `data/reader/this.cs` |
| file-save's three format lines (`?? Text`, `?? "application/plang"`) | `path/file/this.Operations.cs:73-75` |
| `ISerializer.Read` member + `Json.Read` + `Text.Read` (plan §11 — no orphan check needed) | `serializer/this.cs:45-53`, `Json.cs:142-151`, `Text.cs:79-90` |
| read twins: `ResolveSerializer` + `DeserializeAsync<T>(DeserializeOptions)` + `DeserializeOptions`/`ResolveOptions` (plan §10) | `channel/serializer/list/this.cs:173-189, 204-224` |
| `type.@this.Convert(string)`'s json arm (obp-findings §1) | `type/this.cs:462-472` |
| `Text._jsonFallback` field + ctor param + stale class doc (obp-findings §3) | `Text.cs:21, 27-31, 5-9` |

Reprieved: `Serializers.Text` property (`channel/serializer/list/this.cs:130`) — becomes file-save's content fallback.

## Modified

| What | Change |
|---|---|
| `source` class | unseals; `Read()` becomes `private protected virtual` (the value-dispatch body); `Write` becomes template-guard + materialize-and-delegate; `Value()`'s catch gains `NotSupportedException` |
| `plang.SerializeAsync` catch | gains `FormatException` (write-side failure story) |
| `data/reader` value arm | template gate + wire mint; tail arms merge to one `Data(name, value)` |
| `object/serializer/json.cs` | static Of-mode → `ITypeReader` with `Kind => "json"` |
| `Reader.TypeOf` | scans the typed tables too (`{binary, json}` narrowing survives the conversion) |
| `channel/type/{http,stream,file}` | re-point `StampReadAsync` calls to the receive door |
| file-save (`path/file/this.Operations.cs:225`) | picks its serializer by extension, `Serializers.Text` fallback (content, not envelope — flagged behavior change) |
| `channel/list/this.cs` `ReadChannelAsync<T>` | unifies through the receive door + `As<T>` (§10 addendum) — its `channel is stream.@this` fork dies |
| dict/list readers | **untouched** (the literal arm was cut by the strictness rulings) |

**Addenda after coder start (also marked ADDENDUM in plan):** `ReadChannelAsync<T>` unification; `type.Convert(string)` likely deletes WHOLE (caller-less — verify) incl. `FromWire`/`_wireReaders`; the two stamp-sites' unknown-mime fallback unifies (`?? binary` rule stated once).
