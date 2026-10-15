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

## What is still OPEN (see v3/stage3-result.md for the full detail; reported to the architect)
1. **bool needs its kind** — `IsClosed=true` (pinned by `PickOptionTests`); the type model forces `IsClosed=false` once an `Offer` static is set. Kind-removal reverted; bool's kind stays pending a ruling (a type-level "closed" hook is a locked-core addition).
2. **Nested-kind read (`KindNestsTests`, ruling 1)** — the type serializer must recurse on an object `kind`; the `.kind` slot is `kind.@this`, so nesting a `type.@this` is a locked-core design call. Red at the inherited baseline too (pre-existing TODO).
3. **App-wide kind search** has live callers (reader binary-narrow, text coiner, OpenAi by-MIME, `{binary,X}` fallback) — removing as the demolition says would strand them. Needs a ruling on which to keep.
4. **`type.format` filter removal** needs FormatKindTests surgery — do now or stage 6?
5. **Blocked on Ingi:** `separator` (Q2, held; 2 test `.pr` counted) and `JsonIsItemsKind` (held, per its own goal comment).
6. **template** has no parent value type — left on the non-generic base; is it a standalone kind or should a `template` type exist?
7. **kind/strict plang tests** were red at baseline; likely stage-4 (bytes). Confirm scope.

## For v3 (this version)
Stage 2 accepted (decision 780). No reviewer flags carried in. The stage-3 trace corrected my first shape (the `FamilyOf` swap doesn't apply to the subclass families — `[Kinds]` stays for them); architect agreed.
