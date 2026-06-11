# Proposal: the value owns its template-ness, born from the builder, carried in the .pr

**For:** architect. **From:** coder (Ingi's direction, 2026-06-11).

## The smell

Whether a `text` (or container) is a live template — `Template="plang"`, i.e. "has
`%var%` holes the door should render" — is **decided externally, at load, by a
string re-scan**, not owned by the value.

Today:
- `Template` is `init`-only on `item.@this` (`type/item/this.cs:139`) — good, immutable.
- But **nothing on the wire carries it**: a `.pr` value is `{name, type, value}` with
  no `template` field. No built `.pr` contains `template`.
- A runtime **walker** sets it: `Action.StampTemplates() → Data.Authored() →
  StampedForm` (`app/data/this.cs:380–467`) re-scans value strings for `%refs%` and
  stamps `Template="plang"` onto text/list/dict/clr. It runs at the authored seams
  (`goal/list/this.cs:48`, `action/this.FromWire.cs:49`, `GoalCall.cs:315,325`).

So template-ness is re-derived every load by re-scanning strings — decided "somewhere
else," and the value never owns it. The stamp-gate ("authored input stamps, runtime
input doesn't") is enforced by *where the walker runs*, not by the data.

## The proposed model

The **builder** — which compiles the source and authoritatively knows a value has a
`%var%` — decides once and writes it into the `.pr`:

```json
{ "name": "greeting", "type": "text", "value": "Hello %name%", "template": "plang" }
```

The wire reader sets `Template` on the value at construction. The value is born
self-describing. No runtime walker.

- **`.pr` text with `template`** → renders (it IS a template).
- **runtime-input text** (never through the builder) → no `template` → literal.

The stamp-gate becomes intrinsic to the data, not to a code path.

## Why it's right (OBP)

1. **Ownership at the authoritative point.** The builder decides, the value carries it,
   the `.pr` is the contract. No load-time re-derivation, no "did this come from an
   authored seam" gate.
2. **Deletes the fragile walker.** `StampTemplates`/`StampedForm` (~90 lines of
   recursion) goes away. A container needs no template bit of its own — its door
   renders any child that carries one (children own it).
3. **Dissolves a real bug.** See "the regression this fixes" below — the walker
   recursing into a flattened action-template and re-stamping deferred sub-params is
   the cause of two test failures on this branch. Born-from-builder template-ness has
   no walker to over-stamp; deferral holds intrinsically.

## The regression this fixes (concrete)

On `compare-redesign`, the temporary raw-container `Lift` bridge (see
`Documentation/Runtime2/todos.md` "Raw C# container in Data") flattens an
action-template list into a uniform native `dict`/`list`/`text` graph. `StampTemplates`
then recurses straight down and stamps a **deferred** sub-action `%var%` that must stay
raw, and the door renders it. Two tests regress:
`DataWrappedActionList_DoesNotRecurseIntoActions`,
`DataWrappedActionList_SubActionParametersRemainRaw`.

Root: the walker re-decides template-ness by structure-walking. The real pipeline keeps
action templates as `PrAction` (which `StampedForm` doesn't recurse), so deferral holds
by accident of the walker's type-switch. If template-ness rode on each value from the
builder, there'd be no walker and no over-stamp — the deferred sub-params would simply
not carry `template` (the builder didn't set it), regardless of how they're nested.

## What it touches (migration, not a one-liner)

1. **Wire** — add `template` to `Wire.Read`/`Write` (`app/data/Wire.cs`); read it onto
   the value's `Template` at construction.
2. **Builder** — emit `template:"plang"` on any value with a `%var%` hole (the
   `HasTemplateRef`/`RefRegex` detection moves from load to build).
3. **Reader** — set `Template` from the wire field (replaces the `StampTemplates` walk).
4. **Delete** `StampTemplates` / `Authored` / `StampedForm` and their seam calls.
5. **Container door** — render-if-any-child-carries-template (no container-level bit).
6. **Rebuild all `.pr` files** (the builder now writes `template`).

## Open questions for architect

- Container template-ness: derive from children (door checks), or carry a bit per level?
  (Leaning: children own it; container has none.)
- Deferred sub-action params (action templates inside a value): the builder's
  compilation intent decides whether they carry `template`. Confirm the builder is the
  right (only) authority, and that a runtime-added action (`goal.list.Add`) sets it
  explicitly at construction.
- Non-builder authored seams (test fixtures, `FromWire` of hand-authored shapes): they
  now must set `template` explicitly or go through the builder/wire.

Ingi has additional thoughts to bring; this is the coder's framing of the current state
and the target.
