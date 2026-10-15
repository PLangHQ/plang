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

## Stage 3 CLOSED OUT except separator (held on Ingi)
Every kind is classified — the stage-3 check passes:
- **kind<T>:** number, duration, size, hash, archive, conversation, junit, wire; `[Format]` mints (text md/xml/…, image's 8, table csv); path scheme; list element; the typed `kind.subclass<T>` (input/screen/browser/query).
- **named values:** choice sets, bool (true/false), separator (held).
- **host kinds (673):** json, dict, list, reflection.
- **stage-4 deferrals:** text's `*format` coiner + wrapper.
- **facet:** template (`type.Template`; `plang` a named value of it; commented, revisit stage 6).
Decider pins hold (PickList 8/8, PickOption 19/19); C# at the baseline 7 (KindNests now passes); plang 254/24/12/3; surface re-pinned; no type name changed.

**separator — HELD on Ingi.** Ingi ruled it becomes a `kind<text>` (`{text, kind: separator}`) — a text SUBCLASS (`separator.@this : text.@this`), like `screen.window`, so the slot stays `Data<separator.@this>` and the LLM keeps the names. But `text.@this` is **sealed** — subclassing it is a locked-core change. The architect sent **option A (unseal text) to Ingi** (suggested yes); option B (separator overrides Type without subclassing) was rejected (a value whose Type says text but isn't a `text.@this` lies and fails the cast). On Ingi's yes: unseal text, `separator.@this : text.@this`, `[Kinds]` on text, the 5 kind classes → named-value entries, re-pin surface/decider under 783, NamedValuesBelongToTheType + list split/join green, rebuild `test/module/list/.build/{start,listops.test}.pr`.

## For v3 (this version)
Stage 2 accepted (decision 780). No reviewer flags carried in. The stage-3 trace corrected my first shape (the `FamilyOf` swap doesn't apply to the subclass families — `[Kinds]` stays for them); architect agreed.
