# module-discovery — coder summary

## Version
v2 (v1 = comment rounds on the architect's Stage 4 plan; v2 = the 5-leg spike, 4a's first commit).

## What this is
Stage 4 dissolves `module.@this` (the last god-object) into a collection at `app.module` + element hosts, with teaching moving from C# to Fluid templates. Before the disruptive 4a split, the architect asked for a 5-leg spike to de-risk the two hard mechanics: (1) can Fluid render our host elements from a native `item.list`, and (2) does `list.where` filter `clr(action)` elements. This version is that spike + the fix it forced.

## What was done
**The spike caught a real collision and fixed it.**

- Spike test: `PLang.Tests/Modules/App/Modules/Stage4Spike/HostRenderSpikeTests.cs` — renders real host-element shapes through the real Fluid provider and runs the real `list.where`.
- **Finding:** a native `item.list` of host elements did NOT render — Fluid reflects a `clr` carrier (`Peek()=>this`), which exposes no host members. Today's builder only works because `clr<StepActions>.Value` is a plain `IEnumerable` Fluid iterates natively.
- **Fix (Ingi chose Option B):** `PLang/app/module/ui/code/Fluid.cs` — a `PlangDoorStrategy` routes member access on any plang item through its own `Data.Get` door (the same navigation `list.where`/`condition` use); every other type reflects as before (composed inner `UnsafeMemberAccessStrategy`, which is sealed). The door resolves via `.Value()` then lowers a leaf with the existing `item.@this.Backing()` (leaf → raw CLR; container/host → item). Sync `IMemberAccessor.Get` throws (no sync-over-async).
- **Result: all 5 legs green.** No regressions (the 8 apparent new reds fail identically with the change stashed — pre-existing, single-run-baseline artifacts).

**Key consequence for the 4a build:** element prose must be exposed as a **sync property** (a method is unreachable from Liquid; a Task-property doesn't resolve). The draft's `async Task<string?> Description()` doors change to sync properties (their value may be a lazy plang item the `Value()` door resolves).

## Code example
The door — the whole of Option B:
```csharp
// Fluid.cs — member access on a plang item goes through its own navigation door
public override IMemberAccessor GetAccessor(Type type, string name)
    => typeof(app.type.item.@this).IsAssignableFrom(type) ? _door : _reflection.GetAccessor(type, name);

// PlangDoorAccessor.GetAsync
var resolved = await (await new Data("", obj, context: context).Get(name)).Value();
return item.@this.Backing(resolved);   // leaf → raw; container/host → item
```

## What's next
- 4a proper: the split (`module/this.cs` → `module/list/this.cs`, element at the freed slot, `app.module` collection), shaping prose as sync properties per the spike finding.
- Files touched this version: `PLang/app/module/ui/code/Fluid.cs` (production), `PLang.Tests/Modules/App/Modules/Stage4Spike/HostRenderSpikeTests.cs` (test), `.bot/module-discovery/coder/v2/*` (plan, baseline, spike-report).
