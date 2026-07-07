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

`clr` today derives `kind` from the CLR type name in `Mint()`. It gains a **stamped `kind`** set at construction. Default (unstamped) = the CLR type name → resolves to the `*` (reflection) navigator. A producer that knows the format stamps it: `json` / `yaml` / `xml`. The stamped kind is what `Mint()` surfaces as the type's kind, so the wire carries `{item, json}` and a round-trip reproduces the same clr.

### 2. A navigator registry, keyed by kind

Mirrors the reader registry (`app.type.reader`), discovered by namespace. Each navigator **declares the CLR type(s) and the kind it handles** at registration, so the registry resolves either way:

- value stamped `kind=json` → json navigator;
- bare `JsonElement` with no stamp → json navigator (declared CLR type match);
- anything else → the `*` reflection navigator (default).

```
kind → navigator
  "json"  → json         (handles JsonElement / JsonNode)
  "yaml"  → yaml   (later)
  "xml"   → xml    (later)
  "*"     → reflection   (walk public properties)  ← default
```

### 3. Navigator interface — owns the tail language AND variable resolution

**Decision (Ingi): option (b).** The navigator receives the raw remaining path and resolves variables *itself*, because only the navigator knows its own syntax — for `kind=magic`, `planStep.index` might be magic, not a variable; only the magic navigator can tell. So a navigator is a mini-interpreter for its kind:

```
Navigate(obj, tail, context) → Data       // interpret the tail in this kind's language; resolve embedded vars via context
Enumerate(obj, context) → items                        // for foreach; each element a Data (clr for containers, scalar leaf otherwise)
```

- The navigator resolves a plang variable it finds in the tail (the `%planStep.index%`-in-a-bracket case) through `context.Variable` — the navigator decides *when* to call it. No separate resolver param; the context it's born with is the hook.
- **Container vs scalar:** a container node stays `clr(kind)`; a scalar leaf resolves to its plang scalar (`number`/`text`/`bool`/`null` by `ValueKind`, `number` precision from the token). Navigate/Enumerate never return a `clr` wrapping a scalar.

### 4. The parser handoff

`data.Navigate(path)` (`data/this.Navigation.cs`) walks generically until a hop lands on a **kinded-navigable `clr`**, then hands the **untokenized tail** to that clr's navigator, which interprets the rest in one call:

```
%plan.steps[step.Index].actions%
  plan            → generic hop → clr(kind=json)
  steps[step.Index].actions   → tail → json navigator
                                 (resolves step.Index via context.Variable, then walks)
```

- Native `dict`/`list` and item types (`goal`, …) keep the generic per-hop walk for now — the handoff only fires for a `clr` whose kind has a registered navigator. That's why the bug fix is bounded: `goal` is an item type and already navigates fine; only `%plan%` was broken.
- **Not a second plang tokenizer.** `app.variable.path.Parse` stays the one tokenizer for *plang* paths (parent-branch rule). The navigator's tail parser is a *different language* (jsonpath / css), which is legitimately the navigator's own concern.
- The path object must yield its untokenized tail at the handoff. It already keeps `Segment.Raw` and reconstructs via `ToString()`; the tail's raw form is derivable from there (or retain the original string + a cursor).

### 5. The pivot that makes it fall out: what the `(item, json)` reader returns

Today `item/serializer/json.cs` (`Parse`) narrows a `JsonElement` object → native `dict`, array → native `list`. **Change it to produce `clr(JsonElement, kind=json)` for external structured data.** Then:

- `file.read .json`, http json, `llm.query` all uniformly land as `clr(kind=json)`.
- The wire round-trip that caused the bug fixes itself: the plan crosses as `{type:{item,json}, value:<raw json>}` and read-back runs the `(item,json)` reader → `clr(kind=json)` again.

Paired with fixing `data/reader/this.cs:79-80` to route by the **declared type/kind**, not token shape. Constraint from the parent branch: a full-match `%ref%` still borns a `variable` and is **never parsed** — only genuine *content* of a structured declared type becomes `clr(kind=json)`.

### 6. OpenAi stamps the kind

Both the fresh (`context.Ok(TryParseJson…)`) and cached (`ParseResultValue`) paths stamp from `effectiveFormat`, so fresh == cached:

- `json` → `clr(kind=json)`; `xml` → `clr(kind=xml)`; `yaml` → `clr(kind=yaml)`.
- `md` / prose → `text` (with `kind=md` where useful) — scalar text, not navigable, no navigator.

### 7. Convert — the **outbound** (target) owns it, behind the existing door

Convert is a **content transform**, module-dev-facing: an action that wants audio gets `read file.md` (text, `kind=md`) and just asks "give me audio." **The target owns the conversion, not the source** (Ingi): if `text(md)` owned its outbound conversions it would have to know every possible format (audio, html, pdf, …); but if `audio` owns it, `audio` only needs to know how to make itself **from** text. So the owner is the **outbound (type, kind)** — exactly how the per-type `Convert(value, kind, context)` hook already works (`catalog.Convert` → `OwnerOf(targetType).Convert(value, …)` — the *target* builds itself from the value).

- **Keyed by the target `(type, kind)`, discovered like readers.** `audio` owns "build audio from a source"; `(text, html)` owns "build html-text from a source." The converter may branch on the source's type/kind internally (audio: text→TTS), but it lives with the **outbound**, so adding a new output format = adding one owner, and no existing type has to learn about it.
- **The door already has the kind axis.** The per-type `Convert(value, kind, context)` hook's `kind` param exists today (currently "a hint"). Make it load-bearing: the door resolves the **target** `(type, kind)` owner and hands it the source value whole.
- **Value-facing call:** `await Text.Convert("audio")` — no `context` param; everything at an action boundary arrives as `Data`, which already carries its context. So `Data.Convert(toKind)` routes through `Data`'s own context. (See the Convert section in `code-draft.md`.)
- **Chains later.** md→html→pdf via composition is a graph problem; v1 = direct hops only. `log()` when the target owner can't build from this source (fail loud, don't silently pass the source through).

### 8. Write-side — read/navigate only for v1

`JsonElement` is immutable; `set %json.x% = 5` (mutate the `object`) is out of scope for v1. When someone needs a mutable structure, they `as dict` (Convert) and mutate the native dict. Flag `set` into a `clr` as an explicit error for now.

### 9. Guards

- **Double-wrap guard** — added, keeper (`data/this.cs` ctor throws on a bare `Data` as value).
- **Container-materializes-to-scalar guard** — proposed, in `source.Value`: if the declared type is a container (`object`/`dict`/`list`) but it materialized to a non-container leaf, throw. This class of round-trip loss (the exact bug) then fires loudly at the point, not three hops later as `IndexNotSet`.

---

## Leaf trace — the incumbents this plan gives a new owner

| Incumbent (today) | Does | Disposition under this plan |
|---|---|---|
| `clr.Navigate` (`type/clr/this.cs`) | reflects C# properties on the `object` by key | v1: gains a registry lookup first (`Navigators.For(...)?.Navigate ?? existing reflection`) — reflection **stays** as the fallback. v2: reflection relocates into the `*` navigator and `clr.Navigate` becomes pure delegation |
| `data/this.Navigation.cs` — `Index` segment (`ResolveKey` + the `IndexNotSet` diagnostic) | resolves `%[var]%` against the variable store mid-walk (option a) | stays for native/item hops (generic walk); for a kinded-clr tail, resolution moves *into* the navigator (option b) via `context.Variable` |
| `object/serializer/json.cs` `Read` (the (item/object, json) reader) | walks `JsonElement` → native `dict`/`list` via `Parse` | wraps in `clr(kind=json)` instead (§5). The `Parse` DOM walker **stays** (authored `dict`/`list` literals use their own readers, untouched) — only the *reader* path pivots |
| `data/reader/this.cs:79-80` | picks read format by token shape | route by declared type/kind; preserve full-match `%ref%` → `variable` (parent-branch rule) |
| `catalog/Conversion.cs` (JsonElement→dict/list on `As<T>`) | value-model conversion | stays; becomes the `json→dict` arm of Convert (§7), now reachable via the value-facing `Convert` |
| per-type `Convert(value, kind, ctx)` hooks | build a value from another, `kind` unused | `kind` becomes load-bearing — the door resolves the **target** `(type, kind)` owner (outbound-owns-inbound) and hands it the source value |
| `OpenAi` result construction (fresh + cached) | `context.Ok(TryParseJson)` → unstamped | stamp kind from `effectiveFormat` (§6) |

---

## Demolition worklist

**Dies now (v1):**
- The token-shape branch at `data/reader/this.cs:79-80` — replaced by declared-type/kind routing.
- The `JsonElement → dict` walk in `object/serializer/json.cs` `Read` **for external structured data** — replaced by `clr(kind=json)`. (The `Parse` DOM walker stays; only the *reader* path wraps.)
- OpenAi's unstamped `context.Ok(TryParseJson)` on both fresh and cached paths.

**Dies later (v2, staged — see Scope):**
- The reflection body inside `clr.Navigate` — relocates into the `*` navigator, and `clr.Navigate` becomes pure delegation. In v1 it **stays** as the fallback (Ingi: reflection already works, just add json).
- The generic `Index.ResolveKey` walk in `data/this.Navigation.cs`, once native/item navigation is generalized through the registry (the reflection navigator resolving via `context.Variable`). Until then it stays — do not delete it in v1.

**Stays (explicitly not touched):**
- `app.variable.path.Parse` — the one plang-path tokenizer. The navigator's tail parser is a separate language, not a replacement.
- Full-match `%ref%` → `variable`, never parsed (parent branch). The reader pivot must preserve this.
- Native `dict`/`list` construction where a value is plang-authored (`set %x% = {a:1}`) — those stay native, not clr.
- The double-wrap guard (keeper).

---

## Scope — v1 vs the direction

**v1 (unblocks `plang build`) — Ingi: "we already have reflection navigation on the generic clr object, so just add the json navigation to get it working."** So v1 is narrow: clr gains `kind`; the navigator registry + the **json navigator only**; the parser handoff for a kinded clr; the `(item/object, json)` reader pivot + `data/reader:79-80` fix; OpenAi stamping; the container-materializes-to-scalar guard. **The existing clr reflection stays put** — it's the fallback when no registered navigator matches (`clr.Navigate` = `Navigators.For(kind, type)?.Navigate(...) ?? existing reflection`). Deliverable: `%plan%` is a navigable `clr(kind=json)` → `foreach %plan.steps%` works → `IndexNotSet` falls.

**Direction beyond v1 (do not silently pull into v1):**
- Relocate the existing clr reflection into a registered `*` navigator (so `clr.Navigate` becomes pure delegation, no fallback branch), and generalize **all** navigation through the registry — native `dict`/`list` and item types get navigators, resolving variables (option b) universally, and the generic `Index.ResolveKey` in `data/this.Navigation.cs` retires. This is the end-state Ingi described (`kind=magic`), and it's the bulk of "it's going to be a lot" — stage it deliberately.
- yaml / xml navigators; the Convert graph with composition (md→html→pdf); the write-side (mutate-in-place or copy-on-write).

The transitional cost, named: in v1 the reflection default lives on `clr` (fallback branch) while json lives in the registry — two homes for navigation. Accepted debt, retired when reflection relocates into the `*` navigator.

---

## OBP validation pass

| New/changed surface | Verb+Noun check | Object-decomposition check |
|---|---|---|
| `clr(object, kind)` | `kind` — one word, ok | carries the `object` whole; does not flatten its fields onto itself |
| navigator registry keyed by `kind` | "navigator" agent-noun, matches `reader`/`renderer` — ok (naming pass may revisit vs `convert.@this` style) | registry = selection + lifecycle; behavior on the navigator, not a type-switch in the registry |
| `Navigate(obj, tail, context)` | all single words — ok | passes the `object` and the context whole; navigator does not receive pre-decomposed segments for its own language |
| `Enumerate(obj, context)` | one word — ok | yields whole `Data` items, not raw fields |
| `Data.Convert(toKind)` on the value | "Convert" verb-as-noun, matches existing `convert.@this` — ok | called on the carrier; no `context` param (Data carries its own); value not decomposed |
| target-`(type, kind)` converter files | the target descriptor is the key, not a compound — ok | one converter per outbound owner, file-per-variant (matches variant-design rule) |

Naming settled: a "navigator registry" keyed by `kind` (not "mime registry") — `kind` is the plang word already on every type; `mime` is what a kind *maps to*, an impl detail.

---

## Settled with Ingi (2026-07-07 review comments)

1. **Convert dispatch** — the **outbound (target) owns it**, not the source. `text(md)→audio`: `audio` owns "make audio from text"; `md` doesn't have to know audio. Keyed by the target `(type, kind)`, which is how the existing per-type `Convert` hook already dispatches (`OwnerOf(target)`). §7 rewritten.
2. **Value-facing Convert API** — `Data.Convert(toKind)`, **no `context` param** — everything at an action boundary is `Data` and carries its own context (`Data<text> Text` → `await Text.Convert("audio")`). Demonstrated in the Convert section of `code-draft.md`.
3. **Naming / registration** — `navigator` keyed by `kind`, discovered by namespace exactly like the reader registry. Discovery demonstrated in `code-draft.md` "How registration works today".
4. **v1 boundary** — json navigator **only**. Reflection navigation already exists on `clr`; keep it as the fallback and just add json (§ Scope). Relocating reflection into a `*` navigator is v2.

No open questions remain — ready for coder.

---

## You own this (coder)

Every code shape, signature, file path, and method name above (and in `code-draft.md`) is a **suggestion** to make the design concrete — you own the final form. In particular: the exact navigator interface, where the navigator files live (`app/type/clr/<kind>.cs` vs a dedicated `navigator/` folder), and how the path object exposes its untokenized tail are yours to shape. Verify the parent-branch `%ref%`/reader behavior before touching `data/reader:79-80`. If a shape here fights the code, push back — the design intent (clr stays clr, per-kind navigator owns its language + var resolution, convert's **outbound owns it** behind the existing door) is what must survive, not these specific signatures.
