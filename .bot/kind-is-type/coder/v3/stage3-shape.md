# Stage 3 shape — every family's kinds are types

Branch `kind-is-type`, decision 774 rulings 1–6. Trace done; this is the shape for approval **before** any code. Nothing here changes a type's name (checked at the end).

## 1. The one mechanism change

Today a family's kind base derives from the **non-generic** `app.type.kind.@this` and names its parent with a string `Owner` override (`duration/kind/this.cs:12` `Owner => "duration"`, …). A kind's family is then found by `FamilyOf` → `IdentityOf` **walking up base classes for `[Kinds]`** (`type/item/this.cs:531–536`), and the registry enlists each `[Kinds]` subclass as a `kind.subclass.@this` (`Registry.cs:359–363`).

Stage 1 already built the target for `number`: a family's kind base derives from the **generic** `kind.@this<T>` (`type/kind/this.Generic.cs:16`), whose ctor sets the kind's Family to T's type entry (`:21–22`). The parent is carried by the type parameter, not a string and not a base-walk.

**The change, once, for every family:** move each family's kind base from `kind.@this` to `kind.@this<family>`; drop its string `Owner` override (T carries it); and swap the family-finder from the `[Kinds]` base-walk to reading the `kind<T>` generic base. Then `[Kinds]` and the base-walk go, and the app-wide kind search goes (below).

`FamilyOf` after the swap (sketch — `type/item/this.cs`):
```
// the family is the T of the kind<T> base, not a [Kinds] walk
for (var b = ct.BaseType; b != null; b = b.BaseType)
    if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(app.type.kind.@this<>))
        return (b.GetGenericArguments()[0], <kind name>);
```
The kind name stays what it is today (the `[PlangType]` word or the namespace leaf). `Registry.cs:359–363` keeps enlisting, now fed by the generic-based `FamilyOf`; `Registry.cs:310–311` (hold a kind whose `Owner` names a type) folds into the same path (the generic names the owner).

## 2. The app-wide kind search goes

`type/list/this.cs:46–52` `Kind(type, kind)` resolves a type's kind, and when the named type doesn't hold it, **falls back to `Kind(kind)`** (`:29–30`) → `Held(key)` (`:57–69`), a walk over *every* type's kinds by bare name (and the "ambiguous across two types" throw). Once every kind is held under its parent, a kind is always reached through its parent, so:
- `:50–51` the fallback `: Kind(kind)` goes (a missing kind is the type's empty kind, or minted for the name under that type — never an app-wide search).
- `:29–30` `Kind(string)` and `:57–69` `Held` go.
- `Kind(System.Type clr)` (`:76–80`, the host lookup over item's json/dict/list/`*`) **stays** until 673.

This is ruling at line 162 of the plan ("names on two levels: a kind is asked of its parent, never of the app; no second lookup by kind name"). It also removes the `SameKindNameUnderTwoTypes` ambiguity *by construction* — `{hash, sha256}` and `{binary, sha256}` are each found through their own parent.

## 3. `type.format` (the filter) goes — not the serializer

Three different things share the word "format"; **only the first goes**:
- **GOES:** the per-type formats **filter** — `type/this.cs:126` `format => new(_kind)` + `type/format/this.cs` (`format.@this`, a view of a type's kinds that carry a MIME/extension), surfaced as `%!app.type.<x>.format.list%`, read by `type/root/this.cs:14–23` and `FormatKindTests`/`KindListTests`. A type's formats are just its kinds that have a MIME/extension — callers ask the kind list and filter, no separate view object.
- **STAYS:** the `app.type.format` **serializer** namespace — `IWriter`/`IReader`/`IOutput`/`value`/`TokenKind` (locked core, used everywhere, e.g. `type/kind/this.cs:173,272`).
- **STAYS:** `%!app.type.format%` = `type.root.format`, the **format in play** (`type/root/this.cs`), set by `set %!app.type.format% to …` and `--app.type.format=`. Unrelated to a type's own kinds.

## 4. Per-family migration

| family | today | → stage 3 |
|---|---|---|
| **number** (done) | `number.kind.@this : kind<number>`, 16 leaves | the template, no change |
| **duration** | `duration/kind/{this,iso,dotnet,short}`, base `: kind.@this`, `Owner=>"duration"` | base `: kind<duration>`, drop `Owner`; leaves unchanged (`: duration.kind.@this`) |
| **size** | `size/kind/{this,iec,si}` | base `: kind<size>`, drop `Owner` |
| **template** | `template/kind/{this,plang}` | base `: kind<template>`, drop `Owner` |
| **path** | `path/scheme/this.cs` one sealed `kind.@this` holding a factory; file/http/https registered in `app/this.cs:310–312`; `path/this.cs:12` has `[Kinds]` | `path/scheme/this.cs : kind<path>`; `[Kinds]` off path; registration in `app/this.cs` unchanged (named values, open set) |
| **text** (md/xml own formats) | `[Format]` attrs on `text/this.cs:30–38`, minted to bare `kind.@this` at `Registry.cs:316–323` | the minted format kind is typed to its owner — `kind<text>` (see open question Q1). The `*format` coiner (`text/kind/this.cs`) + wrapper (`text/kind/format/this.cs`) are **stage 4** |
| **image** | `[Format]` × 8 on `image/this.cs:19–26`, minted at `Registry.cs:316–323` | minted as `kind<image>` (Q1) |
| **table** | `[Format("csv")]` on `type/table/this.cs:26` | minted as `kind<table>` (Q1) |
| **separator** | its own **type** `separator/this.cs:9 : item.@this` with `kind/{line,comma,tab,space,semicolon}` subclasses | **collapses** to a single `kind<text>` carrying an **open named-value set** (line/comma/tab/space/semicolon, any chars allowed). Ruling 6: "separator is a kind<text> with its named values." The 5 kind subclasses become entries in separator's named-value list, not classes. (See Q2 — this is the one real restructure.) |
| **choice** | `choice/set/this.cs:12 : kind.@this` (abstract), 3 set kinds (named/family/member), `Of(type)` closes to `choice<T>` | base set `: kind<choice>`; ruling 6 "one generic set class, a kind<choice>, holding the enum." The enum/Choices/kind-family mechanisms stay |
| **bool** | `bool/kind/this.cs:8 : kind.@this`, `IsClosed`, true/false | bool's **closed named-value pair** (ruling 6 "bool's true and false"), a `kind<bool>` holding the two, offers-only |
| **item json/dict/list/reflection** | `type/item/kind/{json,dict,list,reflection}` host kinds over C# objects | **host kinds, stay until 673** (ruling 9). json-as-`{item,kind:json}` is reached; item's json **navigator** goes in **stage 4**. `Kind(System.Type clr)` stays |

## 5. The categories (every kind classified — the stage-3 check)

Every kind class from the trace falls in exactly one bucket; none unclassified:

- **`kind<T>` (a type under its parent):** number's 16; duration iso/dotnet/short; size iec/si; template plang; path's scheme; text's md/xml/yml/ini/goal/llm/… (via Q1); image's 8 formats (Q1); table's csv (Q1); the `wire` plang kind (`wire/kind/plang/this.cs` → `kind<wire>`, see Q3); **module-owned families** — crypto hash (sha256/keccak256), archive (compression/bundle), llm conversation, test junit (same pattern, module-owned — Q4).
- **named values (the type's own list, not sub-kinds):** separator (open), bool (closed true/false), choice's sets (closed enums). These are a type/kind's value list, carried by the type, per ruling 6.
- **host kinds kept for 673:** item's `json`, `dict`, `list`, `reflection` (`*`) over a C# carrier. Plus the kind **infrastructure** that is not a family kind and simply stays: `type/kind/empty/this.cs` (a type's own empty kind), `type/kind/subclass/this.cs` (a kind minted by name).

application/plang's `wire` kind is accounted for (Q3).

## 6. Nested kinds and the host base (trace items 5–6)

- **Nested kind `{list, kind: {list, kind: path}}`** (item 6): a list's kind names its element **type** (`type/item/list/kind/this.cs`, `list/kind/element/this.cs`). Because a kind IS a type, the element can itself be a kinded type, so the nesting is already structural — `list.kind.@this : kind<list>` carries the element type entity, which may be `{list, kind: path}`. Tests: `ListKnowsItsElement`, `KindNestsTests`. No new mechanism; just the `kind<list>` retype.
- **Host verbs to a host base the clr carrier holds** (item 5): json/dict/list/reflection carry navigation verbs (Descend/Enumerate/Parse/Clr/Read/Output). The direction is a `host`-kind base holding these, which `app.type.clr.@this` (the carrier) owns; json/dict/list/reflection become host-kind instances over the carrier. **The move is 673 (host kinds) / stage 4 (json navigator)**; stage 3 only names the destination so the retype of the other families doesn't entangle the host verbs.

## 7. Tests this stage owns

| test | what it pins |
|---|---|
| `JsonIsItemsKind` | `'{"a":1}' as json` → `{item, kind: json}`; `is text` false |
| `ListKnowsItsElement` | `list of path` → element born a path; `{list, kind: path}` |
| `KindNestsTests` (C#) | `{list, kind: {list, kind: path}}` round-trips |
| `SameKindNameUnderTwoTypes` | two types each with a kind of one name, each found via its parent, no clash (now true by construction) |
| `NamedValuesBelongToTheType` | separator's comma/… and the "any chars" open case |
| the four `kind/strict` tests | `%bytes% as image/gif, strict` refused on non-gif |

Plus: the surface golden and decider pins **must name the same types** (no name changed); the plang suite no worse than its baseline except the above; PLNG001/002 at 0.

## 8. Open questions for the architect (ruling needed before code)

- **Q1 — `[Format]`-declared kinds (text md/xml, image's 8, table csv): subclass or typed mint?** Option A: keep `[Format]` on the value class (it carries only data — MIME/extension/magic-bytes, no behaviour) and have the registry mint a **`kind<T>`** instead of a bare `kind.@this`. Option B: a `png/this.cs : kind<image>` class per format. **I recommend A** — the formats differ only in data, so per-format classes would be ~17 near-empty classes (a shape smell); `[Format]` stays as the declaration, the family comes from the declaring type. This keeps `[Format]` (the plan doesn't list it as demolished) and still makes every format a `kind<T>`. Confirm A, or you want the classes.
- **Q2 — separator's collapse.** separator stops being a top-level `item` type and becomes a `kind<text>` with an open named-value set. That removes `separator` from the top-level type list. Does "no type's name changed" hold when `separator` becomes a kind name under text (`{text, kind: separator}`)? A kind is a type, so the name survives — but the **surface golden / decider offerings** will show `separator` as text's kind, not a top-level type. Is that the intended surface move (I re-pin naming 774/this decision), or should separator stay a top-level type that simply *is* a `kind<text>`?
- **Q3 — the `wire` kind.** `wire/kind/plang/this.cs` (application/plang) is the serializer's own format kind. Make it `kind<wire>` for consistency, or is it a serializer/host kind that stays as-is (locked core)? The check requires it classified; I've tentatively put it in `kind<T>`.
- **Q4 — module-owned families scope.** crypto hash, archive, llm conversation, test junit derive from the non-generic base today. "Every family" by the letter includes them (same mechanical retype to `kind<T>`, module-owned). In scope for stage 3, or a follow-up? I lean in-scope (the base-walk/`[Kinds]`/app-search they rely on all go this stage, so leaving them on the old base would strand them).

## 9. What stays (guard)
Every type's name and `[PlangType]` word; the written form `{name, kind}`; the load-time refusal of two classes claiming one name; every `.pr`, prompt and saved row; the serializer (`app.type.format.*`), the format-in-play (`%!app.type.format%`), and the C# host kinds (json/dict/list/reflection) until 673.
