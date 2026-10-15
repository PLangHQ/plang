# coder summary — kind-is-type

**Version:** v3 (stage 3). v1/v2 = stages 1–2 (done, accepted).

## What this is
Decision 774: "a kind is a type." `kind<T> : kind : type`. Plan: `test/plan/kind-is-type/start.md`. Stages: 0 done; 1 done+accepted; 2 done+accepted (settings are their own types; persisted rows read by their own type, decision 779); **3 in progress (this version)**; 4–6 pending.

**Stage 3 — "every family's kinds are types":** each type-family's kinds move from the non-generic `kind.@this` + a string `Owner` onto the generic `kind<family>` (as stage-1 number already did). Full trace + shape: `v3/stage3-shape.md` (architect rulings absorbed). Result: `v3/stage3-result.md`.

## What was done (done + green)
- **`kind<T>` provides `Owner` from T** (`PLang/app/type/kind/this.Generic.cs`) — families drop their `Owner` override.
- **Codec families → `kind<family>`:** duration, size, hash, archive, conversation, junit, wire; number's redundant override dropped.
- **`[Format]` → `kind<T>` (Q1-A):** one concrete generic `PLang/app/type/kind/format/this.cs`; `Registry.cs:316–323` mints one per `[Format]` typed to the declaring type. text/image/table formats are now `kind<T>`.
- **path schemes → `kind<path>`** (`PLang/app/type/item/path/scheme/this.cs`).
- **Subclass families (input/screen/browser/query):** `[Kinds]` + the `FamilyOf` base-walk STAY (value classes keep `: parent.@this`); the enlisted kind object is now the typed `PLang/app/type/kind/subclass/this.cs` `@this<T> : kind<T>` (`Registry.cs:359–365`).
- **list element kinds → `kind<list>`** (`PLang/app/type/item/list/kind/{this,element}.cs`).
- **Surface golden re-pinned** under 774 (diff = only `kind<T>` bases + the new generic classes; no type name changed).

**Gate (verified):** C# at the 8 pre-existing baseline fails (no new reds); plang **254/24/12/3 — identical to the stage-2 baseline, zero regressions**; `SameKindNameUnderTwoTypes`, `ListKnowsItsElement` green.

## Code example
A family's kind base, before → after:
```csharp
// before
public abstract class @this : global::app.type.kind.@this {
    protected @this(string name) : base(name) { }
    protected internal override string Owner => "duration";   // string parent
}
// after — the parent is the type parameter; Owner comes from it
public abstract class @this : global::app.type.kind.@this<global::app.type.item.duration.@this> {
    protected @this(string name) : base(name) { }
}
```

## Architect rulings on the 7 (resolved)
- **Items 2, 3, 4 — DONE + green** (architect-assigned trio). See `v3/stage3-result.md`.
  - **2 (a kind nests):** `KindNestsTests` green. Reader recurses on an object `kind`; writer emits a kinded kind as the object. Nested kind rides via hidden ctors (internal/private-protected) — no public/protected surface member. Commit 5ad1cb74a.
  - **3 (ask the parent, not the app):** dropped the `Kind(type,kind)` fallback; redirected binary-narrow (type/reader) → extension door, OpenAi → `Format`. The bare-name `Held` path is **retained solely for `text/kind:25`** (the `*format` coiner), which stays until stage 4 — flagged to the architect. Commit dac90dd0e.
  - **4 (type.format filter):** `type.@this.format` + `app.type.format.@this` removed; tests ask the kinds. Serializer + format-in-play stay. Commit babe32632.
- **bool:** kind stays (needs `IsClosed`); architect filed it on the backlog for Ingi (decision 782). Not this branch.
- **template:** stays on its base (a facet, not a value type); `plang` is a named value of that facet. Comment + classify; revisit stage 6.
- **JsonIsItemsKind + kind/strict:** stage 4 (test-designer fixed JsonIsItemsKind's header). Not stage 3.

## OPEN — separator (783), in progress
Ingi ruled separator becomes a `kind<text>` (`{text, kind: separator}`) with an open named-value set (line/comma/tab/space/semicolon + any chars); its 5 kind classes become named-value entries; `%line% is text` true; it stops being a top-level type. **Shape sent to the architect for one confirm** (separator value = a plain `text.@this` with kind separator, `separator.@this` class removed; split/join take `text`). On confirm: implement + re-pin surface/decider under 783 + NamedValuesBelongToTheType green + list split/join green. The 2 `.pr` to rebuild (handed to the builder): `test/module/list/.build/{start,listops.test}.pr`.

## For v3 (this version)
Stage 2 accepted (decision 780). No reviewer flags carried in. The stage-3 trace corrected my first shape (the `FamilyOf` swap doesn't apply to the subclass families — `[Kinds]` stays for them); architect agreed.
