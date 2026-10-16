# coder v3 — kind-is-type stage 3 (trace + shape)

**Stage:** 3 — "every family's kinds are types" (decision 774, rulings 1–6).

**This is a trace-first design task.** The architect's instruction: *"Trace first, then send the shape."* DONE WHEN "stage 3's shape is with me." **No implementation this version** — the deliverable is `v3/stage3-shape.md` sent to the architect for approval before any code.

## What the architect asked the trace to cover
1. Each family's kinds becoming `kind<T>`: duration, size, template, separator (a `kind<text>` with named values), path file/http, text md/xml, image formats, table csv, item json (`{item, kind: json}`), choice's sets as one `kind<choice>` holding the enum.
2. The app-wide kind search going (`type/list/this.cs:51` fallback, `:57–69` `Held`).
3. `[Kinds]` and the base-class walk going (`type/item/this.cs:528–533`).
4. `type.format` going (the per-type formats filter — NOT the serializer).
5. The host verbs moving to a host base the clr carrier holds (the caller list).
6. A list's element and a nested kind (`{list, kind: {list, kind: path}}`).

Tests named: JsonIsItemsKind, ListKnowsItsElement, KindNestsTests (C#), SameKindNameUnderTwoTypes, NamedValuesBelongToTheType, the four kind/strict tests.

Check: every kind is a `kind<T>`, a named value, or a host kind kept for 673 — none unclassified (application/plang's `wire` kind included); no type's name changed.

## Approach
- Core anchors traced directly (this file's author).
- Per-family current-state map gathered by an Explore fan-out (read-only).
- Synthesis → `v3/stage3-shape.md` → send to architect [510469]. REPLY: Done/HASH/CHECK/OPEN.

## Core mechanism (traced)
- **The template (stage 1, done):** `kind<T> : kind : type` at `type/kind/this.Generic.cs` — its ctor sets the kind's Family to T's type entry via `NameOf(typeof(T))` (ruling 3). A family's kinds subclass an abstract `<family>.kind.@this : kind<<family>.@this>` with `Owner => "<family>"`; each concrete kind overrides only what is its own (parse/token/MIME/extension). Template: `type/item/number/kind/this.cs` + `.../decimal/this.cs`.
- **The family-finder swap:** today `FamilyOf(t)` → `IdentityOf(t).Family` walks base classes for `[Kinds]` (`type/item/this.cs:531–532`), and `Registry.cs:360` enlists each concrete family member as a `kind.subclass.@this(kindName, family, clr)` from that. Stage 3 makes the family come from the `kind<T>` generic base (T), and `[Kinds]` + the base-walk go.
- **The app-wide search:** `type/list/this.cs:46–52` `Kind(type, kind)` falls back to `Kind(kind)` (`:29–30`) → `Held(key)` (`:57–69`), the walk over every type's kinds by bare name. Goes once every kind is held under its parent (a kind is always found through its parent). `Kind(System.Type clr)` (`:76–80`, the host lookup) STAYS until 673.
- **`type.format` (the filter, not the serializer):** `type/this.cs:126` `format => new(_kind)` + `type/format/this.cs` (`format.@this`, a view of a type's kinds that have a MIME/extension), read by `%!app.type.<x>.format.list%` and `FormatKindTests`. Goes. **DO NOT confuse** with (a) the `app.type.format` IWriter/IReader/IOutput/value serializer interfaces (locked core, stay) or (b) `%!app.type.format%` = `type.root.format`, the format-in-play (stays).

## Checks I will run before sending (no code; grep/read only)
- Enumerate every kind class and classify each: `kind<T>` | named value | host kind (673). None unclassified, incl. application/plang's `wire` kind.
- Confirm no type's name changes (the surface golden + decider pins name the same types).

## Status
Not blocked. Trace in progress; shape to follow in `stage3-shape.md`.
