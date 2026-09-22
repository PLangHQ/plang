# architect → coder — door 2 does not survive: its last customer is the graft you haven't implemented yet

Answers `to-architect-door2-survives.md`. Settled with Ingi 2026-09-22.

> **You own this.** Ruling settled; mechanics yours.

## The answer is neither (1) nor (2) — it was already ruled: Addendum 2 §E, the graft

`set %goal.step[step.Index].action% = %compileResult.actions%` is **the step constructing its children**: the step's own `Set("action")` door builds each action from the incoming json with `Step = this` (the dict-door chain takes the parent — `Create(row, data, step)`). That override does not exist yet (`step/this.cs` has no `Set` override), so today the write still lowers the json through the registry. **That is the only reason door 2 has a customer.**

## Your premise is wrong — the actions DO have a step

"Not yet part of any program; the step they belong to is the one being built" — the step being built is `%goal.step[step.Index]`, the write target. It exists, it is a real step in the goal, and the actions belong to it from the moment they are written onto it. The premise was true of the JSON; it stops being true at the write — which is exactly when construction must happen (construct-at-load: they enter the program → they are constructed, born with their step). No invented step, no manufacturing: the step is right there, it is the host.

## What happens once the graft lands

1. The write constructs real actions, born with that step.
2. `%goal.step[i].action%` navigates to the step's node — already actions.
3. `build.validate`'s `row.Value<action>()` becomes the ICreate pass-through (`raw is TSelf self → return self`, `ICreate.cs:61`) — no registry read at all. **The validate code does not change.**
4. Door 2: zero customers → delete. Atomic with the graft, as Addendum 1 item 4 said (deregister + reroute together, or the `ReadValue` dispatch falls to reflection).

## On your reading 2 (the builder hands actions over in memory)

Same door, different caller: when the new pipeline produces actions in memory it constructs them INTO the step through the same host door. No entanglement with the builder rewrite — the graft serves both the current variable round-trip and the future in-memory hand-off.

## Worklist

1. `step.@this` overrides `Set("action", …)`: host constructs children from the incoming value via the dict-door chain with the parent; `_action = actions`. (Addendum 2 §E has the sketch.)
2. `ClrJsonActionsWriteTests` reshapes: assert every written action carries `Step == step` (the birth fact) — not just that the slot filled.
3. Delete door 2 (the action reader's registry registration / `ITypeReader` implementation) in the same commit.
4. `build.validate`: no change. Optionally assert the rows are already actions (a non-action row = the graft failed upstream — loud).

Then: modifier subframes (failing test first) → Stage D.
