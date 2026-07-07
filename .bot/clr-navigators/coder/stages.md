# Implementation stages — clr + kinds under type

**Branch:** `clr-navigators`. Build target: architect's `code-draft.md` (authoritative) + `plan.md`.
Baseline: `baseline.md` (142 red before Stage 1). Each stage ends green-or-baseline + a commit.

Ordering principle (my round-2 caveat, Ingi agreed): **land the apex fix first and prove it clears
`IndexNotSet` alone**, before building the kind machinery — that tells us whether the unblock and the
durable clr/json representation are actually coupled. Stages 2–4 are the durable representation
(uniform for `file.read`/http/llm-cached), not the unblock.

---

## Stage 1 — Apex must not mask a richer type (the unblock) + the round-trip guard

**Intent.** Declaring a value `object`/`item` (the apex/universal) carries no information — "this is
an object" is always true. It must **not** overwrite a value's intrinsic `item/json` (or `dict`).
`variable.set(Name=%plan%, ..., Type=object)` on a dict must leave it a dict, so the wire carries the
real type and read-back reconstructs it. Paired with the read-side guard so any residual masking
fails loud at the point of loss, not three hops later as `IndexNotSet`.

**Seam (pin exactly — trace before editing).**
- The `variable.set` Type clause / mint path (`plan.md §5`, `code-draft.md` "Blocker 1"): when
  `declared.IsApex && value has a more specific intrinsic type` → keep the value's type; else existing
  mint-to-declared. Confirm whether the re-stamp is in `variable/set` or the shared mint path.
- Guard in `source.Value` (`code-draft.md` "Guards"): declared **genuine container** (`dict`/`list`)
  materialized to a non-container leaf → throw. **Risk to pin:** the container predicate must be
  `dict`/`list`, **not** the bare apex `object`/`item` — a value legitimately declared `object` may
  hold a scalar (`set %x% = 5 as object`), and with Stage 1 in place an apex declaration no longer
  reaches read as "declared object" anyway. Verify `IsContainer` excludes the apex before wiring it.

**Verify gate.**
- `rm -rf` the plan `.build` cache; `plang build` (cache off) — `IndexNotSet` at `BuildStep/Start.goal:6` is gone. Fail-loud assert: at the `%plan%` write boundary, the Data's type is `dict`/`item`, **not** `object`.
- C# suites: no new reds vs baseline. Watch the blast-radius `variable.set` tests (`BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins`, `ValidateActions_*`, `Set_NullValue_StoresAndRetrieves`).
- **Decision point:** if the build clears here, Stages 2–4 are confirmed as the *durable* layer, not the unblock — proceed but know they're separable.

---

## Stage 2 — The kind behavior under `Type[t].Kind[k]` (json + `*`, under `item`)

**Intent.** A kind owns what you can do with a value of that kind — navigate/enumerate/load — and is
addressed *under its type*: `Type["item"].Kind["json"]`, `Type["item"].Kind["*"]`. This is the
genuinely new surface. Reconcile with the **existing** `app.type.kind` value token — do not stand up a
second `@this`.

**Seam.**
- Reconcile the existing `app/type/kind/this.cs` (the kind value token: names a kind, maps kind→type
  via readers) with the new navigate/load/build behavior. My call per `code-draft.md` "Open for the
  implementer": either unseal the token + per-format subclass, or the token delegates to a registered
  per-`(type,kind)` behavior. Decide first — it sets the file layout. **Confirm class→item / new-type
  shape with Ingi before writing** (memory: confirm class→item).
- `Type[t].Kind[k]` accessor on `type.@this`; `KindOf(type, name)` resolution.
- `app/type/kind/json.cs` + `app/type/kind/reflection.cs` (`*`): `Step` / `Data` (child factory) /
  `Enumerate`; json `Load` **delegates** to the single reader (Stage 4), no second parse.
- Base `behavior` template = the segment loop; reuse `Segment.Index.ResolveKey(ctx.Variable)` — **no**
  second `Key(...)` resolver.
- `KindOf(clrType)` (CLR→kind) **reuses** `KindHooks.Of` + `clr.Mint`'s `ResolveName` — no third path.

**Verify gate.** New C# unit tests: `Type["item"].Kind["json"].Navigate` walks object→[index]→member
to a scalar; `Enumerate` yields array/object children; `*` reflects a POCO property. No new baseline reds.

---

## Stage 3 — `clr` delegates; reflection relocates into the `*` kind

**Intent.** `clr` stops navigating itself. `clr.Navigate` becomes pure delegation to
`Type[EffType].Kind[EffKind]`; the inline reflection body **moves into** the `*` kind (Stage 2). `Mint`
reports `(item, EffKind)`. `clr` gains an optional `StampedKind` for ambiguous raw forms only.

**Seam.** `app/type/clr/this.cs`: `Navigate(parent, key)` and `Navigate(parent, path)` → the kind;
`Enumerate()` → the kind; delete the reflection + `is JsonElement`/`innerData` switch (the `innerData`
case becomes the `*` kind's `Data` handling of a nested `Data`). `EffKind` via `KindOf`.

**Verify gate.** clr navigation tests still pass (now through the `*`/json kind). No reflection or
type-switch remains on `clr`. No new baseline reds — watch `Data/Navigation_ObjectShape_NavigatesByKey`.

---

## Stage 4 — Reader pivot + parser handoff (the durable clr/json representation)

**Intent.** External structured json — `file.read .json`, http json bodies, the **llm cached path** —
all land as `clr(item, json)` and navigate identically, instead of eager-narrowing to `dict`. This is
where the cached-llm path (which Stage 1 can't reach — it has no richer intrinsic) also gets fixed.

**Seam.**
- `object/serializer/json.cs:Read` → return `new clr.@this(parsed, ctx.Context)` instead of
  `Parse(parsed)` → dict. `Read` stays the single parse owner; `item.serializer.json.Parse` (universal
  DOM narrower) is **untouched**. **Trace `Read` vs `Parse` vs `source.Value`/`Build` before cutting** —
  the one place a wrong cut regresses every json read.
- `data/this.Navigation.cs`: when `_item is clr c` → `c.Navigate(this, path)` (hand the whole tail).
  Native dict/list + item types keep the existing per-hop walk.

**Verify gate.** Rewrite the blast-radius tests that assert the *old* eager-dict behavior to the clr/json
model (`Materialize_JsonObjectRoot_NarrowsToDict`, `Materialize_JsonArrayRoot_*`, the `Body_*`, `Cut*`,
`*MalformedJson*` families). `%ref%` full-match still borns a `variable` (`type.Build`), never a clr —
assert this explicitly. `plang build` still green.

---

## Stage 5 — Producer door `context.Ok(raw, kind)` (OpenAi collapse)

**Intent.** A producer hands raw + the kind it asked for; the kind loads it. No `format == "json"`
ladder; fresh == cached (one line each). `json` → clr, `md` → text, unknown → text.

**Seam.** `context.Ok(raw, kind)` → `Type["item"].Kind[kind].Load(raw)` (sugar over the loader —
confirm it reads better than a direct `Load`). `app/module/llm/code/OpenAi.cs` fresh + cached both →
`context.Ok(extracted, kind: format)`; rename local `effectiveFormat` → `format`.

**Verify gate.** Blast-radius `Types/LlmQuery_Build_*`, `Modules/Query_CacheHit_PropertiesPreserved`,
`Roundtrip_LlmDict_ToStep_*` green (or updated to the new representation). Fresh and cleared-cache
`plang build` both green.

---

## Stage 6 — Full sweep + snapshot regen

**Intent.** One clean pass before the scan: `./dev.sh full` (analyzers on — PLNG001/002, TUnit), all
C# suites at-or-below baseline, `plang --test` from `Tests/`, regenerate the llm snapshot json under
`PLang.Tests/Modules/App/Modules/llm/snapshots/` if the producer door changed them.

**Verify gate.** No red outside baseline; PLNG002 zero; `plang build` green from clean.

---

## Stage 7 — OBP scan (`obpscan`)

**Intent.** Structural review of everything landed. Per `Documentation/v0.2/obp-scan.md`: forks first,
then unplanned API surface (new classes/methods not in the plan), the Verb+Noun rule, the 8 shape
smells, `.Clr`-off-boundary. Grounded on the key intents that must survive: a `clr` stays a `clr`; a
`(type, kind)` owns navigate/load/build; convert's outbound owns it; the apex never masks; the reader
pivot never turns a `%ref%` into a `clr`. Report grouped (violation/borderline/clean); update the doc's
"Last scanned" marker.

---

## Explicitly deferred (own branches, after green)

- `identifiers → text` (deep: wire serializer, primitive tables, `Canonicalise`/`Compare`; `text` keys the registry fine without it).
- `Peek → item.@this` (a `source` contract change — `source.Peek()` returns raw CLR by design).
- **Convert** (`Data.Convert(kind)` → `Type[t].Kind[k].Build`) — ships with the first real converter; only the *shape* is left room in Stage 2's base.
- yaml/xml kinds; the convert graph (md→html→pdf); the write-side (mutate/copy-on-write); the `- add type <dll>` action surface.

## Commit rhythm

Green (or at-baseline) chunk per stage → commit → push (pipeline reviews origin). Narrate what landed.
