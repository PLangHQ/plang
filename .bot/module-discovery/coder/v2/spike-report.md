# Stage 4 — 5-leg spike results (branch `module-discovery`)

Spike test: `PLang.Tests/Modules/App/Modules/Stage4Spike/HostRenderSpikeTests.cs`. Real Fluid provider, real `list.where`. Evidence below is captured runtime output, not reasoning.

## Verdict

| Leg | What it proves | Result |
|---|---|---|
| (e) `list.where` `Get(field)` over `clr(action)` + `"in"` | the 6b `where %actions% Name in %...%` filter | **PASS** |
| (a) enumerate host elements in a template | model 5b native-list surface → Fluid | **FAIL** — same root cause |
| (b) Fluid `where:`/`map:` over element props | filterable native list in templates | **FAIL** — same root cause |
| (c) property-row host | rows read in a template | **FAIL** — same root cause |
| (d) async prose doors | least-proven; sync vs Task vs method | **BLOCKED** — masked by the root cause |

## The one root cause (evidenced)

```
[SPIKE-A] CountRaw=2  row0.Peek type=app.type.clr.this
[SPIKE-A] var: vsize=2 vloop=<[][]>     # list enumerates (size 2, loops twice) but {{ m.Name }} empty
[SPIKE-A] poco: name= sync= taskProp= method=   # even a single directly-bound host POCO renders empty
```

**Fluid cannot navigate a `clr` host carrier.** Chain:
1. A native `item.list` wraps every element via `item.@this.Create` → an unknown POCO lifts to `clr` (`item/this.cs:85`).
2. `clr.@this.Peek() => this` (`clr/this.cs:72`) — Peek returns the **carrier**, not the host POCO. (Correct per the host-carrier decision: navigate via `Get`, Peek→self.)
3. Fluid's `NativeListView` yields `element.Peek()` (the carrier) and Fluid reflects it with `UnsafeMemberAccessStrategy` (raw C# reflection). The carrier's C# surface is `Kind`/`Value`/`Context` — **no `Name`/`Actions`** → every member renders empty.
4. Binding a host through a `Data` at all lifts it to `clr` — so even a single directly-bound POCO renders empty.

**Why today's builder works and this doesn't:** `build.actions` returns `clr<StepActions>` whose `.Value` is a plain `IEnumerable<action>`. Fluid iterates that CLR collection **natively** and reflects the raw `action` POCOs directly — it never goes through Peek/carrier. The native-`item.list`-of-elements surface (model 5b) is a different shape and hits the carrier wall.

## The collision this exposes (5b + 6b)

Model 5b says every catalog surface answers the NATIVE `item.list`. Model 6b renders those surfaces (and the `where`-filtered result) through Fluid templates. But a native `item.list` of host elements is **exactly** the shape Fluid can't navigate. And 6b's flow makes it unavoidable: `list.where` returns a native `item.list` of `clr(action)` (leg e proved the filter works) → that filtered list is then rendered → empty. The `where` mechanic passes; feeding its native-list result to a template is the wall.

## Fix options (need your call — this touches production `Fluid.cs`)

- **A — clr→host ValueConverter (smallest).** Add one converter beside the existing dict/list ones: `clr.@this c => c.Value` (hand Fluid the host POCO; it reflects it directly, like it already does for `clr<StepActions>` elements). Fixes a/b/c uniformly and unmasks d. Risk: verify it doesn't disturb `clr<StepActions>`/`formal`-filter paths.
- **B — MemberAccessStrategy routes through the `Get` door.** Fluid navigates ANY plang value via its `Get` (the same door `where` uses), not raw reflection. More general (host, dict, list all navigate identically), bigger change to the render layer.
- **C — template surfaces stay plain `IEnumerable<POCO>` (today's shape).** Contradicts 5b; and the `where`-filtered result is still a native `item.list`, so 6b's filter-then-render path still needs A or B anyway.

My lean: **A** — localized, symmetric with the converters already there, and it matches how Fluid already consumes raw POCOs from `clr<StepActions>`. I'd spike A next to confirm it fixes a/b/c and to finally get leg (d)'s answer.

## Baseline / hygiene
- Spike is a test-only file; no production change yet. No new reds elsewhere.
- The `poco:`/`var:` probes are the evidence; they come out once the direction is fixed.

---

## Option B implemented + measured (branch `module-discovery`)

`Fluid.cs`: member access now routes through a `PlangDoorStrategy` — a plang item navigates by its own `Data.Get` door (the door `list.where`/`condition` use); every other type reflects as before (composed inner `UnsafeMemberAccessStrategy`, since that class is sealed). Async door via `IAsyncMemberAccessor`.

### Results after B
| Leg | Result | Note |
|---|---|---|
| (a) enumerate host elements | **PASS** | `{{ m.Name }}` renders through the door — the carrier wall is gone |
| (d) prose door (sync property) | **PASS** | sync prose reads; async-method/Task-prop forms still to measure |
| (e) `list.where` over clr(action) | **PASS** | unchanged |
| (b) Fluid `where:`/`map:` filters | **FAIL — not a blocker** | Fluid's *built-in* filters don't route member access through the door. **The design doesn't need them**: 6b filters with plang's `list.where` (= leg e, passing), not Fluid `where:`. Iteration (`{% for %}`, leg a) + plang `where` cover the builder path. |
| (c) `{% if p.IsVariable %}` bool | **FAIL — pre-existing scalar gap** | `Peek()` returns the plang **item** for scalars (base `item.Peek() => this`; bool/text/number don't override). Fluid sees a truthy object for a `false` bool, and `where:` compares items not strings. **Not host-specific** — `NativeListView`/`NativeDictView` hand Fluid `.Peek()` items today too; the existing templates just don't exercise bool truthiness. |

### Verdict
**B solves the hard problem** (host navigation in Fluid — legs a/d/e). The two reds are:
- leg (b): out of scope — plang `list.where` is the filter, not Fluid's.
- leg (c): a real but **separate** gap — Fluid needs raw primitives for scalar plang items (bool truthiness, `where:` comparison). Fix candidate: a ValueConverter that unwraps a scalar leaf item to its `.Clr` while leaving containers to their existing view converters. This touches scalar rendering generally (affects native dict/list too), so it's a deliberate call, not a spike hack.

### Open decision
Do we (1) fix leg (c) now with the scalar-unwrap converter (helps all template scalar rendering), or (2) land B as-is for host navigation and file the scalar gap as its own piece? Leg (b) needs nothing — it's not on the design path.

---

## FINAL — all 5 legs GREEN (Option B complete)

Your `.Value()` steer + the existing `item.@this.Backing()` leaf-lowering closed legs (b) and (c) too.

**The door (`Fluid.cs`, `PlangDoorAccessor.GetAsync`):**
```csharp
var resolved = await (await new Data("", obj, context: context).Get(name)).Value();
return item.@this.Backing(resolved);   // leaf → raw Clr; container/host → item
```
- `.Value()` (not `.Peek()`) — resolves references/computed/prose through the real async door.
- `Backing` — a leaf lowers to its raw CLR so Fluid gets a real bool/string/number (`{% if %}`, `where:`, `map:` all work); a container passes through as its item (→ view converter), a host passes through (→ member access re-enters the door). Reuses the single existing owner of that transform — no new fork.
- Sync `IMemberAccessor.Get` throws (templates render via `RenderAsync`), so no sync-over-async.

| Leg | Result |
|---|---|
| (a) enumerate host elements | PASS |
| (b) Fluid `where:`/`map:` over element props | PASS |
| (c) property-row host + `{% if p.IsVariable %}` | PASS |
| (d) prose door | PASS — **must be a sync property**; a method is unreachable, a Task-prop doesn't resolve to its awaited value |
| (e) `list.where` over clr(action) | PASS |

**Regressions: none.** Full-suite diff surfaced 8 apparent new reds (`Foreach_*`, `Set_NameTypedAsText`, `Integration_File*`); all 8 fail identically with my Fluid change **stashed** — pre-existing reds the single-run baseline missed, not mine. (Lesson re-confirmed: a single-run baseline manufactures false new-reds.)

## Consequences for 4a/4c
- Model 5b (native-list-of-host surfaces) is now viable through Fluid — the whole teaching path can consume `app.module`/`module.Actions`/`action.Properties` natives.
- **Prose doors on the element must be sync properties** (loaded/cached at mint), not async methods — leg (d). This pins the 4a/4c element shape (the draft's `async Task<string?> Description()` doors change to sync `string? Description` resolved at population).
- Leg (b) confirms Fluid filters work now too, but 6b still uses plang `list.where` (leg e) for the builder filter — both are available.
