## 2026-05-18 — Absorb scope-binary, close events-architecture

Ingi confirmed the events plan: `events-scope-binary` is rolled into this branch (not a separate ship), `events-architecture` is closed without merging (the `Actor.Developer` sub-object on it is no longer wanted — to be solved differently). Updated `v1/plan.md` to make the scope absorption explicit and to record both closures. No code or stage changes.

## 2026-05-12 — Events rebuild

Full redesign of the event subsystem. Replaces the 11-value `EventType` enum, five-field heterogeneous `EventBinding`, and three-tier registry walk with a flat `On` enum (8 values), unified `Binding` record (private inside `Event.@this`), and two-tier scope walk (App + Context). Singular naming applied **inside the event area only** — `App.Event.@this`, `Context.Event.@this`, no `.Lifecycle.Bindings.Binding.` chain. The broader codebase rename (`Goals/Steps/Actions/Channels` → singular) is filed separately and out of scope.

Variable events become universal via `Data.Value` getter/setter — every named-variable property access fires `On.Variable` Before/After events. Data carries `Name` (root variable) and `Path` (sub-walk). Result/literal Data uses `Name=""` sentinel and skips firing. Lifecycle events fire from engine sites via the same uniform `ctx.event.Before(On.X, Data source)` / `ctx.event.After(...)` surface — no more per-class facades, no `lifecycle.Before.Run(EventType.X)` calls.

`scope:goal|app` parameter from the parked `events-scope-binary` Thread 1 folds in as the writer selector (Context.Event vs App.Event).

Five stages:

| Stage | File | Status |
|-------|------|--------|
| 1 | [Event.@this registry + scope owners + binding shape](stage-1-event-registry.md) | pending |
| 2 | [On enum + EventType collapse + event.on rebuilt](stage-2-on-enum-and-eventon.md) | pending |
| 3 | [Migrate engine fire sites (Step/Goal/Action/Channel)](stage-3-engine-fire-sites.md) | pending |
| 4 | [Data.Value fires variable events](stage-4-data-value-firing.md) | pending |
| 5 | [Drop Lifecycle/Bindings folders + final cleanup](stage-5-cleanup.md) | pending |

Topic deep-dives in `plan/`:
- `data-value-firing.md` — where Name/Path get set, sync-vs-async, why the hook is in .Value
- `registry-internals.md` — storage, indexing, fast-path mask, two-tier walk implementation
- `on-enum-semantics.md` — per-`On` rules for what `name:` and `path:` mean

### Carrying forward from the prior conversation

- `events-scope-binary` branch (Thread 1) is parked with its plan still useful as background, but its Stage 1 (the `scope` parameter alone in old shape) is **subsumed** by this branch's Stage 2.
- Thread 3 (`BeforeAppStart`/`AfterAppStart` firing + `/system/events/Events.goal` loading) is out of scope — see `Documentation/Runtime2/todos.md`. Becomes trivial after this branch lands: one fire site in `App.Start()`, one goal-loader call.
- Broader no-plurals rename across the codebase (Goals/Steps/Actions/Channels) is in `Documentation/Runtime2/todos.md` (2026-05-12 entry: "Drop plurals across the OBP folder structure"). Deferred to its own pass.
- Error category is deferred from this branch. New `On.Error` enum value + one fire site in the error-propagation path when it lands.

### Key design decisions and where they live

- **Singular naming inside event area only:** Ingi 2026-05-12. Not broader codebase. (See "Carrying forward" above.)
- **Universal hook in `Data.Value`, not per-class source-gen:** Ingi 2026-05-12. Data is the centralized choke point — one hook covers all variable events. Lifecycle events fire from engine sites; no IEvent marker, no generated facades.
- **`On` is a closed enum, not an open string:** compile-time checked, additions are explicit. 8 values now, +Error later.
- **Pass `Data` to every fire method, never raw `object`:** uniform contract. `After` takes one Data which carries both source and result (via Properties). Allocation cost at non-Data fire sites accepted.
- **Glob default, regex via flag** on both `name:` and `path:`.
- **Two-tier walk (App + Context) replaces today's three-tier (Channel + Actor + App):** channels don't own bindings, they fire into the current context.

### Next session

Stage 1 is the entry point — net-new code in `PLang/App/Event/` with no coupling to old structures. Coder can start there. The plan calls out where to be careful (sync-vs-async on `.Value`, performance acceptance criteria, catalog mechanism deferrable).
