# Implementation stages — clr + kinds under type

**Branch:** `clr-navigators`. Build target: architect's `code-draft.md` (authoritative) + `plan.md`.
Baseline: `baseline.md` (142 red before Stage 1). Each stage ends green-or-baseline + a commit.

Ordering principle: **build the kind machinery, let it dissolve the bug, confirm at the build-clears
gate.** A quick trace showed the fresh llm result is already a `clr(JsonElement)` that navigates by
*reflection* (kind derives to the CLR FullName) instead of json — so the machinery (`KindOf(JsonElement)
→ json` + the json navigation kind + clr delegation) is very likely the whole unblock on its own,
*without* touching `variable.set`/producer/reader. Don't pin the seam up front — the machinery fixes
every form the value can take (`clr(JsonElement)`, `source(object)`); the empirical proof is `plang
build` clearing at the end. Producer/reader/apex changes are **durability** (uniform for
`file.read`/http/llm-cached), verified at the gate, not prerequisites.

---

## Stage 1 — The kind machinery (Type[t].Kind[k]) + clr delegation — the unblock

**Intent.** Make a `clr` navigate by its **kind**, not by C# reflection. `KindOf(JsonElement) → "json"`
so any `clr(JsonElement)` (the llm result already is one) navigates via the json kind — `%plan.steps%`
walks the JsonElement instead of reflecting a nonexistent C# property. This *is* the unblock.

**Seam.**
- Reconcile the existing `app/type/kind/this.cs` (kind value token: names a kind, maps kind→type via
  readers) with new navigate/enumerate behavior addressed as `Type[t].Kind[k]` — unseal+subclass, or
  the token delegates to a registered per-format behavior. Decide first (sets file layout).
- `app/type/kind/json.cs` + `reflection.cs` (`*`): `Step` / `Data` (child factory) / `Enumerate`;
  base `behavior` = the segment loop, reusing `Segment.Index.ResolveKey(ctx.Variable)`.
- `KindOf(clrType)` (CLR→kind) reuses `KindHooks.Of` + `ResolveName`; **add `JsonElement → "json"`** to
  that bridge — this is the pivotal line.
- `clr.Navigate` → pure delegation to `Type["item"].Kind[EffKind]`; relocate the reflection body into
  the `*` kind. `Mint` reports `(item, EffKind)`.

**Verify gate.**
- New C# unit tests: `Type["item"].Kind["json"].Navigate` walks object→[index]→member to a scalar;
  `Enumerate` yields children; `*` reflects a POCO. clr navigation still works (now via the kinds).
- **The real proof:** `plang build` from clean (cached snapshots) — `IndexNotSet` at
  `BuildStep/Start.goal:6` is gone. If it clears here, the producer/reader/apex work (Stages 2–4) is
  confirmed as durability, not unblock.
- C# suites: no new reds vs baseline.

---

## Stage 2 — Reader pivot + parser handoff (durable clr/json for file.read / http / cached-llm)

**Intent.** External structured json — `file.read .json`, http json bodies, the llm **cached** path —
all land as `clr(item, json)` and navigate identically, instead of eager-narrowing to `dict`.

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

## Stage 3 — Producer door `context.Ok(raw, kind)` (OpenAi collapse)

**Intent.** A producer hands raw + the kind it asked for; the kind loads it. No `format == "json"`
ladder; fresh == cached (one line each). `json` → clr, `md` → text, unknown → text.

**Seam.** `context.Ok(raw, kind)` → `Type["item"].Kind[kind].Load(raw)` (sugar over the loader —
confirm it reads better than a direct `Load`). `app/module/llm/code/OpenAi.cs` fresh + cached both →
`context.Ok(extracted, kind: format)`; rename local `effectiveFormat` → `format`.

**Verify gate.** Blast-radius `Types/LlmQuery_Build_*`, `Modules/Query_CacheHit_PropertiesPreserved`,
`Roundtrip_LlmDict_ToStep_*` green (or updated to the new representation). Fresh and cleared-cache
`plang build` both green.

---

## Stage 4 — Full sweep + snapshot regen

**Intent.** One clean pass before the scan: `./dev.sh full` (analyzers on — PLNG001/002, TUnit), all
C# suites at-or-below baseline, `plang --test` from `Tests/`, regenerate the llm snapshot json under
`PLang.Tests/Modules/App/Modules/llm/snapshots/` if the producer door changed them.

**Verify gate.** No red outside baseline; PLNG002 zero; `plang build` green from clean.

---

## Stage 5 — OBP scan (`obpscan`)

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
