# Coder review of architect plan v1 — for the architect

**Branch:** `value-construction-redesign`
**Author:** coder · 2026-06-29 · reviews `../../architect/v1/plan.md`
**Status:** plan is sound — ready to sequence into stages once the predicate is restated as **three cases** (§1) and Stage 1's reachable-set + reader template are corrected (§2–§3). Verified against live code at every cited spot.

---

## Verdict

The core insight is right: kill the eager `Build`/`Judge` door, mint a `source` declared as the type, materialize once through the reader. Invariants 1–4 are the correct targets. Line refs check out (`data/this.cs:184` unconditional lift, `:193-212` the context fork, `source.cs:120-129` the context-less fallback, `set.cs` eager `type.Convert`→re-`Build`). One real gap in the predicate, plus three smaller corrections below.

---

## §1 — The predicate is three cases, not raw-vs-native (the crux)

The plan frames the constructor judgement as *raw form → `source`* / *native → hold*. But a raw **string** splits two ways, and the construction `source` as described (text/plain + `channel/serializer/value/reader.cs`, a single scalar token) only serves one of them:

```
"5"                    as number  → source(text/plain) → value.Reader → number/Reader.cs reads ONE string token    ✅
'{"a":1}' (raw string) as dict     → source(text/plain) → value.Reader → dict/Reader.cs calls reader.BeginObject()  ❌
{a:1} (JsonElement)    as dict     → json.Parse() already returns native dict → hold as-is                          ✅
```

`dict/serializer/Reader.cs:21` does `reader.BeginObject()` — it needs the JSON token stream, not a scalar token. Today the JSON-string→container conversion only works because it routes `text → dict.Convert(text)` — the eager door this branch deletes. And `json.Parse` returns a bare string **unparsed** (`item/serializer/json.cs:131` `return value;`), so Parse does **not** rescue it.

**Ingi settled this (2026-06-29): dynamic `%jsonStr% as dict` must keep working.** So the predicate is:

| Inflow | Construction |
|---|---|
| raw **scalar** string / `byte[]` + scalar type | `source(format = text/plain)` → `value.Reader` — **this is the double-convert win** |
| raw **string** + **container** type (dict/list/object/item) | `source(format = application/plang)` → json reader → `BeginObject()` |
| already-native (Parse returned a dict/list/number/…) | hold as-is — no `source`, no re-convert (containers already skip the coercion today) |

**The one addition to the architect's predicate:** the construction `source` **picks `_format` from the declared type's reader mode** (scalar → `text/plain`; container → `application/plang`), rather than always `text/plain`. Cleanest implementation: keep `json.Parse(value)` (it already natives-out `JsonElement`/`JsonNode` containers), then mint a `source` only for a still-raw string/`byte[]`, choosing the format by type. This is the predicate the coder must nail; it replaces the `Build`/`Judge` fork.

---

## §2 — Reader-coverage worklist: two corrections

**(a) Wrong template.** The plan says model the new readers on `number/serializer/Default.cs:62`. But construction hits `channel/serializer/Text.cs:79` → `Readers.Reader()` (`type/reader/this.cs:109`) → the **`ITypeReader`** registry — *not* the `Of()` / `Default.Read` whole-payload delegate. The shape to mirror is **`number/serializer/Reader.cs`** (the `ITypeReader` pull). For date/datetime/time it is a one-liner:

```csharp
// app/type/date/serializer/Reader.cs  (mirror datetime, time)
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;
    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx) where TReader : global::app.channel.serializer.IReader, allows ref struct
        => reader.Null()
            ? new global::app.type.@null.@this("date", kind)
            : global::app.type.date.@this.Convert(reader.String(), kind, ctx.Context);
}
```

`Default.cs:62` is a different contract (`object? Read(object raw, …)`, the `Of()` path) — do not send the coder there.

**(b) Reachable-set is wider than four types.** The plan's table lists date/datetime/time/choice as the gaps. Cross-checking the tree:

- It **omits `table`** (it *does* ship a `Reader.cs`) and lists `object`/`item`/`code` as having a `Convert` hook — they don't (no `this.Convert.cs`); they ride the lazy path fine regardless.
- More important: **`file`, `directory`, `url`, `permission` have a `serializer/` folder but only `Default.cs` — no `Reader.cs`.** If `as file` / `as url` / `as directory` / `as permission` construction is reachable, `Readers.Reader()` throws `NotSupportedException` (`type/reader/this.cs:119`), and that exception is **not** in `source.Value`'s catch list (`source.cs:98` — only `JsonException`/`FormatException`/`InvalidOperationException`), so it throws into the courier instead of failing the Data cleanly with `MaterializeFailed`.

**Stage 1 action:** enumerate the *actual* reachable `(type, kind)` set from `as T` construction (coder will trace this), and either add a `Reader.cs` for each, or — if a type is provably not a construction target — record why it's excluded. Do not assume the four-row table is complete.

---

## §3 — `choice`: endorse "investigate first"; it is the odd one out

Right call to gate it. Specifics found:

- `choice` has **no `serializer/` folder at all**, and its `Convert` lives in `choice/this.cs` (not a `this.Convert.cs`).
- It is keyed by the **enum's name**, not `"choice"` (`type/this.cs:288-296`, `compare.FamilyOf(Name)`).

So its `ITypeReader` must register under the **enum name** in `_runtimeTyped` (runtime registration, not the generated convention scan), take the scalar string token, and validate against **`ValidValues` membership** — not a string-ctor (closed-enum rule; see `feedback_validvalues_vs_construction`). The scalar-3 template will not fit; treat `choice` as its own sub-task within Stage 1.

---

## §4 — Merge order with read-path-unification: endorse, with the conflict surface named

Both branches edit the same two places:

- `source.cs:120-129` — the context-less string fallback dies **here** *and* is flagged to die on read-path-unification.
- the reader registry (`type/reader/this.cs`).

Whoever lands second rebases. Confirm order with Ingi before Stage 5 deletes anything, exactly as the plan states. A late rebase is cheaper than a conflict-heavy one.

---

## §5 — Small accuracies (non-blocking)

- `set.cs` reroute confirmed accurate: it eager-`type.Convert(converted, Context)`s, then `new data.@this(name, converted, type)` re-converts in the ctor — the double/triple touch is real. The `keepAsIs` rule (richer-type-under-different-name, e.g. image→path slot) is genuine semantics; the plan correctly says keep it. Note `keepAsIs` already passes `type = null` to the ctor, so it never enters the typed-construction branch — consistent with the redesign.
- Under the reroute, `set` must hand the ctor the **raw** value (`sourceValue`), not the eagerly-`converted` one. But `sourceValue` can itself already be native (line ~248 `if (Value.RawUntouched) sourceValue = await Value.Value()`), which is just the "already-native → hold" predicate case — no special handling, it falls out of §1.

---

## Net

Plan holds. Before sequencing into stage files:
1. Restate the constructor predicate as the **three cases** in §1 (source format chosen by type — scalar=text/plain, container=application/plang).
2. Stage 1 enumerates the **real** reachable set (include file/directory/url/permission; table already covered) and mirrors **`Reader.cs`** (ITypeReader), not `Default.cs`.
3. `choice` is its own sub-task — enum-name keyed, ValidValues-validated.
4. Merge order confirmed with Ingi before Stage 5 deletions.

Everything else in the plan is ready to go.
