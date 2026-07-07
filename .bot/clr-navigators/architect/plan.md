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
- **The kind owns its own path language.** A json navigator can interpret jsonpath; an html navigator (over `text`) could interpret css/xpath. The value decides what a path segment *means*.

---

## The model

### 1. `clr` carries `(object, kind)`

`clr` today derives `kind` from the CLR type name in `Mint()`. It gains a **stamped `kind`** (a `text` — see §Identifiers) set at construction. Unstamped → derived from the CLR type → resolves to the `*` (reflection) navigator. A producer that knows the format stamps it: `json` / `yaml` / `xml`. The stamped kind is what `Mint()` surfaces, so the wire carries `{item, json}` and a round-trip reproduces the same clr. `clr.Navigate` is **pure delegation** — resolve the navigator, call it; no reflection or `is JsonElement` switch remains on `clr` (the reflection body moves into the `*` navigator, §3).

### 2. A navigator registry, keyed by kind

Discovered by "implements `INavigator`" — a navigator declares its own `Kind`, so no namespace filter is needed (unlike the reader registry, which derives its type name from the folder). Each navigator declares its `Kind` and the CLR type(s) it claims, so the registry resolves either way:

- value stamped `kind=json` → json navigator;
- bare `JsonElement` with no stamp → json navigator (CLR-type match via `Handles`);
- anything else → the `*` reflection navigator (always resolves).

```
kind → navigator
  "json"  → json         (Handles JsonElement / JsonNode)
  "*"     → reflection   (walk any object's public properties)  ← catch-all
  "yaml"/"xml" → later    (one file each: implement INavigator, declare Kind)
```

### 3. Navigator interface — owns the path language AND variable resolution

**Decision (Ingi): option (b).** The navigator receives the remaining path and resolves variables *itself*, because only the navigator knows its own syntax — for `kind=magic`, `planStep.index` might be magic, not a variable; only the magic navigator can tell. So a navigator is a mini-interpreter for its kind. It lives at **`app/type/INavigator.cs`** (not under `clr/`) — navigation generalizes to other types over time (Ingi).

```
Navigate(obj, path, parent, context) → Data   // walk obj by the (already-tokenized) path object; resolve vars via context
Enumerate(obj, context) → items               // for foreach; each element a Data (clr for containers, scalar leaf otherwise)
```

- **Takes the raw `obj`, not an `item`.** The navigator turns a raw host (`JsonElement`, POCO) into plang values — input is raw, output is `Data`. Wrapping the input as `item` would just force an immediate unwrap.
- **Takes the `path` object, not a string.** `app.variable.path.Parse` already tokenized once; the navigator walks `path.Segments` directly — no re-parse, no `ToString`.
- The navigator resolves a plang variable it finds in the path (the `%step.Index%`-in-a-bracket case) through `context.Variable` — it decides *when* to call it. No separate resolver param; the context it's born with is the hook.
- **Container vs scalar:** a container node stays `clr(kind)`; a scalar leaf resolves to its plang scalar (`number`/`text`/`bool`/`null` by `ValueKind`, `number` precision from the token). Navigate/Enumerate never return a `clr` wrapping a scalar.
- **Identifiers are `text`; the CLR-interop surface stays C#.** `Kind` (and every type identifier) is a plang `text` — `text` keys a dictionary (value `Equals`/`GetHashCode`) and compares to string literals via its implicit operator, so it carries no cost as a key. `Handles(System.Type) → bool` stays C#: it matches C# reflection types, which is a CLR concern, not a plang value. This makes `type.@this.Name`/`.Kind` `text` too — a mechanical companion change (see §Identifiers).

### 4. The parser handoff

`data.Navigate(path)` (`data/this.Navigation.cs`) walks generically until a hop lands on a **kinded-navigable `clr`** (`_item is clr c && c.Navigable`), then hands the **`path` object** to that clr's navigator, which walks the rest in one call:

```
%plan.steps[step.Index].actions%
  plan            → generic hop → clr(kind=json)   (Navigable)
  steps[step.Index].actions   → path object → json navigator
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

### 6. OpenAi stamps the kind

Both the fresh (`context.Ok(TryParseJson…)`) and cached (`ParseResultValue`) paths stamp `kind` from the requested `format` (rename `effectiveFormat` → `format`), so fresh == cached. No switch — one line: `format == "json" && TryParseJson(...) is JsonElement je ? new clr(je, context, kind: format) : extracted`.

- v1: `json` → `clr(kind=json)`; everything else → `text` (the string). `xml`/`yaml` become `clr(kind=…)` when their navigators land.

### 7. Convert — the **outbound** (target) owns it, behind the existing door

Convert is a **content transform**, module-dev-facing: an action that wants audio gets `read file.md` (text, `kind=md`) and just asks "give me audio." **The target owns the conversion, not the source** (Ingi): if `text(md)` owned its outbound conversions it would have to know every possible format (audio, html, pdf, …); but if `audio` owns it, `audio` only needs to know how to make itself **from** text. So the owner is the **outbound (type, kind)** — exactly how the per-type `Convert(value, kind, context)` hook already works (`catalog.Convert` → `OwnerOf(targetType).Convert(value, …)` — the *target* builds itself from the value).

- **An `IConverter` keyed by the target `(type, kind)`.** `audio` owns "build audio from a source"; `(text, html)` owns "build html-text from a source." It lives with the **outbound** type, so adding an output format = adding one owner, and no existing type learns about it. `IConverter` is the third agent-noun interface beside `ITypeReader`/`INavigator`.
- **`Build` returns the value or an error `Data` — no boolean probe.** The door (`Conversions.To`) resolves the target owner and calls `Build(source, ctx)`; the owner returns an error `Data` when it can't build from that source. No registered owner for the target → the door returns an error `Data` naming the missing conversion.
- **Value-facing call:** `await Text.Convert("audio")` — no `context` param; everything at an action boundary arrives as `Data`, which already carries its context. So `Data.Convert(toKind)` routes through `Data`'s own context.
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
| `clr.Navigate` (`type/clr/this.cs`) | reflects C# properties on the `object` by key | becomes **pure delegation** (resolve navigator, call it). The reflection body relocates into the `*` navigator — no reflection or `is JsonElement` switch left on `clr` |
| `item.@this.Peek()` | `virtual object? Peek() => this` | tighten to `item.@this Peek()` — always a plang value, never C# null (absence is `@null.@this.Instance`). Every existing `Peek()` already returns `this`, so it's a signature tighten (§Peek) |
| `data/this.Navigation.cs` — `Index` segment (`ResolveKey` + the `IndexNotSet` diagnostic) | resolves `%[var]%` against the variable store mid-walk (option a) | stays for native/item hops (generic walk); for a clr path, resolution moves *into* the navigator (option b) via `context.Variable` |
| `object/serializer/json.cs` `Read` (the (item/object, json) reader) | walks `JsonElement` → native `dict`/`list` via `Parse` | wraps in `clr(kind=json)` instead (§5). The `Parse` DOM walker **stays** (authored `dict`/`list` literals use their own readers, untouched) — only the *reader* path pivots |
| `data/reader/this.cs:79-80` | picks read format by token shape | route by declared type/kind; preserve full-match `%ref%` → `variable` (parent-branch rule) |
| `catalog/Conversion.cs` (JsonElement→dict/list on `As<T>`) | value-model conversion | stays; becomes the `json→dict` arm of Convert (§7), now reachable via the value-facing `Convert` |
| per-type `Convert(value, kind, ctx)` hooks | build a value from another, `kind` unused | the door resolves the **target** `(type, kind)` owner (`IConverter`, outbound-owns-inbound) and hands it the source value; the owner returns the built value or an error `Data` |
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

**v1 (unblocks `plang build`):** clr gains `kind` (text); the navigator registry + **two navigators — `json` and the `*` reflection catch-all** (relocating clr's reflection, which also proves the `*` mechanism end-to-end); `clr.Navigate` becomes pure delegation; the parser handoff for a clr; the `(item/object, json)` reader pivot + `data/reader:79-80` fix (default text); OpenAi stamping; the container-materializes-to-scalar guard; the `Peek`→`item` and identifiers→`text` companion changes. Deliverable: `%plan%` is a navigable `clr(kind=json)` → `foreach %plan.steps%` works → `IndexNotSet` falls.

**Direction beyond v1 (do not silently pull into v1):**
- Generalize **all** navigation through the registry — native `dict`/`list` and item types get navigators, resolving variables (option b) universally, and the generic `Index.ResolveKey` in `data/this.Navigation.cs` retires. This is the end-state Ingi described (`kind=magic`), and it's the bulk of "it's going to be a lot" — stage it deliberately.
- yaml / xml navigators; the Convert graph with composition (md→html→pdf); the write-side (mutate-in-place or copy-on-write).
- **A plang action to load a type/navigator pack from a DLL** — `- add type mytype.dll`. Wraps the existing `code.load` + a registry sweep for `ITypeReader`/`INavigator`/`IConverter`. The `Register(...)` seam is built in v1 so the action has something to call; the action surface itself is a follow-on.

Because v1 relocates reflection into the `*` navigator, `clr.Navigate` is pure delegation from the start — no transitional two-homes debt. The remaining staged item is generalizing native/item navigation through the registry (v2).

---

## §Identifiers — type identifiers are `text`

A type's name and kind, a navigator's kind, a converter's target — every identifier in the type system is a plang `text`, not a C# `string`. The payoff is uniformity: the type system's own metadata is a first-class plang value like everything else, with one serialization path and the ability to be inspected/compared in plang. `text` implements value `Equals`/`GetHashCode`, so it keys the registries directly, and its implicit `string` operator keeps `kind == "json"` and interop with `System.Type` names working. This makes `type.@this.Name`/`.Kind` `text` too — mechanical breadth (many call sites, shallow change), landed with this work so `clr.Kind`/`INavigator.Kind`/`IConverter` are not lone `string` islands. The CLR-interop surface (`Handles(System.Type)`) stays C# — it matches C# reflection types, not plang values.

## §Peek — `Peek()` returns `item.@this`

Every `Peek()` in the type system already returns `this`; tighten the base signature from `object? Peek()` to `item.@this Peek()`. A value is always a plang value, never C# null — absence is `@null.@this.Instance`. This removes null-checking at `Peek()` call sites and makes "navigation always yields a plang value" true by type. (`Data.Peek()` — a distinct surface on `Data` — is out of scope.)

---

## OBP validation pass

| New/changed surface | Verb+Noun check | Object-decomposition check |
|---|---|---|
| `clr(object, kind)` | `kind` — one word (a `text`), ok | carries the `object` whole; does not flatten its fields onto itself |
| navigator registry keyed by `kind` | "navigator" agent-noun, matches `reader`/`renderer` — ok | registry = selection + lifecycle; behavior on the navigator, not a type-switch in the registry |
| `Navigate(obj, path, parent, context)` | all single words — ok | passes the raw `object` + the whole `path` object + context; navigator walks `path.Segments` (no re-parse), returns `Data` |
| `Enumerate(obj, context)` | one word — ok | yields whole `Data` items, not raw fields |
| `Data.Convert(toKind)` on the value | "Convert" verb-as-noun, matches `IConverter` — ok | called on the carrier; no `context` param (Data carries its own); value not decomposed |
| `IConverter` (outbound-owned) | agent-noun, matches `ITypeReader`/`INavigator` — ok | one converter per outbound `(type, kind)`; `Build` takes/returns whole `Data`, returns an error `Data` when it can't build |

Interfaces are the agent-noun trio: `ITypeReader`, `INavigator`, `IConverter`. The navigator registry is keyed by `kind` (not "mime registry"); `mime` is what a kind maps to, an impl detail.

---

## Settled decisions

1. **Convert dispatch** — the **outbound (target) owns it**, not the source. `text(md)→audio`: `audio` owns "build audio from text"; `md` never enumerates its targets. Keyed by the target `(type, kind)` (`IConverter`), which is how the existing per-type `Convert` hook already dispatches (`OwnerOf(target)`).
2. **Value-facing Convert API** — `Data.Convert(toKind)`, **no `context` param** — everything at an action boundary is `Data` and carries its own context (`Data<text> Text` → `await Text.Convert("audio")`).
3. **Registration** — discovered by "implements `INavigator`" (no namespace filter — a navigator declares its own `Kind`). Runtime DLLs register via `Register`, surfaced as the `- add type <dll>` action.
4. **v1 navigators** — `json` **and** the `*` reflection catch-all (the relocated clr reflection). Shipping both proves the registry + `*` fallback end-to-end; `clr.Navigate` is pure delegation.
5. **Identifiers are `text`** — `clr.Kind`, `INavigator.Kind`, `IConverter.Type`/`Kind`, and `type.@this.Name`/`.Kind` are plang `text`, not `string` (§Identifiers). `Handles(System.Type)` stays C# (CLR-interop).
6. **`Peek()` returns `item.@this`** — never `object?`, never C# null; absence is `@null.@this.Instance` (§Peek).

Companion changes (§Identifiers, §Peek) are type-system-wide but mechanical; they land with this work so the new surfaces are consistent with the type system rather than lone `string`/`object?` islands.

---

## You own this (coder)

Every code shape, signature, file path, and method name above (and in `code-draft.md`) is a **suggestion** to make the design concrete — you own the final form. In particular: the exact navigator interface, and where the navigator impls live (`app/type/navigator/json.cs` vs with each type), are yours to shape (the interface at `app/type/INavigator.cs` is settled). Verify the parent-branch `%ref%`/reader behavior before touching `data/reader:79-80`. If a shape here fights the code, push back — the design intent (clr stays clr, per-kind navigator owns its language + var resolution, convert's **outbound owns it** behind the existing door) is what must survive, not these specific signatures.
