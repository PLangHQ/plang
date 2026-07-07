# Coder review of architect's `plan.md` + `code-draft.md`

**Branch:** `clr-navigators`
**Author:** coder — grounded against HEAD source, not from memory.
**Verdict:** direction is right (clr stays clr, a kind owns navigate/load/build, outbound owns convert). But three "mechanical/shallow" claims are wrong against the code, one drafted fix **does not fix the bug**, and there's a namespace collision the spec didn't see. Details below, each with the file it fails on.

---

## 🔴 BLOCKER 1 — the drafted reader fix does NOT fix `%plan%`

The spec (code-draft §"reader pivot") routes the deferred read by `typeRef?.Kind`:

```csharp
deferredFormat = typeRef?.Kind is { } k ? Mime(k) : Text.Mime;   // no kind → text
```

But the actual `%plan%` slot in `os/system/builder/BuildGoal/.build/plan.pr:652` is:

```
variable.set(Name=%plan%, Value=%!data%, Type=object)
```

**`Type=object`, no kind.** So `typeRef.Kind` is `null` → routes to **text** → the value borns `source(object, text/plain)` → *exactly the current bug*. The drafted fix ships green and the blocker survives.

The signal is **container-ness, not kind**. The route must be:

```csharp
deferredFormat =
      typeRef?.Kind is { } k                    ? Mime(k)                 // declared kind wins
    : typeRef is { Name: "object" or "dict" or "list" } ? "application/json"   // container → json
    : Text.Mime;                                                          // scalar → text
```

`object`/`dict`/`list` are precisely the container set the §9 guard already enumerates — reuse that predicate (`typeRef.IsContainer`), don't re-list it. **Verify this against `plan.pr` before writing** — it's the one line the whole branch exists to fix.

---

## 🔴 BLOCKER 2 — `app.type.kind.@this` is already taken

The spec puts the abstract navigate/load/build base at `app/type/kind/this.cs` and the registry at `App.Type.Kind[k]`. That namespace is **already occupied**:

- `app/type/kind/this.cs` → `kind.@this` **is the kind VALUE** — the subtype token ("json","md","jpg") that wraps `name+context` and resolves `.Type` via the reader/format registry (`app/type/reader/this.cs:115` uses it).
- `app/type/kind/Hooks.cs` → `kind.Hooks` — the build-time `Build(object?)` hook cache, surfaced as `App.Type.KindHooks` (`catalog/this.cs:49`).

Two different `@this` cannot live in `app.type.kind`. Options:
1. The behavior owner is a **different noun** (the format/kind-behavior lives elsewhere; keep `kind.@this` as the value token).
2. `Type.KindOf(clrType)` should **reuse** `KindHooks.Of(clrType, value)` + `clr.Mint()`'s `ResolveName` — those already map CLR→kind. Don't add a third CLR→kind path.

Pick the home before writing a line — this decides the whole file layout.

---

## 🟠 DESIGN FORK — is "kind" one owner, or two? (navigate vs convert)

The spec fuses **navigate/enumerate/load** and **build/convert** onto one `kind` entity. But they have different owners:

| capability | example owners | already lives on |
|---|---|---|
| navigate / enumerate / load | `json`, `xml`, `*` (reflection) | *format/kind* — how a raw host is structured |
| build / convert | `audio`, `dict`, `pdf` | *target TYPE* — `type.@this.Convert(value,ctx)` (`type/this.cs:187`) + `Conversions` catalog (`catalog/this.cs:56`) |

`json`/`*` are **formats**; `audio`/`dict` are **types** (they have items). Fusing them means `Kind["dict"]` and `App.Type["dict"]` are the same thing addressed twice, and `Kind["audio"].Build` duplicates the `type.Convert` door that already exists. This is Ingi's own comment-85 musing (*"kind is a plang type, the one that owns the conversion… `context.type[to].Convert(kind)`"*).

**Coder lean:** split by owner.
- Navigation → the **format/kind** (json, `*`) — the genuinely new surface.
- Convert → the **target TYPE**: `Data.Convert(to) => App.Type[to].Build(source)`, reusing `type.Convert`/`Conversions`. Don't stand up `Kind["audio"]` beside `Type["audio"]`.

This also dissolves BLOCKER 2: only navigation needs a new home; convert stays on `type`.

*(If Ingi wants the single unified "kind owns everything" concept — his comment 85 leans that way — then `kind` and `type` are collapsing, which is a much bigger move than this branch. Flagging so it's a decision, not a default.)*

---

## 🟠 "Companion changes are mechanical" — two of them are not

### `Peek() → item.@this` is a contract change, not a signature tighten

Spec: *"Every `Peek()` already returns `this`; tighten the base to `item.@this`."* **False.** `source.Peek()` (`app/type/item/source.cs:90`) returns raw CLR:

```csharp
public override object? Peek()
{
    if (_value is byte[] b && _type == "text") { ... return GetString(b) or b; }
    return _value;      // raw byte[] / raw string — NOT an item.@this
}
```

A source's sync face **is its un-parsed raw** (Peek = "in memory now", parse stays behind `Ready`). Tightening to `item.@this` forces wrapping raw bytes/string in an item at Peek time — that changes the lazy-materialization contract, not just the return type. **Pull out of v1**; if wanted, it's its own design pass on `source`.

### Identifiers → `text` is deep, not shallow

`type.@this.Name`/`.Kind` are `string` threaded through: the JSON wire converter (`writer.String(Name)`, `type/this.cs:43-44`), the primitive alias/canonical tables, `ClrType` resolution, `Canonicalise`, `Compare`/`Rank`, and `text.@this`'s own `string Kind`. Making Name/Kind `text` ripples across the wire serializer and the primitive machinery — real refactor, not a rename sweep.

Ingi asked (comment 63) *"what are the benefits?"* — honest answer: uniformity + plang-inspectable metadata, real but not free. **Recommend landing it as its own adjacent branch AFTER the unblock is green** — the plan's own rule is "don't silently pull scope into v1," and a type-system-wide churn is exactly that. `text` keying the `Kind` dictionary works fine (it has `GetHashCode`), so the registry doesn't *need* this to ship.

---

## 🟡 Reuse what exists — don't build parallels

- **`Segment.Index.ResolveKey(store)`** (`variable/path/Segment.cs:61`) already resolves a bracket var against the store. The draft's base `Key(i, ctx)` reimplements it — and wrongly: it uses `i.Inner.ToString()` where `ResolveKey` uses `((Member)Inner.Segments[0]).Name` (unquoted). Call `ResolveKey`, delete `Key`.
- **The json parse already has an owner.** `object/serializer/json.cs:Read(object raw, string? kind, ReadContext)` decodes+parses json today. The draft's `Kind["json"].Load(rawJsonText)` (a `JsonDocument.Parse`) is a *second* json parser. One owner: `Read` produces the `clr` (or delegates to the kind), not two parse paths keyed by json.
- **`Type.KindOf`** overlaps `KindHooks.Of` + `clr.Mint()`'s `ResolveName` (see BLOCKER 2).

---

## 🟢 Confirmations (these are right)

- **Reader-pivot seam is `Read`, not `Parse`.** Confirmed: `object/serializer/json.cs:Read` receives the raw string/bytes and returns `object?` — swapping `Parse(parsed)` for `clr(JsonElement, kind=json)` is a low-risk, localized cut. `Parse` stays (the universal DOM narrower).
- **`%ref%` → variable won't regress.** The full-match `%ref%`→`variable` born happens in `type.Build` (`type/this.cs:265`, the string→variable branch) — a *different* branch from the deferred-value read. Defaulting the deferred branch to text (or json-by-container) can't turn a `%ref%` into a clr. Safe.
- **Double-wrap guard** — already in `clr` ctor (`clr/this.cs:26`) and `type.Create` (`type/this.cs:445`). Keeper.
- **Container→clr, scalar→plang scalar** rule, and the `Data(...)` child-factory name (over `Wrap`) — agreed.
- **Producer hands raw+kind, no per-format ladder** — agreed; `OpenAi` collapses to one line.

---

## Recommended v1 cut (tightened)

1. **Reader fix routed by container-ness** (BLOCKER 1) — this alone likely clears `IndexNotSet`.
2. **json navigation surface** with a home that respects the existing `kind` namespace (BLOCKER 2) + the `*` reflection relocation.
3. `clr` derives its kind (reuse `KindHooks`/`ResolveName`), pure delegation to navigation.
4. Container-materializes-to-scalar guard.
5. `context.Ok(raw, kind)` producer door.

**Defer out of v1:** identifiers→text (own branch), `Peek`→`item` (own `source` pass), Convert/`Build` (ships with the first real converter, on the *target type* not a fused kind).

---

## Questions for Ingi

1. **The fork above:** one fused `kind` owning navigate+convert, or split (navigation on the format, convert on the target type)? Your comment 85 leans "kind == owning type" — if so, are `kind` and `type` meant to collapse (big), or stay separate registries (this branch)?
2. **Sequencing:** OK to land identifiers→text and `Peek`→item as **separate branches after** the unblock is green, rather than inside it?

---

## Round 2 — sign-off on the revised plan (`Type[t].Kind[k]`)

Read the folded plan. It resolves all four points, and improves on one:

- **Blocker 1 — better than my fix.** You root-caused past my reader-side container heuristic to the real cause: `variable.set(Type=object)` *masking* the intrinsic `item/json`. Apex-declaration must not demote a richer type. Agreed — mine was a symptom patch, drop it.
- **Blocker 2 + the fork — resolved cleanly.** `Type[t].Kind[k]` (navigation under `item`, convert under target type, extending the existing kind token) dissolves the collision and the navigate-vs-convert split in one move. No `Kind["dict"]` beside `Type["dict"]`. 👍
- **Companions + reuse** — all folded. No notes.

### One caveat to pin during implementation (§5 seam)

**Verify §5-alone clears `IndexNotSet` before building the kind machinery.** If apex-doesn't-mask keeps the value intrinsically `dict` (the fresh path already narrows to a dict at OpenAi), then `%plan.steps%` navigates via the *existing* dict per-hop walk — no clr/json kind needed for this bug at all. That means:

- Land §5 first, rebuild `plang build` (cache off), confirm the blocker falls.
- Only then build `Type["item"].Kind["json"]` + the reader pivot (§2, §4, §6) — which is the *durable* representation (so `file.read .json` / http / the cached path all land as `clr(json)`), not the thing unblocking the build.

Sequencing this way keeps the unblock a ~1-file change and the kind work a separate, testable layer — and tells us immediately if the two are actually coupled or not. Fail-loud check: after §5, assert `%plan%`'s Data type is `item`/`dict`, not `object`, at the write boundary.

**Verdict: build it.** No blockers remain.
