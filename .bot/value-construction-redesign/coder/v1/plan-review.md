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
- Under the reroute, `set` must hand the ctor the **raw** value (`sourceValue`), not the eagerly-`converted` one. **CORRECTION (see §7):** I earlier said `sourceValue`-already-native "falls out of §1 as the hold case, no special handling." That is **wrong** — when the materialized value's type ≠ the declared type, holding skips the conversion. This is the §7 flaw.

---

## Net

Plan holds. Before sequencing into stage files:
1. Restate the constructor predicate as the **three cases** in §1 (source format chosen by type — scalar=text/plain, container=application/plang).
2. Stage 1 enumerates the **real** reachable set (include file/directory/url/permission; table already covered) and mirrors **`Reader.cs`** (ITypeReader), not `Default.cs`.
3. `choice` is its own sub-task — enum-name keyed, ValidValues-validated.
4. Merge order confirmed with Ingi before Stage 5 deletions.

Everything else in the plan is ready to go.

---

# Addendum v2 — second crawl (post-v3 plan)

Re-crawled the live construction/read path against plan v3. The from-raw redesign mechanically checks out (verified below). **One blocking flaw found** (§7) and one dangling reference to fix (§6).

## Verified sound (the headline win stands)

- **Scalar from-raw works.** `value.Reader` (`channel/serializer/value/reader.cs`) *parses* a raw string: `Int()`/`Long()`/`Double()` do `int.Parse(Str(), Inv)` etc. So `"5" + {number}` → `source(text/plain)` → `Text.Read` → `value.Reader` → `number/serializer/Reader.cs` → `5`. One conversion, no throwaway `text`. ✅
- **Container from-raw works.** `source(application/plang)` → `plang.Read` (`channel/serializer/plang/this.cs:255`) UTF8-encodes `source.Raw` into a `Utf8JsonReader` → `json.Reader` → `dict/serializer/Reader.cs` `BeginObject()`. The format split (text/plain vs application/plang) is exactly the scalar-vs-structural seam — `value.Reader.BeginObject()` *throws by design*, so the split is load-bearing, not cosmetic. ✅
- **Format keys resolve.** `serializers["text/plain"]` → Text serializer (scalar); `serializers["application/plang"]` → plang serializer (json). Both registered (`channel/serializer/list/this.cs:133,170`). ✅

## §6 — `byte[]` format reference is wrong (fix the dangling pointer)

Plan case 3 puts `string`/`byte[]` together under `text/plain`, and the predicate parenthetical says "`byte[]` follows `FromRaw`'s existing format logic."

- `text/plain` is wrong for `byte[]` — `byte[] as image` is not text.
- `FromRaw` is the **wrong reference**: it's the `dict`/`list` container factory (`dict/this.Convert.cs:6`, `list/this.Convert.cs:29`), `IDictionary`/`IEnumerable` → native. There is no `source.FromRaw`.

The byte-backed readers already answer this — format comes from the type's kind→mime:
```csharp
// image/serializer/Reader.cs:23, Default.cs:27
string mime = ctx.Context?.App.Format.Mime("." + (kind ?? "")) ?? $"image/{kind}";
```
**Fix:** drop `byte[]` from case 3's `text/plain` bullet and delete the `FromRaw` parenthetical. Let Stage 1's reachable trace assign the byte[] format from the declared type's kind→mime. Never `text/plain` for bytes.

## §7 — BLOCKING: the fork converts from-RAW only; three call sites feed it an already-built value of the WRONG type, and "native → hold" silently skips the conversion

The four-case fork handles *construction from a raw form*. But three live call sites do not pass a raw form — they pass an **already-materialized value of a different type** that needs **conversion**:

| Call site | What it hands the type stamp | Today |
|---|---|---|
| `data.Declare(type)` (`builder/code/Default.cs:927,943`) | `p`'s `_item` is **already a built `text`** — `p.Peek() as text.@this` is read at `:934` | `type.Build(_item)` converts `text → number` |
| `validateResponse.cs:222` | `resolved` is the materialized LLM value (`resolved as text.@this`, `:218`) | `p.Type.Convert(resolved, ctx)` — does the text convert to the target? |
| `set %y% = %x% as number` (`set.cs:248`) | `sourceValue = await Value.Value()` — a **materialized** built value; the type-matches case already returned at `:240-246`, so the fall-through is exactly type-**differs** | eager `type.Convert(converted)` |

Run each through plan v3's fork: `value` is a built `text.@this` (a wrong-typed native), not a raw string. `json.Parse(text.@this)` returns it untouched (only natives-out `JsonElement`/`JsonNode`), `Create` passes it through → **case 2 "already-native → hold as-is."** The declared type is **never applied**. Consequences:

- **`Declare`:** stamps `number` but holds the `text` — type silently not applied.
- **`validateResponse`:** the plan reroutes to "construct with declared type + `await data.Value()`." But case 2 holds the value and `Value()` succeeds → **the bad-literal check becomes a no-op.** `"abc" as number` would pass build validation.
- **`set` (type-differs):** the plan's leaf-trace explicitly says "`sourceValue` already native → already-native → hold, no special handling." That is the bug verbatim — it drops the `as number` conversion on a materialized `%var%`.

**Root:** the fork conflates two jobs:
1. **construct from a raw form** → converges on `source` (the real double-convert; headline win — keep).
2. **re-type an already-built value** → needs the per-type `Convert` hook applied to a value. *That is exactly what `Build`/`Judge` did.*

Case 2 ("already-native → hold") is correct **only when the native value's type already equals the declared type.** When it's native-but-wrong-type, the value must be **converted**, not held. So **`Build` cannot be deleted outright** — the "apply a type's Convert hook to an existing item" operation survives for these three sites (it can be renamed/relocated onto the type or the value, but it stays).

Why "mint a `source` from the built value" does **not** rescue this: to mint `source(raw, type)` you need the raw form, and a built `text` has **no clean raw accessor** — only `ToString()` (display edge, `text/this.cs:158`) and `Clr` (CLR lowering, `:162`). Routing through `ToString()` reintroduces the throwaway-text round-trip via the *display* edge (the exact asymmetry the coder's report flagged as unsolved), and breaks entirely for a built non-text value (a re-declared dict's `ToString()` is not its raw).

**Fix for the plan:**
- Split case 2: **native AND type == declared → hold; native AND type != declared → convert** (the surviving per-type `Convert` hook on the value).
- Demote the leaf-trace claims: `Build`/`Judge` do **not** fully dissolve. The from-RAW double-convert dissolves into `source`; the **re-type-a-built-value** path keeps a (renamed) convert-existing-item operation, used by `Declare`, `validateResponse`, and `set`'s type-differs fall-through. Stage 5's "delete `Build`/`Judge`" must become "delete the *eager from-raw route*; keep the convert-existing-item hook under whatever name."
- Trace `builder/code/Default.cs:920-945` explicitly: the `%var%`-skip guard at `:934-935` reads `p.Peek() as text.@this` and tests `StartsWith("%")` — it assumes a **text face**. If `Declare`'s input ever becomes a `source` instead, `Peek()` returns the raw string (not a `text.@this`), `sv` is null, and the guard mis-fires. Whatever the fix, keep `Declare`'s input shape consistent with this guard.

## Net (v2)

The from-raw redesign is correct and the headline double-convert win is real. But the plan **over-claims** that `Build`/`Judge` can be deleted: three call sites re-type an **already-built** value, which the fork's "native → hold" silently no-ops. Restate case 2 as a type-match split, keep a convert-existing-item operation (renamed) for those three sites, and fix the `byte[]`/`FromRaw` reference. With those, the plan is sound.

---

# Addendum v3 — for the architect (Build survives; reconcile the wording)

Two coder/Ingi decisions while sequencing the stages, both touching the plan's framing:

1. **`Build` is KEPT, not deleted** (Ingi, 2026-06-29). After reading `object_pattern_formal.md`, the OBP-correct shape puts the construction fork **on the type, not in the Data ctor** (Rule #1 — behavior on the owner). The type already owns construction via one call — `type.Build`. So the Data ctor + `Declare` **delegate** in one line, and `Build` is **reimplemented** to fork internally (raw → `source` with a type-owned `RawFormat` noun; built → 2a hold / 2b `Convert`; null → typed-absence). The throwaway-`text` + reflection die *inside* `Build`; the method stays as the single construction entry.

2. **Plan wording to reconcile:** the leaf-trace / demolition still say "gut Build's from-raw scaffolding" and Stage 5 "delete dead machinery." Consistent in spirit (the from-raw eager route dies), but the *method* `Build` survives — please reword so the plan and the stage files agree before Stage 1 lands: Stage 5 deletes `Judge`/`Deserialize`/the `source` context-less fallback and thins the 2-arg `Convert`; it does **not** delete `Build`.

The stage files (`stages/`) already reflect this. No design change — just the name/location of where the fork lives, and that `Build` is the keeper.
