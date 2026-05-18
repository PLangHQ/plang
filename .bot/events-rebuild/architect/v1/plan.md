# Events Rebuild

## Why

The current event subsystem accreted: an 11-value `EventType` enum, five
heterogeneous match fields on `EventBinding`, a five-segment namespace
chain (`App.Events.Lifecycle.Bindings.Binding.@this`), and five
hand-coded fire sites in the engine that each call
`lifecycle.Before.Run(context, EventType.BeforeXxx)` with slightly
different shapes. The model has three orthogonal concerns tangled
together — *where the binding lives* (scope), *what category of thing
fires* (lifecycle vs channel vs property), and *what the binding
matches against* (goal name, step text, action pattern, channel name).
Adding a new event type today means touching the enum, the binding
record, the matcher, and every fire site.

The redesign unifies it. One `Event.@this` per scope. One `On` enum
that names the category (App / Goal / Step / Action / Read / Write /
Ask / Variable). One uniform fire shape: `event.Before(On on, Data
source)` / `event.After(On on, Data source)`. Variable events fire
universally from `Data.Value`'s getter and setter — no per-class
source-gen, no per-property wrapping. Lifecycle events fire from the
engine using the same surface. Adding a new event category is one
enum value + one fire-site line.

Concurrently, the singular-naming rule is applied **only inside the
event area** (per Ingi 2026-05-12 — broader codebase rename deferred).
`App.Events.@this` becomes `App.Event.@this`, the
Lifecycle/Bindings/Binding folder chain disappears, the binding is a
private record inside `Event.@this` with no public `Binding.@this`
type. PLang surface reads as `%app.event%`, `%context.event%`.

The `scope:goal|app` parameter (originally drafted on the
`events-scope-binary` branch, now absorbed here) is part of this
branch, not a separate one. In the old design it was the smallest
shippable change — one parameter, one branch in the handler, no new
types. In the new design it's a natural consequence of having two
named registries: `Scope` simply selects which `Event.@this` the
binding lands in. The old "release valve" framing is gone; the scope
parameter ships as part of stage 2.

Decisions inherited from the scope-binary plan and preserved here:

- Default scope is `goal` — current behaviour unchanged for any goal
  that doesn't pass `scope`.
- `Actor` + `scope:app` are mutually exclusive (build-time check
  preferred, runtime fallback acceptable).
- Lifetime semantics are implicit in the registry choice:
  `scope:app` bindings live for the App, `scope:goal` bindings live
  for the actor's context. No extra teardown.
- The third tier (`Channel.Events`) was never exposed through the
  parameter and stays internal — in this redesign it doesn't exist
  at all (channels fire, don't store).
- Naming: `scope` (not `level`); values `goal` / `app` in PLang,
  `EventScope.Goal` / `EventScope.App` in C#.

`events-scope-binary` is closed by this branch.
`events-architecture` is closed without merging — its only useful
artefact (an `Actor.Developer` sub-object) is being solved
differently.

Boot-time `Events.goal` loading and `BeforeAppStart`/`AfterAppStart`
firing (Thread 3) is **out of scope** for this branch — same writeup
as in `Documentation/Runtime2/todos.md` from 2026-05-08. It's a
follow-up that becomes trivial once this lands.

## Decision summary

**Storage.** Two scope registries:

- `App.Event.@this` — app-scoped bindings (`scope:app`).
- `Context.Event.@this` (per-actor `Actor.Context.Event.@this`) — actor-scoped bindings (`scope:goal`, default).

`Channel.Event.@this` from today is removed. Channels don't own
bindings; they fire into the current context's event surface.

**Binding shape** (a private record inside `Event.@this`):

```
Binding {
  on:      On                  // App | Goal | Step | Action | Read | Write | Ask | Variable
  name:    string?             // pattern (glob default, regex via flag) — context-appropriate per `on:`
  path:    string?             // pattern (glob default, regex via flag) — for Variable: sub-path
  type:    Phase               // Before | After
  isRegex: bool                // name/path interpreted as regex when true
  priority: int                // higher runs first
  handler: Func<Data, Task>    // what to run when fired
}
```

`name:` semantics per `on:`:
- **App** — null (only one app)
- **Goal** — goal name pattern (today's `goalNamePattern`)
- **Step** — step text pattern (today's `stepPattern`)
- **Action** — action name pattern, e.g. `http.*` (today's `actionPattern`)
- **Variable** — root variable name (e.g. `step`, `goal`)
- **Read/Write/Ask** — typically null; handler reads `Channel.Name` from `source` if it cares
- **Error** — deferred from this branch

`path:` is meaningful only for `Variable` (e.g. `"Text"` for `%step.Text%`). Null otherwise.

**Fire surface.** Uniform, on `Event.@this`:

```csharp
public Task Before(On on, Data source);
public Task After(On on, Data source);
```

After's `source` Data carries any result internally (via `Properties`
or a nested `Data`); no separate `result` parameter. Caller wraps
`this` in `new Data<T>(this)` at fire sites where `this` is not
already a Data.

**Fire sites:**

```csharp
// Engine before/after running a step:
await ctx.event.Before(On.Step, new Data<Step>(this));

// Channel write:
await ctx.event.Before(On.Write, new Data<Channel>(this, payload));

// Data.Value getter (the universal variable hook):
await Context.event.Before(On.Variable, this);
var v = Resolve(value);
await Context.event.After(On.Variable, this);
return v;
```

`Context.event.Before/After` internally consults `App.Event` too —
single call site, two-tier walk.

**PLang surface unchanged in spirit:**

```plang
- on before, on step, call LogStep
- on before, on step (name: "Api/*"), call LogApiSteps
- on get on '%step.Text%' (scope: app), call Logger
- on before, on write, call FormatOutput
```

The LLM-builder translates the natural phrasing into binding records.
For variable events, it pulls `name` (root variable) and `path` (sub-path) from the `%var.subpath%` expression.

**Drops:**

- `EventType` enum — 11+ values gone, replaced by `On` (8 values) + `Phase` (2 values).
- `App.Events.Lifecycle.@this` folder — no per-target Before+After view.
- `App.Events.Lifecycle.Bindings.@this` folder — collapsed.
- `App.Events.Lifecycle.Bindings.Binding.@this` (the `EventBinding` alias) — bindings are private inside `Event.@this`.
- `Channel.Event.@this` — channels fire, don't store.
- All five match fields on EventBinding (`goalNamePattern`, `stepPattern`, `actionPattern`, `channelName`, `isRegex`) collapse to `name` + `path` + `isRegex`.

## Stage index

| Stage | File | Status |
|-------|------|--------|
| 1 | [`Event.@this` rewrite + scope owners + binding shape](../stage-1-event-registry.md) | pending |
| 2 | [On enum + EventType collapse + event.on rebuilt](../stage-2-on-enum-and-eventon.md) | pending |
| 3 | [Migrate engine fire sites (Step/Goal/Action/Channel)](../stage-3-engine-fire-sites.md) | pending |
| 4 | [Data.Value fires variable events](../stage-4-data-value-firing.md) | pending |
| 5 | [Drop Lifecycle/Bindings folders + final cleanup](../stage-5-cleanup.md) | pending |

Stages 1-3 land the core machinery. Stage 4 lights up the variable-event
surface. Stage 5 is the deletion pass — by then nothing depends on the old
shapes.

Each stage is independently buildable and testable. Stage 1 alone
gives a working (if unused) new registry. Stage 2 makes the new
registry the writer target. Stage 3 makes it the reader at fire sites.
Stage 4 extends firing to Data.Value. Stage 5 removes the corpses.

## Topic deep-dives

- [Data.Value firing — where Name and Path get set](data-value-firing.md)
- [Registry internals — indexing, dirty bit fast path](registry-internals.md)
- [On enum semantics — what name/path mean per category](on-enum-semantics.md)

## Key invariants

- **Bindings live in exactly one place.** Either `App.Event` (scope:app) or `Context.Event` (scope:goal). Never duplicated. The choice is fixed at registration time and survives until unregister/teardown.
- **Context.event.Before/After is the unified entry.** No caller walks app+context+channel explicitly. The context's event surface knows to consult app on each fire.
- **Source is always Data.** Every fire site passes Data, never raw objects. Allocation cost (one `new Data<T>(this)` at non-Data fire sites) is accepted; uniformity wins.
- **Data.Name is always set.** No null-check in `Data.Value`. Empty/literal Data either gets a synthetic name or the resolver wraps differently — but inside `.Value`, Name is treated as present.
- **No source generator for events.** The lifecycle facades are hand-coded fire sites in the engine; `Data.Value` is a universal hook; the builder's type catalog (for validating `%step.Text%` paths) is a separate concern handled by the existing generator infrastructure.

## Risks / open notes

- **Data.Name when there's no meaningful variable.** Result Data from action calls, computed Data, etc. Need a convention for what Name to use. Option A: a sentinel like `"_"` or `""` that no binding will match. Option B: the resolver constructs Data with Name even for results (the action name + return-slot index). Lean A — sentinel keeps the firing path cheap and bindings can't match accidentally.

- **Cost of Data.Value firing on every property access.** Two-layer fast path: class-level dirty bit on each Event.@this (`HasAnyBindingsFor(On.Variable)` — single hashset check) + per-(name) cached binding lists with `OnChanged` invalidation. Steady state with zero variable bindings: one bool-equivalent check, return raw value. See [registry-internals.md](registry-internals.md).

- **Pattern matching cost on hot paths.** Bindings indexed primarily by `(on, name)` so `step.Text` accesses don't iterate goal bindings. Path patterns checked only after name match. Glob compiled at registration time, regex compiled lazily.

- **Builder catalog drift.** PLang `event.on` validates `%step.Text%` against a catalog of known core types. Today no such catalog exists in a usable form. Stage 2 includes the catalog mechanism — emit from PLang.Generators alongside action handler scanning. Drift-free, but a new generator output.

- **`Actor` parameter on event.on.** Keeps working in the new shape — selects which actor's `Context.Event.@this` receives the registration when `scope:goal`. Mutually exclusive with `scope:app` (build-time check, same as Thread 1's call).

## Test coverage outline

Per stage; full matrix is sketched in stage files. High-level shape:

1. **Round-trip on each `On` value.** Register a binding, fire matching event from the engine, observe handler ran. One test per `On.*` value.
2. **Scope semantics.** `scope:goal` binding visible from same actor only; `scope:app` binding visible from any actor. Already covered by Thread 1's outline — preserved here.
3. **Pattern matching.** Glob (`Api/*`, `step.*`), regex (with flag), plain name, null pattern (match-all). Two-axis (name + path) for variable events.
4. **Data.Value firing.** Bind to `%step.Text%`, access `%step.Text%` in a step, assert handler ran exactly once on Before and once on After. Multi-segment walks (`%step.Action.Module%`) fire intermediate + leaf bindings independently.
5. **Performance.** Microbench: Data.Value with zero bindings registered vs current main. Acceptance: within 5% of raw property access cost.
6. **Migration parity.** Existing `event.on` PLang tests pass unchanged. The shape moved underneath them.

## What success looks like

- `EventType.cs` is deleted.
- `App/Events/Lifecycle/` folder is deleted.
- `App.Events.@this` is renamed to `App.Event.@this` and rewritten around the new binding shape.
- `event.on` registers bindings of the new shape, supports `scope:goal|app`, accepts `%var.path%` for variable events.
- Step/Goal/Action/Channel fire sites call `ctx.event.Before(On.X, new Data<T>(this))` instead of `lifecycle.Before.Run(...)`.
- `Data.Value` getter and setter fire `On.Variable` events when accessed.
- All existing PLang event tests pass.
- Performance for Data.Value with no bindings registered is within 5% of today's raw access.
