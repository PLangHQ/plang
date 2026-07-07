# clr + kind navigators — structured data becomes navigable/convertible without materializing

**Branch:** `clr-navigators` (off `variable-as-value`).
**Status:** plan, for coder. Direction settled with Ingi (session 2026-07-07). Builds on coder's `design.md` in this folder.
**Author:** architect.

---

## Why

`plang build` dies at `BuildStep/Start.goal:6` (`IndexNotSet: %planStep.index% is null`) because `%plan%` — the LLM planner result — is opaque: it rides the wire as an `object`-typed value, is read back as `source(object, text/plain)`, and is never re-parsed, so `%plan.steps%` returns garbage and the foreach iterates nothing. Coder confirmed the mechanism (design.md "Confirmed root cause"): `data/reader/this.cs:79-80` picks the read format by *token shape* (a `String` token → `text/plain`) instead of by the declared type, and `text/plain` never re-parses the JSON.

The narrow fix would be to eagerly parse that JSON into a native `dict`. **We are deliberately not doing that.** Ingi's decision: external structured data (json/yaml/xml) stays as its `object` in a `clr` **stamped with a `kind`**, and becomes navigable / convertible **lazily** through a per-kind registry. Rationale:

- **Uniform.** json/yaml/xml are all structured data: `clr` + `kind` + one navigator each. No special json path, no per-format eager conversion. Register a navigator = support a format.
- **Cheaper.** The parse already happened (that *is* the validation — "is this valid yaml/xml") and the DOM is in memory. Navigating the `object` directly skips building a parallel `dict`/`list` tree — the CPU + allocation we save is the whole point.
- **The kind owns its own path language.** The json kind can interpret jsonpath; an html kind (over `text`) could interpret css/xpath. The value decides what a path segment *means*.

---

## The model

### 1. `clr` carries `(object, kind)`

`clr` today derives `kind` from the CLR type name in `Mint()`. It gains a **stamped `kind`** (a `text` — see §Identifiers) set at construction. Unstamped → derived from the CLR type → resolves to the `*` (reflection) navigator. A producer that knows the format stamps it: `json` / `yaml` / `xml`. The stamped kind is what `Mint()` surfaces, so the wire carries `{item, json}` and a round-trip reproduces the same clr. `clr.Navigate` is **pure delegation** — resolve the navigator, call it; no reflection or `is JsonElement` switch remains on `clr` (the reflection body moves into the `*` navigator, §3).

### 2. The kind registry — a kind owns the behavior for its values

A **kind** is a first-class entity in the type system: `context.App.Type.Kind[k]` (the registry hangs off `App.Type`, beside `Readers`/`Conversions`). A kind owns what you can do with a value of that kind — **navigate** it, **load** it from raw, **build** it from a source (convert). Discovered by "is a `kind`" (a kind declares its own `Kind`, so no namespace filter). `Kind[unknown]` falls back to the `*` kind.

```
context.App.Type.Kind[k]
  "json"  → json kind         (navigate + enumerate + load)
  "*"     → reflection kind    (navigate any object by reflection)  ← catch-all
  "audio" → audio kind         (build audio from text)   (later)
  "yaml"/"xml" → later          (one file each: extend the kind base, declare Kind)
```

`clr` does not store its kind unless a producer stamps one — it asks `Type.KindOf(objectClrType)` (the CLR bridge: `JsonElement → json`, else `*`). The creator needn't know the kind; that's the type system's job.

### 3. A kind navigates its values — owning the path language AND variable resolution

**Decision (Ingi): option (b).** A kind's `Navigate` receives the remaining path and resolves variables *itself*, because only the kind knows its own syntax — for `kind=magic`, `planStep.index` might be magic, not a variable; only that kind can tell. The kind base gives a default plang-path walk; a kind whose path language differs (jsonpath, css) overrides `Navigate` wholesale.

```
Kind[k].Navigate(obj, path, parent, context) → Data   // walk obj by the (already-tokenized) path; resolve vars via context
Kind[k].Enumerate(obj, context) → items               // for foreach; each element a Data (clr for containers, scalar leaf otherwise)
Kind[k].Load(raw, context) → Data                     // raw payload → a value of this kind (json parses to a clr; md → text)
Kind[k].Build(source, context) → Data                 // convert: build a value of this kind from a source (audio from text)
```

- **`Navigate`/`Load` take the raw `obj`, not an `item`.** A kind turns a raw host (`JsonElement`, POCO, raw text) into plang values — input is raw, output is `Data`. Wrapping the input as `item` would just force an immediate unwrap.
- **`Navigate` takes the `path` object, not a string.** `app.variable.path.Parse` already tokenized once; the kind walks `path.Segments` directly — no re-parse, no `ToString`.
- The kind resolves a plang variable it finds in the path (the `%step.Index%`-in-a-bracket case) through `context.Variable`. No separate resolver param; the context is the hook.
- **Container vs scalar:** a container node stays a `clr`; a scalar leaf resolves to its plang scalar (`number`/`text`/`bool`/`null` by `ValueKind`). Navigate/Enumerate never return a `clr` wrapping a scalar.
- **Identifiers are `text`; `System.Type` is confined to the CLR bridge.** `Kind` (and every type identifier) is a plang `text` — it keys the registry (value `Equals`/`GetHashCode`) and compares to string literals via its implicit operator. `System.Type` appears only in `Type.KindOf(clrType)`, the CLR↔plang bridge — no kind surface takes it. This makes `type.@this.Name`/`.Kind` `text` too (see §Identifiers).

### 4. The parser handoff

`data.Navigate(path)` (`data/this.Navigation.cs`) walks generically until a hop lands on a **kinded-navigable `clr`** (`_item is clr c && c.Navigable`), then hands the **`path` object** to that clr's navigator, which walks the rest in one call:

```
%plan.steps[step.Index].actions%
  plan            → generic hop → clr(kind=json)   (Navigable)
  steps[step.Index].actions   → path object → the json kind
                                 (resolves step.Index via context.Variable, then walks path.Segments)
```

- Native `dict`/`list` and item types (`goal`, …) keep the generic per-hop walk for now — the handoff only fires for a `clr` whose kind has a registered navigator. That's why the bug fix is bounded: `goal` is an item type and already navigates fine; only `%plan%` was broken.
- **Not a second plang tokenizer.** `app.variable.path.Parse` stays the one tokenizer for *plang* paths (parent-branch rule); the navigator walks the resulting `path.Segments`. A future jsonpath/css navigator that wants a *different* language reads the raw form off the path itself — its own concern.
- No `ToString`, no re-parse: pass the `path` object; the navigator walks `path.Segments`.

### 5. The pivot that makes it fall out: what the `(item/object, json)` reader returns

Today `object/serializer/json.cs` `Read` narrows a `JsonElement` → native `dict`/`list` (via `item.serializer.json.Parse`). **Change that one line to produce `clr(JsonElement, kind=json)`.** Then:

- `file.read .json`, http json, `llm.query` all uniformly land as `clr(kind=json)`.
- The wire round-trip that caused the bug fixes itself: the plan crosses as `{type:{item,json}, value:<raw json>}` and read-back runs the `(item,json)` reader → `clr(kind=json)` again.
- **`item.serializer.json.Parse` is NOT deleted** — it's the universal DOM narrower (`Data` ctor, `type.Create`, the `dict`/`list`/`object` readers, Fluid all call it for raw/authored values). Only the *reader* path (`Read`) stops calling it. Authored `dict`/`list` literals stay native.

Paired with fixing `data/reader/this.cs:79-80` to route by the **declared type/kind**, defaulting to **text** (not `application/plang`) when there's no kind — an undeclared value is safest as text (the type decides); internal-wire values are `@schema`-marked and read by a different branch. Constraint from the parent branch: a full-match `%ref%` still borns a `variable` and is **never parsed** — only genuine *content* of a structured declared type becomes `clr(kind=json)`.

### 6. The producer hands raw + kind; the kind loads it

A producer does not branch per format. It hands the raw payload and the kind it asked for, and the kind's loader builds the right value: `context.Ok(extracted, kind: format)` → `Kind[format].Load(extracted)`. `json` → a `clr`; `md` → `text`; an unknown kind → `text`. `OpenAi`'s fresh and cached paths both use this one line, so fresh == cached, and there is no `format == "json"` ladder. (Rename the local `effectiveFormat` → `format`.)

### 7. Convert — the **outbound** (target) owns it, behind the existing door

Convert is a **content transform**, module-dev-facing: an action that wants audio gets `read file.md` (text, `kind=md`) and just asks "give me audio." **The target owns the conversion, not the source** (Ingi): if `text(md)` owned its outbound conversions it would have to know every possible format (audio, html, pdf, …); but if `audio` owns it, `audio` only needs to know how to make itself **from** text. So the owner is the **outbound (type, kind)** — exactly how the per-type `Convert(value, kind, context)` hook already works (`catalog.Convert` → `OwnerOf(targetType).Convert(value, …)` — the *target* builds itself from the value).

- **The target kind's `Build` does it.** `Data.Convert(to)` → `context.App.Type.Kind[to].Build(this)` — the target kind builds itself from the source. `audio` owns "build audio from text"; `dict` owns "build dict from json" (reuse the existing `catalog/Conversion` arm). Adding an output format = one kind; no existing type learns about it.
- **`Build` returns the value or an error `Data` — no boolean probe.** The target kind returns an error `Data` when it can't build from that source; an unknown target kind's default `Build` errors too.
- **Value-facing call:** `await Md.Convert("html")` — no `context` param; everything at an action boundary arrives as `Data`, which already carries its context. So `Data.Convert(to)` routes through `Data`'s own context and the kind registry.
- **Chains later.** md→html→pdf via composition is a graph problem; v1 = direct hops only. A missing conversion fails loud (error `Data`), never silently passes the source through.

### 8. Write-side — read/navigate only for v1

`JsonElement` is immutable; `set %json.x% = 5` (mutate the `object`) is out of scope for v1. When someone needs a mutable structure, they `as dict` (Convert) and mutate the native dict. Flag `set` into a `clr` as an explicit error for now.

### 9. Guards

- **Double-wrap guard** — added, keeper (`data/this.cs` ctor throws on a bare `Data` as value).
- **Container-materializes-to-scalar guard** — proposed, in `source.Value`: if the declared type is a container (`object`/`dict`/`list`) but it materialized to a non-container leaf, throw. This class of round-trip loss (the exact bug) then fires loudly at the point, not three hops later as `IndexNotSet`.

---

## Leaf trace — the incumbents this plan gives a new owner

| Incumbent (today) | Does | Disposition under this plan |
|---|---|---|
| `clr.Navigate` (`type/clr/this.cs`) | reflects C# properties on the `object` by key | becomes **pure delegation** (`Kind[EffectiveKind].Navigate(...)`). The reflection body relocates into the `*` kind — no reflection or `is JsonElement` switch left on `clr`; kind is derived from the object via `Type.KindOf` when unstamped |
| `item.@this.Peek()` | `virtual object? Peek() => this` | tighten to `item.@this Peek()` — always a plang value, never C# null (absence is `@null.@this.Instance`). Every existing `Peek()` already returns `this`, so it's a signature tighten (§Peek) |
| `data/this.Navigation.cs` — `Index` segment (`ResolveKey` + the `IndexNotSet` diagnostic) | resolves `%[var]%` against the variable store mid-walk (option a) | stays for native/item hops (generic walk); for a clr path, resolution moves *into* the navigator (option b) via `context.Variable` |
| `object/serializer/json.cs` `Read` (the (item/object, json) reader) | walks `JsonElement` → native `dict`/`list` via `Parse` | wraps in `clr(kind=json)` instead (§5). The `Parse` DOM walker **stays** (authored `dict`/`list` literals use their own readers, untouched) — only the *reader* path pivots |
| `data/reader/this.cs:79-80` | picks read format by token shape | route by declared type/kind; preserve full-match `%ref%` → `variable` (parent-branch rule) |
| `catalog/Conversion.cs` (JsonElement→dict/list on `As<T>`) | value-model conversion | stays; becomes the `json→dict` arm of Convert (§7), now reachable via the value-facing `Convert` |
| per-type `Convert(value, kind, ctx)` hooks | build a value from another, `kind` unused | folds into the target kind's `Build(source)` (`Kind[to].Build(this)`, outbound-owns); the kind returns the built value or an error `Data` |
| producer result construction (e.g. `OpenAi.context.Ok(TryParseJson)`) | hands raw / unstamped | `context.Ok(raw, kind)` → `Kind[kind].Load(raw)` — no per-format branch on the producer |
| `OpenAi` result construction (fresh + cached) | `context.Ok(TryParseJson)` → unstamped | wrap in `clr(kind=format)` (§6) |

---

## Demolition worklist

**Dies now (v1):**
- The reflection body inside `clr.Navigate` — relocates into the `*` navigator; `clr.Navigate` becomes pure delegation (no inline reflection, no fallback branch).
- The token-shape branch at `data/reader/this.cs:79-80` — replaced by declared-kind routing (default text).
- The `JsonElement → dict` walk in `object/serializer/json.cs` `Read` **for external structured data** — replaced by `clr(kind=json)`. (The `Parse` DOM walker stays; only the *reader* path wraps.)
- OpenAi's unstamped `context.Ok(TryParseJson)` on both fresh and cached paths.

**Dies later (v2, staged — see Scope):**
- The generic `Index.ResolveKey` walk in `data/this.Navigation.cs`, once native/item navigation is generalized through the registry (a navigator resolving via `context.Variable`). Until then it stays — do not delete it in v1.

**Stays (explicitly not touched):**
- `app.variable.path.Parse` — the one plang-path tokenizer. The navigator walks the resulting `path.Segments`; a future jsonpath/css navigator reading a separate language does not replace it.
- Full-match `%ref%` → `variable`, never parsed (parent branch). The reader pivot must preserve this.
- Native `dict`/`list` construction where a value is plang-authored (`set %x% = {a:1}`) — those stay native, not clr.
- The double-wrap guard (keeper).

---

## Scope — v1 vs the direction

**v1 (unblocks `plang build`):** the `kind` base + registry (`context.App.Type.Kind`) with **two kinds — `json` and the `*` reflection catch-all** (relocating clr's reflection, which also proves the mechanism end-to-end); `clr` with a derived kind and pure delegation to its kind; `Type.KindOf` in the CLR bridge; the parser handoff for a clr; the `(item/object, json)` reader pivot + `data/reader:79-80` fix (default text); `context.Ok(raw, kind)`; the container-materializes-to-scalar guard; the `Peek`→`item` and identifiers→`text` companion changes. Deliverable: `%plan%` is a navigable `clr` (kind json) → `foreach %plan.steps%` works → `IndexNotSet` falls.

**Direction beyond v1 (do not silently pull into v1):**
- Generalize **all** navigation through the registry — native `dict`/`list` and item types get navigators, resolving variables (option b) universally, and the generic `Index.ResolveKey` in `data/this.Navigation.cs` retires. This is the end-state Ingi described (`kind=magic`), and it's the bulk of "it's going to be a lot" — stage it deliberately.
- yaml / xml navigators; the Convert graph with composition (md→html→pdf); the write-side (mutate-in-place or copy-on-write).
- **A plang action to load a kind/type pack from a DLL** — `- add type mytype.dll`. Wraps the existing `code.load` + a registry sweep for the kinds (and readers) the assembly defines. The registration seam is built in v1 so the action has something to call; the action surface itself is a follow-on.

Because v1 relocates reflection into the `*` navigator, `clr.Navigate` is pure delegation from the start — no transitional two-homes debt. The remaining staged item is generalizing native/item navigation through the registry (v2).

---

## §Identifiers — type identifiers are `text`

A type's name, a kind's key, a clr's kind — every identifier in the type system is a plang `text`, not a C# `string`. The payoff is uniformity: the type system's own metadata is a first-class plang value like everything else, with one serialization path and the ability to be inspected/compared in plang. `text` implements value `Equals`/`GetHashCode`, so it keys the kind registry directly, and its implicit `string` operator keeps `kind == "json"` working. This makes `type.@this.Name`/`.Kind` `text` too — mechanical breadth (many call sites, shallow change), landed with this work so `clr.Kind` and the kind registry are not lone `string` islands. `System.Type` appears only in the CLR bridge (`Type.KindOf(clrType)`) — the one place that maps a live CLR object's type to a kind; no kind surface takes it.

## §Peek — `Peek()` returns `item.@this`

Every `Peek()` in the type system already returns `this`; tighten the base signature from `object? Peek()` to `item.@this Peek()`. A value is always a plang value, never C# null — absence is `@null.@this.Instance`. This removes null-checking at `Peek()` call sites and makes "navigation always yields a plang value" true by type. (`Data.Peek()` — a distinct surface on `Data` — is out of scope.)

---

## OBP validation pass

| New/changed surface | Verb+Noun check | Object-decomposition check |
|---|---|---|
| `clr(object, kind?)` | `kind` — one word (a `text`), ok | carries the `object` whole; kind derived from it, not a flattened copy of its fields |
| kind registry `Type.Kind[k]` | `kind` — one word — ok | registry = selection + lifecycle; behavior lives on the kind, not a type-switch in the registry |
| `Kind[k].Navigate(obj, path, parent, context)` | all single words — ok | passes the raw `object` + the whole `path` object + context; the kind walks `path.Segments` (no re-parse), returns `Data` |
| `Kind[k].Enumerate(obj, context)` / `Load(raw, context)` / `Build(source, context)` | single words — ok | whole `Data`/raw in, `Data` out; `Build` returns an error `Data` when it can't build |
| `Data.Convert(to)` on the value | "Convert" verb-as-noun — ok | called on the carrier; no `context` param (Data carries its own); routes to `Kind[to].Build(this)`, value not decomposed |

Behavior is owned by the `kind` (one entity per format owning navigate/load/build), reached at `context.App.Type.Kind[k]` — not a "mime registry"; `mime` is what a kind maps to, an impl detail.

---

## Settled decisions

1. **A `kind` owns behavior** — navigate/load/build for a value of that kind live on `context.App.Type.Kind[k]`. One registry; navigation, conversion, and kind-derivation all resolve through it.
2. **Convert dispatch** — the **outbound (target) owns it**, not the source. `Data.Convert(to)` → `context.App.Type.Kind[to].Build(this)`. `audio` owns "build audio from text"; `md` never enumerates its targets. No `context` param on `Convert` (Data carries its own).
3. **Kind derived, not required** — `clr` needn't be stamped; it asks `Type.KindOf(objectClrType)` (`JsonElement → json`, else `*`). Registration is "is a `kind`" (no namespace filter — a kind declares its own `Kind`). Runtime DLLs register via the `- add type <dll>` action.
4. **v1 kinds** — `json` **and** the `*` reflection catch-all (the relocated clr reflection). Shipping both proves the kind registry + `*` fallback end-to-end; `clr.Navigate` is pure delegation.
5. **Identifiers are `text`** — `clr.Kind`, the kind's `Kind`, and `type.@this.Name`/`.Kind` are plang `text`, not `string` (§Identifiers). `System.Type` is confined to the CLR bridge (`Type.KindOf`).
6. **`Peek()` returns `item.@this`** — never `object?`, never C# null; absence is `@null.@this.Instance` (§Peek).

Companion changes (§Identifiers, §Peek) are type-system-wide but mechanical; they land with this work so the new surfaces are consistent with the type system rather than lone `string`/`object?` islands.

---

## You own this (coder)

Every code shape, signature, file path, and method name above (and in `code-draft.md`, the authoritative spec) is a **suggestion** to make the design concrete — you own the final form. In particular: the `kind` base template vs each kind implementing `Navigate` directly, and where kind files live (`app/type/kind/json.cs`), are yours to shape. Verify the parent-branch `%ref%`/reader behavior before touching `data/reader:79-80`. If a shape here fights the code, push back — the design intent (a `clr` stays a `clr`; a `kind` owns navigate/load/build for its values; convert's **outbound owns it**; the reader pivot must never turn a `%ref%` into a `clr`) is what must survive, not these specific signatures.
