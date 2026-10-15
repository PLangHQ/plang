# v2 — Fundamentals, part 2: the type system and the value lattice

## Context

v1 shipped the in-tree developer docs for the app root and the module system
(`PLang/app/start.md`, `module/start.md`, `module/file/start.md`). Ingi said
"yes go" to continue the tree; the next fundamentals are the **type system**
(`type/`) and the **value lattice** (`type/item/`). "How `item.list` works" was
only sketched in v1's conventions section — this version does it properly.

Same contract as v1: docs live **inside the code tree** as `start.md`; audience
is the **C# developer**; every page is *what → where is the source → how the
plang maps to it*; dev docs **point** to the runtime teaching layer, don't
duplicate it; every claim verified against current source.

## Scope of v2

1. **`PLang/app/type/start.md`** — the type system.
   - The two halves, kept distinct:
     - **`type.@this`** (the type *entity*) — `{Name, Kind, Strict, Template}`,
       itself an `item`; a plang value you can author (`as image/gif, strict`),
       hold in a variable, and ride in a `.pr`. Carries folded catalog knowledge
       (Property, Description, Example).
     - **`type.list.@this`** (alias `AppTypes`, reached at `app.type` via
       `type.root.@this`) — the *registry*: selection only. Lookups: `[name]`
       (throws), `[System.Type]` (null on miss), `Mime`, `Format`, `Extension`,
       `Kind`, `Reader`. `PlangName` is the CLR→plang map (int→number, List→list,
       enum→choice, raw→clr).
   - **Kind** — a subtype refinement; "a file format is a kind of its type"
     (png is a kind of image, md of text, int of number). `[Format]`, MIME,
     extension. (Agent-sourced detail folded in.)
   - **Property** — a type's members as catalog `property.@this`.
   - **`type.current.@this<T, TList>`** — the generic giving a concept both a
     `.list` and a `.current` (the shape behind `app.goal`, `app.actor`, etc.).
   - **Birth vs make**: `Create` (fires `on.create`) vs `Make` (re-type, fires
     nothing); the entity door (build the declared type) vs the apex
     `item.Create` (build whatever the raw is).
   - plang→C#→source cheat-sheet for types.

2. **`PLang/app/type/item/start.md`** — the value lattice.
   - **`item.@this`** — the apex (≈ C# `object`); every value is `: item.@this`.
     Storage-free base: behaviour only, no value slot. The universal contract is
     truthiness (`IBooleanResolvable`) + the lazy narrow (`Value` door) only;
     ordering/equality are opt-in interfaces.
   - **`item.Create`** — the inferred lift: raw CLR → plang value (the rungs:
     is-item → container narrowing → ownership lift → enum→choice → clr).
   - **How `item.list` works** — `type.item.list.@this<T>`: the native plang
     list, holds `Data` end to end; `Add`, enumeration, ordering. Why a domain
     `X.list` is one of these, and why there are no plurals.
   - **`dict`** — the native dict, briefly.
   - **The core value types** — one compact section each: `number` (+ its `kind`:
     int/long/decimal/double…), `text`, `bool`, `null`, `datetime`/`date`/`time`,
     `duration`, `choice` (CLR enum → closed named set), `path` (apex + scheme
     resolution; file/http are subclasses, covered under the file module). For
     each: C# class, PlangType name, backing storage, Create, Rank.
   - **`Data` vs `item`** — the carrier vs the value; link to data-spec.

Deeper leaves (image, template, signature, sensitive, the full number-kind
matrix) are named, not expanded — later passes or they live well enough in XML
docs. v2 stops at the fundamentals a C# dev needs to read any value type.

## Proposals to evaluate

- `.bot/<branch>/claude-md-proposals.md` — none on this branch.
- `.bot/<branch>/character-proposals.md` — none on this branch.

## Verification

Spine read directly this session: `type/this.cs`, `type/list/this.cs`,
`type/item/this.cs` (head + Create/Value). Support classes (`root`, `kind`,
`property`, `current`, `convert`, `format`) and the core value types surveyed by
two Explore agents; every signature I quote is checked against the file before it
lands in prose.

## Not in scope

- No production `.cs` edits unless a doc-blocking XML-doc gap appears.
- No PLang `.goal` test examples (tester's job).
- The stale `Documentation/architecture.md` / `type-system.md` stay for now;
  retiring/redirecting them is a later cleanup once the in-tree tree covers them.
