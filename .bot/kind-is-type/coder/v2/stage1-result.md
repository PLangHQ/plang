# Stage 1 result — a kind is a type (decision 774)

Pushed: `eb4a29c94`. The probe: make number's kinds `kind<number>` where `kind<T> : kind : type`, and
measure the cost of `kind : type` on one family before all of them.

## What landed

- **`kind.@this : type.@this`** (concrete) — a kind IS a type. It drops its own `Name` (inherits
  type's); keeps its format + host verbs (they stay on the base this stage, per the caller trace —
  every host-verb caller holds `kind.@this`, not a host kind; stage 3 moves them).
- **`kind.@this<T> : kind.@this`** (`type/kind/this.Generic.cs`) — the parent-carrying seam. Thin for
  now: number needs no fact-forward (a number value asks its kind only Create/Write, and a kinded
  type's facts already resolve to the parent through `type`'s kind→Family path).
- **`number.kind.@this : kind<number>`** — `decimal` et al. unchanged except the base.
- **A kind has no sub-kind**: the empty kind's `BornKind() => null` (was a self-reference). This ends
  both the `kind : type` ctor's kind-minting recursion AND the STJ `.kind.kind…` serialization cycle.
  `type.@this._kind` is now nullable; the `kind` getter is `_kind!` (null only for a kind, which no
  C# path navigates — only a serializer reads it, and null stops it).
- **A kind renders/compares/lowers as its name**: `kind.Output` writes `Name`; `kind.Clr(string|object)`
  returns `Name`. So `%x!type.kind%` is `"json"` for equals/output, while `.kind.name` navigates.
- **`.kind` returns the navigable kind** (`type.Get` stopgap): returns the kind itself (empty → the null
  citizen). Detects the kind whether it lands direct (an item now) or clr-wrapped.
- **Kinds excluded from top-level type discovery** (`Registry.NameOf`): a kind is found only through
  its parent (ruling 2); `kind : type : item` would otherwise make every `@this` kind a top-level type.
- **Clashes reconciled**: `Offers`/`IsClosed` → `override` (type's were virtual); `Values`/`Description`/
  `Example`/`Alias`/`Names`/`IsSequence` are CS0108 hides — harmless, consistent with the codebase's
  existing ~30, behavior preserved (the kinded-type path reads facts through a kind-typed reference).
- **Locked-core surface re-pinned** with 774 named — diff is exactly the kind:type additions, no
  top-level kind leak.

## The gate

- `DecimalIsANumber`: **green**.
- plang suite: **250/28/12/3** (baseline 250/29/12/3 — DecimalIsANumber fixed, no regressions;
  baseline-verified the 4 compare/sort suspects were already red at the tip).
- C# gate: baseline 8 pre-existing; +10 from kind:type, 8 resolved. **2 open** (with the architect):
  - `AJsonFilesTypeKind_IsTheTextJson` — asserts the pre-774 `.kind`-as-text stopgap; stale, needs a
    774 update (`.kind.name`=="json" + renders "json").
  - `ThePromptCSettingsAndKeys` — `%!build.setting%` lost its args (`(cache: choice<cache> = use,
    files: list = [])` → `()`). The kind:type ripple into settings (settings are `setting.kind` until
    stage 2). Scope call pending: fix in stage 2 (as planned) or patch in stage 1.

## Cost, summarized

Modest and mechanical, as the probe hoped: the one real hazard was the self-referential empty kind's
STJ cycle (now null), plus the member-name diamonds (reconciled/benign). The open settings regression
is the one place kind:type reaches beyond the type system — and that boundary is exactly stage 2's.
