# clr + kinds under type — structured data becomes navigable/convertible without materializing

**Branch:** `clr-navigators` (off `variable-as-value`).
**Status:** plan, for coder. Model settled with Ingi (2026-07-07), grounded against coder's review (`.bot/clr-navigators/coder/review-of-architect.md`).
**Author:** architect.

---

## Why

`plang build` dies at `BuildStep/Start.goal:6` (`IndexNotSet: %planStep.index% is null`) because `%plan%` — the LLM planner result — is opaque: it rides the wire as an `object`-typed value and reads back non-navigable, so `%plan.steps%` returns garbage and the foreach iterates nothing.

Two things are wrong, and the fix keeps external structured data as a `clr` (its foreign object) navigated **lazily** — not eagerly narrowed into `dict`/`list`:

1. **The value's real type is masked.** `os/system/builder/BuildGoal/.build/plan.pr:652` is `variable.set(Name=%plan%, Value=%!data%, Type=object)`. `Type=object` stamps the plan's Data as the apex type — dropping its true `item/json` — so the wire carries `object` and read-back has nothing to reconstruct from.
2. **A `JsonElement` isn't navigable.** Even with the right type, navigation must know how to walk a json value.

The design: a `clr` carries the foreign object; **a `kind` (nested under its type) owns what you can do with a value of that kind** — navigate it, load it, build it (convert). Reached at `context.App.Type[t].Kind[k]`.

---

## The model

### 1. `clr` carries `(object)`; its type and kind are derived

A `clr` holds its foreign object. It is **not** told its kind — it asks the type system what kind the object's CLR type is (`JsonElement → json`, `XElement → xml`, anything else → `*`). A producer *may* stamp a kind (for an ambiguous raw form like a text string that is `md` vs `json`), but for a structured host the creator needn't — that knowledge belongs to the type system, not the caller. `clr` does not navigate itself; it delegates to its `(type, kind)`.

### 2. Kinds nest under their type — `Type[t].Kind[k]` owns the behavior

A **kind** is not a flat parallel registry beside `type`; it lives **under** its type. `type.@this` already carries a `Kinds` vocabulary list (json/int/decimal…) — today just names. It gains a `Kind[k]` **accessor** returning the behavior owner for that `(type, kind)`:

```
context.App.Type["item"].Kind["json"]   → navigate/enumerate/load a json value
context.App.Type["item"].Kind["*"]      → navigate any object by reflection  (the catch-all)
context.App.Type["text"].Kind["html"]   → build html-text from a source (convert)
context.App.Type["audio"].Kind["mp3"]   → build mp3-audio from a source
```

- **Navigation kinds live under `item`** — the apex type, whose kinds are the formats (`json`, `*`, later `xml`/`yaml`).
- **Convert kinds live under their target type** — `text/html`, `audio/mp3`.
- `Type[t].Convert(source)` uses the type's **default kind** (the type decides its default): `Type["audio"].Convert(md)` → audio's default → convert. `Type["audio"].Kind["mp3"].Convert(md)` is the explicit form.

This is the resolution of "does a kind own everything, or split navigate vs convert": neither — **a kind owns behavior, but it is addressed under its type**, so `Kind["dict"]` never sits *beside* `Type["dict"]`, and convert reuses the existing `type.@this.Convert` door reached *through* the kind. It also dissolves the "namespace collision" in coder's review: there is one kind concept (the existing `app.type.kind` value token, extended), not a second `@this`.

### 3. Navigate — the value's `(type, kind)` walks it (option b)

A value's own `(type, kind)` navigates it. Only the kind knows its path language, so the kind resolves variables it meets in the path itself (option b — for `kind=magic`, `planStep.index` might be magic, not a variable). The base kind gives a default plang-path walk; a kind with a different path language (jsonpath, css) overrides it.

- **Takes the raw `object` + the `path` object** (already tokenized once by `app.variable.path.Parse`) — walks `path.Segments`, no re-parse, no `ToString`.
- Resolves a bracket variable via the existing `Segment.Index.ResolveKey(store)` (`variable/path/Segment.cs:61`) — **do not** write a second resolver.
- **Container → a `clr`; scalar leaf → its plang scalar** (`number`/`text`/`bool`/`null` by `ValueKind`). Never a `clr` wrapping a scalar.
- The kind receives only the **tail** relative to the clr: `Variables.Get("user.address.zip")` splits `root="user"`, then `GetChild("address.zip")` — so the kind sees `address.zip`, never the root.

### 4. The parser handoff

`data.Navigate(path)` (`data/this.Navigation.cs`): when the value is a `clr`, hand it the `path` and let its `(type, kind)` walk the rest.

```csharp
if (_item is global::app.type.clr.@this c)
    return await c.Navigate(this, path);      // → Type[c.type].Kind[c.kind].Navigate(Value, path, parent, ctx)
```

Native `dict`/`list` and item types keep the existing per-hop walk for now — routing them through the kind registry too is later.

### 5. Blocker 1 — the apex must not mask a richer type

The root fix (Ingi): declaring a value's type as `object`/`item` — the **apex/universal** — must **not** overwrite the value's intrinsic type. "This is an object" is always true and carries no information. `variable.set(Type=object)` on an `item/json` value must leave it `item/json` (not demote it), so the wire carries `item/json` and the existing kind-routing reconstructs the `clr` on read-back.

This is the cause; a reader-side heuristic (route an `object`-typed wire value to json by container-ness) is only a symptom patch and is **not** needed once the apex stops masking. Coder: pin the exact seam — whether `variable.set` should skip re-stamping when the declared type is the apex and the value is more specific, or the mint path should treat apex-declaration as "keep the value's type." (Converting a value *to* `System.Object` is already identity — `TryConvert` returns it unchanged — so the loss is the Data's *advertised* declared type, not the value.)

### 6. The producer hands raw + kind; the kind loads it

A producer does not branch per format. `context.Ok(raw, kind)` routes to the kind's loader: `json` → a `clr`; `md` → `text`. The json load reuses the **single** json parse owner (`object/serializer/json.cs:Read`) — it does not open a second `JsonDocument.Parse`. `OpenAi`'s fresh and cached paths collapse to `context.Ok(extracted, kind: format)`; no `format == "json"` ladder. (Rename the local `effectiveFormat` → `format`.)

### 7. Convert — the outbound `(type, kind)` owns it

Conversion is owned by the **target**, not the source: `text(md) → audio` is owned by `audio`. The call is on `Data` (which carries its own context), dispatched through the type→kind structure and reusing the existing `type.@this.Convert` / `Conversions` door:

```csharp
// Data.Convert(kind) — the target kind (under its type) builds itself from this source.
public ValueTask<@this> Convert(text kind)
    => _context.App.Type[TargetType(kind)].Kind[kind].Convert(this, _context);
```

- `Type["text"].Kind["html"].Convert(md)` — "text of the kind html, convert this md into it."
- `Type["audio"].Convert(md)` — audio's default kind; `Type["audio"].Kind["mp3"].Convert(md)` is explicit.
- `json → dict` is `dict`'s convert from a json source (reuse the existing `catalog/Conversion` arm).
- `Build`/`Convert` returns the built value or an **error `Data`** when the target can't build from that source — no boolean probe. Chains (md→html→pdf) are later; a missing conversion fails loud, never silently passes the source through.

Convert ships with the first real converter — it is **not** in the v1 unblock (see Scope).

### 8. Write-side — read/navigate only for v1

`JsonElement` is immutable; `set %json.x% = 5` (mutate the object) is out of scope. Convert to `dict` when a mutable structure is needed. Flag `set` into a `clr` as an explicit error.

### 9. Guards

- **Container-materializes-to-scalar** — in `source.Value`: if the declared type is a container (`object`/`dict`/`list`) but it materialized to a non-container leaf, throw. The exact round-trip loss fails loudly at the point.
- **Double-wrap guard** — already in the `clr` ctor (`clr/this.cs:26`) and `type.Create` (`type/this.cs:445`). Keeper.

---

## Leaf trace — the incumbents this plan gives a new owner

| Incumbent (today) | Does | Disposition |
|---|---|---|
| `clr.Navigate` (`type/clr/this.cs`) | reflects C# properties on the object by key | pure delegation to `Type[type].Kind[kind].Navigate(...)`; the reflection body relocates into the `item`/`*` kind. No reflection or `is JsonElement` switch left on `clr` |
| `app/type/kind/this.cs` (the kind value token) | names a kind, maps kind→type via the reader registry | **extended** — a kind gains navigate/load/build behavior, reached as `Type[t].Kind[k]`. No second `@this` |
| `app/type/kind/Hooks.cs` (`KindHooks.Of`) + `clr.Mint`'s `ResolveName` | CLR type → kind | the single CLR→kind mapping; reuse it for kind derivation — do **not** add a third path. (The `KindHooks.Of` name is anti-OBP jargon; rename if it surfaces.) |
| `variable.set` `Type` clause | mints the declared `Data<T>` (masks a richer type with `object`) | apex-declaration (`object`/`item`) must not demote a more specific intrinsic type (§5) |
| `data/this.Navigation.cs` — `Index` segment | resolves `%[var]%` mid-walk (option a) | stays for native/item hops; for a clr path, the kind resolves via `Segment.Index.ResolveKey` (option b) |
| `object/serializer/json.cs:Read` | walks `JsonElement` → native `dict`/`list` via `Parse` | wraps in `clr` instead (the single json parse owner). `Parse` (the universal DOM narrower) **stays** — many callers |
| `type.@this.Convert(value, ctx)` + `Conversions` (`type/this.cs:187`, `catalog/this.cs:56`) | build a value from another | the convert door reached via `Type[t].Kind[k].Convert(source)` — reuse, don't duplicate |
| `OpenAi` result construction (fresh + cached) | `context.Ok(TryParseJson)` → unstamped/masked | `context.Ok(raw, kind)` → the kind loads it; no per-format branch |

---

## Demolition worklist

**Dies now (v1):**
- The reflection body inside `clr.Navigate` — relocates into the `item`/`*` kind; `clr.Navigate` becomes pure delegation.
- The type-masking at `variable.set(Type=object)` — the apex stops demoting a richer intrinsic type (§5).
- The `JsonElement → dict` walk in `object/serializer/json.cs:Read` — wraps in a `clr` (the `Parse` DOM walker stays).
- OpenAi's per-format result construction — collapses to `context.Ok(raw, kind)`.

**Stays (explicitly not touched):**
- `app.variable.path.Parse` — the one plang-path tokenizer; a kind walks `path.Segments`.
- `Segment.Index.ResolveKey` — the one bracket-variable resolver (reuse, don't reimplement).
- `item.serializer.json.Parse` — the universal DOM narrower (Data ctor, `type.Create`, dict/list/object readers, Fluid).
- Full-match `%ref%` → `variable`, born in `type.Build` (`type/this.cs:265`) — a different branch from the deferred read; the pivot can't turn a `%ref%` into a clr.
- Native `dict`/`list` construction for plang-authored values (`%x% = {a:1}`) — stays native.

**Deferred to their own branches (see Scope):** `identifiers → text`, `Peek → item.@this`, `Convert`.

---

## Reuse — don't build parallels (from coder's review, confirmed)

- **`Segment.Index.ResolveKey(store)`** already resolves a bracket variable — call it; do not write a `Key(i, ctx)`.
- **One json parse owner** — `object/serializer/json.cs:Read`. The kind's "load" delegates to it; no second `JsonDocument.Parse`.
- **One CLR→kind mapping** — `KindHooks.Of` + `ResolveName`. Reuse for kind derivation; no third `ResolveXxx`.

---

## Scope — v1 vs the direction

**v1 (unblocks `plang build`):**
1. **Apex-doesn't-mask** (§5) — this alone likely clears `IndexNotSet`.
2. **json navigation** under `item` — `Type["item"].Kind["json"]` (navigate/enumerate), and the `*` reflection kind (relocating clr's reflection). Proves the `Type[t].Kind[k]` structure end-to-end.
3. `clr` derives its `(type, kind)` (reuse `KindHooks`/`ResolveName`), pure delegation.
4. The reader pivot (`Read` → clr) + the parser handoff.
5. Container-materializes-to-scalar guard.
6. `context.Ok(raw, kind)` producer door.

**Deferred (do not pull into v1):**
- **`identifiers → text`** — its own branch after green. Deep (wire serializer, primitive tables, `Canonicalise`/`Compare`, `text`'s own `string Kind`), not a rename sweep. `text` keys the registry fine (`GetHashCode`), so v1 doesn't need it.
- **`Peek → item.@this`** — its own `source` pass. Not mechanical: `source.Peek()` (`item/source.cs:90`) returns raw CLR by contract (a source's sync face is its unparsed raw, parse behind `Ready`); tightening it changes the lazy-materialization contract.
- **Convert** — ships with the first real converter, on the target `(type, kind)` (`Type["text"].Kind["html"]`, `Type["audio"]`), reusing `type.Convert`.
- yaml/xml kinds; the convert graph with composition; the write-side.

---

## OBP validation pass

| New/changed surface | Verb+Noun check | Object-decomposition check |
|---|---|---|
| `clr(object)` | one word — ok | carries the object whole; type + kind derived from it, not a flat copy |
| `Type[t].Kind[k]` | `kind` — one word — ok | kind nested under its type (no parallel registry); behavior on the kind, not a switch in a registry |
| `Kind[k].Navigate(obj, path, parent, context)` | single words — ok | raw object + whole `path` in, `Data` out; walks `path.Segments`, no re-parse |
| `Kind[k].Enumerate` / `Load` / `Convert` | single words — ok | whole `Data`/raw in, `Data` out; `Convert` returns an error `Data` when it can't build |
| `Data.Convert(kind)` | "Convert" verb-as-noun — ok | on the carrier; no `context` param (Data carries its own); routes to `Type[t].Kind[k].Convert(this)` |

Behavior is owned by the `(type, kind)`, reached at `context.App.Type[t].Kind[k]` — not a "mime registry"; `mime` is what a kind maps to, an impl detail.

---

## Settled decisions

1. **Kinds nest under type** — `context.App.Type[t].Kind[k]` owns navigate/load/build for a value of that `(type, kind)`. Navigation kinds under `item`; convert kinds under their target type. No parallel-to-type registry; the existing `app.type.kind` value token is extended, not duplicated.
2. **Convert — the outbound owns it**, reusing `type.Convert`: `Data.Convert(kind)` → `Type[t].Kind[k].Convert(this)`. `Type[t].Convert` uses the type's default kind.
3. **Blocker 1 = apex-doesn't-mask** — declaring `object`/`item` never demotes a value's intrinsic type. The reader container-heuristic is unnecessary.
4. **Kind derived, not required** — `clr` asks the type system (`KindHooks`/`ResolveName`); reuse, no third CLR→kind path.
5. **v1 = navigation + the apex fix**; `identifiers→text`, `Peek→item`, and `Convert` are deferred to their own branches after the unblock is green.

---

## You own this (coder)

Every shape/signature/path here (and in `code-draft.md`, the authoritative spec) is a **suggestion** — you own the final form. Highest-judgement spots: the exact seam for apex-doesn't-mask (§5); where the `Kind[k]` behavior accessor and the per-kind behavior live given the existing `app.type.kind` token; and whether a base `kind` template method is worth it for `json` + `*`. Your review's grounded corrections are folded in — the direction that must survive: a `clr` stays a `clr`; a `(type, kind)` owns navigate/load/build; convert's outbound owns it reusing `type.Convert`; the reader pivot must never turn a `%ref%` into a `clr`.
