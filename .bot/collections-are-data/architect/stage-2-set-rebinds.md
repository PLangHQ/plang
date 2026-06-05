# Stage 2 — `set` rebinds, not mutates

**Leaf-trace rows:** M (`Variables.Set` raw branches), L (dot-path `SnapshotClone`). **Prerequisite for Stage 3.**

**You own the final shape.** Anchors for the design — change what reads wrong, keep the dispositions.

## The state today

`variable/list/this.cs` has three `Set` branches:
- **Data-value branch** (`:137-191`) — already rebinds: mints/replaces and carries `OnCreate`/`OnChange`/`OnDelete` across by name. **Leave it; it's the template.**
- **raw frame-overlay branch** (`:193-222`, mutates at `:199`: `existingFrame.Value = value`).
- **raw underlying-dict branch** (`:224-241`, mutates at `:227`: `existing.Value = value`).

The two raw branches mutate `Data.Value` in place on a same-type `set`. That's the alias bug: a `Data` stored in a list gets rewritten when the variable is re-set.

## Do

- Make both raw branches **rebind** like the Data-value branch — mint/replace the `Data`, carry the three subscriber delegates across by name. **Don't skip `:199`** — the frame-overlay branch is the one that bites inside channel-fire and parallel-foreach flows; skipping it reintroduces the alias bug there only, which is the worst kind to debug.
- **L** — delete the dot-path `SnapshotClone` use (`:298`). Once `set` rebinds, the dot-path target no longer needs a defensive deep-clone; independence comes from rebinding. (`add.cs:43` already uses `ShallowClone`, not `SnapshotClone` — that deep-clone is already gone. The `SnapshotClone` *definition* at `data/this.cs:1241` can stay until its last use is removed, then delete.)

## Why before Stage 3

Stage 3 makes `add.cs` store the element `Data` by reference (drop `ShallowClone`). That's only safe once `set` stops mutating stored values underfoot. Land the rebind first; the `add.cs` simplification falls out in Stage 3.

```
set %x% = "a"        → Data_A
add %x% to %list%    → list holds Data_A
set %x% = "b"        → mint Data_B; x → Data_B; Data_A untouched
%list[0]%            → "a"
```

## Acceptance

- the snippet above: `%list[0]%` stays `"a"` after `set %x% = "b"`.
- `OnChange` fires on rebind (not on in-place mutation).
- forked-flow / parallel-foreach: a `set` inside a branch doesn't rewrite a value the parent stored.

## Green

Both suites pass. Watch event-subscriber tests — the carry-across is where they break if a branch is missed.
