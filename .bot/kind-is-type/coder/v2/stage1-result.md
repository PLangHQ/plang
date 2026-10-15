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

## No hides (architect review round, 0faa3ca46)

Every CS0108/0114 hide kind:type added is an override or a rename — a hide is a fork (one member
answering two ways by the reference held), and one of them (`setting.kind.Property` hiding
`type.Property`) was the real cause of the prompt-C regression:

- **Facts** (Property/Values/Alias/Description/Example): `type.X` is virtual, delegating
  `OwnX ?? kind.OwnX ?? Family._<field>` — a kind answers its own via the `Own*` hook, else the
  parent's STORED field (terminates; reading the virtual would loop type→kind→Family). choice.set →
  `OwnValues`, list element → `OwnDescription`/`OwnExample`, int/line → `OwnAlias`.
- **setting.kind.Property/Values**: public overrides — a template reading the kind's `.Property` by
  name gets the options.
- **A kind navigates its own members** (`kind.Get`), not re-resolved through the registry: that
  re-resolution minted a bare type by the kind's name (a kind is no top-level type) and dropped the
  kind's facts — exactly why `%!build.setting%`'s options vanished.
- **Names**: `type.Names` virtual, `kind.Names` override adding MIME + extension.
- **Renames** (coincidental collisions, not the type's member): `reflection.Property` → `Reflect`,
  `setting.kind.Own` → `Private`; `IsSequence(object)` kept with `new` + a note (a host verb, stage 3).
- **Ruling 3 wired**: `kind<T>` sets Family to the parent entry (by T's name), so a kind's teaching
  falls back to the parent's. Pinned: `ADecimalKindsDescription_IsNumbers`.
- **null-kind**: the empty kind is itself (honest non-null); `[JsonIgnore]` keeps STJ off `type.kind`
  (the wire writes the kind via Write, not STJ reflection) — no cycle.

## The gate (final)

- `DecimalIsANumber`: **green**; prompt C **byte-equal** (PickListTests 8/8).
- C# gate: back to the **8 pre-existing baseline fails, 0 new**. Surface re-pinned (774), diff only the
  kind:type additions, no top-level kind leak.
- plang suite: **253/25/12/3** — better than baseline 250/29/12/3: DecimalIsANumber + 3 strict tests
  fixed, no regressions.

## Cost, summarized

Modest: the hazards were the self-referential empty kind's STJ cycle (JsonIgnore + self), and the
member diamonds — which turned out to matter (the Property fork broke prompt C, fixed by a kind
navigating itself rather than re-resolving to a bare type). Stage 1 closed; the settings ripple that
looked stage-2-shaped was really this navigation fix.
