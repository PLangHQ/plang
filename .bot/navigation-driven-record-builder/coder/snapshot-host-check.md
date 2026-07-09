# Coder check (assigned) — snapshot-as-host: the `SetVariable` arm does NOT just die

Plan (bridge-item audit, `ede498fe0`) says:
> `snapshot = host` … `set %snap.variables.x%` works through the uniform walk
> (`clr.Kind.Navigate` → the Variables collection → its own Set); coder check: the
> captured-variables collection answers a by-name Set … the bespoke `SetVariable` arm
> dies with `SetValueOnObject`.

**The check fails as written.** Converting snapshot to a reflection-host breaks
`%snap.variables.x%` — **read and write both**.

## Why

```
// TODAY — snapshot : item.@this, OVERRIDES Navigate (app/snapshot/this.Variables.cs)
%snap.variables.x%           // snap.Navigate("variables") → pass-through (returns snap)
                             //   → snap.Navigate("x") → GetVariable("x")   [Section-backed]
set %snap.variables.x% = 2   // SetValueOnObject arm → snap.SetVariable("x", 2)
                             //   → mutates the SAME List<data> in the "Variables" Section
                             //     that Restore reads → edit survives resume ✓

// PLAN — snapshot = host, navigated/written by the reflection `*` kind
reflection.Step(obj, "variables")   // = obj.GetType().GetProperty("variables")   (reflection.cs:15)
                                     //   NO `variables` property exists — it's a private
                                     //   VariableList() + Section("Variables").Read(...) → NotFound
```

- The `*` kind's `Step`/`Set` is **pure `GetProperty` reflection** — no hook for a host
  to inject custom navigation.
- Snapshot's `.variables.x` is a **Section lookup**, not a property walk, and it lives in
  a `Navigate` **override** on `item.@this`. Dropping `item` leaves that override homeless;
  `clr<snapshot>` navigation dispatches to the reflection kind, which knows nothing of
  Sections. So the read path breaks too, not only the write arm.

## What the conversion actually needs (option A)

Reify `variables` as a real navigable collection **property** on snapshot:
1. reflection's `GetProperty("variables")` returns it;
2. the collection answers its **own** by-name `Navigate` and by-name `Set`;
3. its `Set` writes **through to the captured `Section`** so `Restore` sees the edit
   (the invariant the check calls out).

`GetVariable`/`SetVariable`/`VariableList` **relocate onto that collection** — clean OBP
(the collection owns its by-name get/set, shape smell #1). The behavior doesn't "die with
`SetValueOnObject`"; it **moves**.

## Ask

The snapshot line is under-scoped: it's "reify variables as a write-through navigable
collection," not "delete one arm." Either (a) accept option A as the snapshot sub-task in
Stage 2, or (b) reconsider snapshot staying a value/item with custom nav (but Ingi settled
host). Flagging before it's coded — the read-path break is the part the check missed.
