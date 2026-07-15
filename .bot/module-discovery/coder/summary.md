# module-discovery — coder summary

## Version
v2 (v1 = comment rounds on the architect's Stage 4 plan; v2 = the spike, the `app.module.action.*` namespace move, and 4a).

## What this is
Stage 4 dissolves the `module.@this` god-object into a collection at `app.module` + element hosts, moving teaching from imperative C# to Fluid templates. This session ran the de-risk spike, an unplanned-but-necessary namespace move it forced, and the 4a structural split.

## What was done (all committed + pushed, green)
1. **5-leg spike** (`HostRenderSpikeTests`) — proved Fluid can render host elements; caught the clr-carrier wall (a native `item.list` of hosts renders empty because Fluid reflects the `clr` carrier, not the POCO).
2. **Fluid `PlangDoorStrategy`** (`app/module/action/ui/code/Fluid.cs`) — member access on any plang item routes through its own `Data.Get` door (resolve via `.Value()`, lower a leaf via `item.Backing`). All 5 legs green. **Spike finding: prose doors must be sync properties** (Fluid can't invoke methods / await Task-properties).
3. **`app.module.action.*` namespace move** — the module concept's `.list` face collided with the `list` action module. Moved all 31 action folders `app/module/<name>/` → `app/module/action/<name>/`; contracts stay at `app.module`. Load-bearing knob flipped in both twins (runtime `Discover` baseNamespace + generator's `Emission/Action` module-name derivation). `.pr` wire unchanged; no new reds.
4. **4a — collection relocation**: god-object → `app.module.list.@this`, freeing the `app.module.@this` slot.
5. **4a — module element** at `app.module.@this` (Name); collection selection `app.module["x"]` → element, `app.module.list` → native list of elements (was Stage-3 name-enumerable / action-type-dict; only caller was one test).
6. **4a — deleted `GetChannelInventory`** (middleman over `actor.Channel`).

## What's next (4a tail, then 4b/4c)
- **Choice-registration fold (delicate):** `RegisterModuleChoiceTypes` (on `type/list`, called `app/this.cs:307`) moves into the collection's population walk, firing on **App-attach** (App isn't set during the ctor `Discover`) + inline on `Register`/`RegisterType` (fixes the latent `code.load` gap where late-registered choice params never register). If wrong, silently breaks the builder's `operator`/`httpmethod` resolution — needs care. Then delete `RegisterModuleChoiceTypes` + its call.
- **4b — `module.Actions`**: native list of `action` class-zoom elements.
- **4c — `action.Properties`** (reflection leaf) + **prose doors as sync properties** (per the spike finding; their value can be a lazy plang item the `Value()` door resolves).
- Then 4d templates + parity gate, 4e repoint+delete `Describe()`/`StepActions`/`getTypes`, 4f/4g.
- **Follow-up (logged):** `NormalizeParameterTypes(Actions)` obpv → `Documentation/Runtime2/obp-cleanup.md`.

## Code example
The Fluid door — the load-bearing fix from the spike:
```csharp
// member access on a plang item goes through its own navigation door, not reflection
var resolved = await (await new Data("", obj, context: context).Get(name)).Value();
return item.@this.Backing(resolved);   // leaf → raw; container/host → item
```
