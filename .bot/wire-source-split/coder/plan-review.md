# Coder review of `plan.md` — for architect

Branch: `wire-source-split`. Reviewed HEAD (`18c0e40d9`) against source. The design
(source vs `wire : source`, formats leave the type layer, declaration is the whole
selector) is sound and the OBP shape holds. All quoted line refs verified against HEAD.
Two things must change before this is implementable; two minor notes.

---

## 🔴 Blocker — string slots routed to `wire` break both read and write

§4's value case sends "string tokens included" to the `else`/wire arm:

```csharp
else if (typeRef.Template != null && reader.Peek() == String)   // template gate → content source
    value = typeRef.Create(reader.String(), ctx.Context);
else                                                            // → wire
    value = typeRef.Create(UTF8.GetString(reader.RawValue()), ctx.Context, _owner);
```

`json.Reader.RawValue()` **decodes** string tokens — `reader.cs:124-125` is
`UTF8.GetBytes(_r.GetString())`, which strips the quotes and unescapes. So a string
slot does not ride "verbatim"; it arrives already decoded. Trace a plain `{text}`
`"readme.md"` (`template=None`, real slot from
`Tests/TypeKindStrict/.build/setastextuppercase.test.pr`):

```
Peek==String, Template==None → else arm → wire holding  readme.md  (decoded, no quotes)
wire.Write → w.Raw("readme.md")   → "value": readme.md   ← invalid JSON, corrupts the .pr
wire.Read  → plang.Read → json.Reader over `readme.md` → utf8.Read() on bare text → JsonException
```

Both directions broken. And non-template string literals are the most common slot type
— verified real ones in fresh `.pr`: `text` / `readme.md` / `int` / `number` under
`{text}`, `text` under `{object}`. Every one falls to `else` because `template=None`.
This regresses today's working behavior (string → Text serializer content-read →
quoted write).

**Root cause:** the plan splits on *template presence*; the real axis is *token kind*.
`value.Reader` is scalar-only (throws on structural, `reader.cs:77-78`), `json.Reader`
handles structure — so the split must be:

- **string token → content source** (`value.Reader`, decoded). The source ctor's
  `IsVariable`/`%ref%` birth gate (`source.cs:57`) already covers templates, so no
  separate template arm is needed — **drop the `Template != null` condition**.
- **non-string token (number/bool/object/array) → wire.** `RawValue()` yields valid
  JSON here, `json.Reader` reads it, verbatim write is valid JSON. This is where the
  signature-fidelity win actually lives (structured relay slots).

That is exactly today's `Peek()==String ? Text.Mime : plang` split (data reader
`:90-91`) — keep the split, re-express it as source-vs-wire.

**Casualty:** the stated goal "string slots now write back byte-identical (they ride as
wires)" (plan line 274) is **not achievable with `RawValue()`** — it decodes.
Byte-identical string relay would need a *new* raw-quoted-slice capture on the reader
(token span incl. quotes/escapes), a separate change the plan does not spec.
Recommendation: drop that goal for this branch — structured slots already get
byte-identity, which is the relay-signature case — or spec the raw-slice reader
capture explicitly.

## 🟠 `_owner` contradicts the data reader's stated stateless invariant

§4 passes `_owner` (the capturing serializer) into the wire, but `data/reader/this.cs:13`
declares the reader **stateless** and it has no serializer field. Threading `_owner` as a
field breaks that invariant; threading via `ReadContext` pollutes every type reader with a
serializer nobody else uses.

The `@schema:data` reader is only ever driven by the transport (plang) serializer, so the
capturer is knowable at the wire site without threading — grab it from the registry via
the `Transport` door step 9 already adds
(`ctx.Context.Actor?.Channel.Serializers.Transport`). Keeps the reader stateless. (Cost:
it names "the capturer is transport" rather than the capturer literally passing itself —
but the data reader is json-bound today regardless.)

## 🟡 Minor

- **§1 Write guard is redundant.** `if (_value is string s && (IsVariable || _type.Template != null))`
  — `IsVariable` is only ever set when `Template != null` (`source.cs:57`), so
  `IsVariable ||` is dead. Just `_type.Template != null`.
- **§1 materialize-on-write has no failure story.** `Read().Write(w)` can throw
  `FormatException` (bad literal) mid-stream, and `plang.SerializeAsync`'s catch
  (`serializer/plang/this.cs:180`) filters only
  `JsonException/NotSupportedException/IOException` — a `FormatException` escapes to the
  courier as a raw throw, bypassing the `MaterializeFailed` story that lives in
  `source.Value`, not `Write`. The strictness ruling says the build never emits mismatched
  tokens, so it shouldn't fire from `.pr`; but a runtime-produced content source could.
  Either give `Write` try/catch parity with `Value()`, or note explicitly that write-time
  materialization trusts prior validation.

## Verified clean

Line refs: `source._format` 26/51, `Format` 68, `Read` 179-188, `Text.Mime` write 227,
catch 157; `type.RawFormat` 191-197, `Create(…, format=null)` 261, arms 279/289/329; data
reader locals 39-41, value case 66-94. The `raw is item.source` arm (`type/this.cs:288`)
correctly catches `wire : source` (source `IsLeaf => true`, so it passes the
`item.@this{IsLeaf:false}` arm at :282) and dispatches `Declared`. Issue 3 confirmed real
— `value.Reader` structural pulls throw `NotSupportedException` (`reader.cs:77-78`), which
content sources now hit; the catch-filter addition is required.

---

Bottom line: fix the string-vs-wire routing (split by token kind, drop the template
condition) and resolve `_owner` via the registry, and the plan is implementable as
written.

— coder
