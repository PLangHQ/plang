# Stage 5: Cleanup — drop old shapes

**Goal:** Delete the obsolete event types, folders, and shims. The codebase is on the new shape end-to-end.

**Scope:**
- Delete `PLang/App/Events/Lifecycle/` folder (including `Bindings/`, `Binding/` subfolders).
- Delete `PLang/App/Events/EventType.cs`.
- Delete `PLang/App/Events/this.cs` (old registry) and the folder if empty.
- Delete the migration shim from Stage 3 (`Lifecycle.@this` and `Bindings.@this` thin wrappers).
- Remove the old `Actor.Context.@this.Events` property (replaced by `Event`).
- Remove the old `App.@this.Events` property.
- Remove the `EventBinding` alias from GlobalUsings.
- Remove the bridge in `event.on` (writing to both old + new registries) — Stage 2 acceptance criterion.
- Sweep for stale `using App.Events;` imports referencing types that no longer exist.

**Out of scope:** Nothing functional changes in Stage 5. It's deletion only.

**Deliverables:**
- Folders deleted (per [stage-1-event-registry.md](stage-1-event-registry.md) for new locations):
  - `PLang/App/Events/Lifecycle/`
  - `PLang/App/Events/Lifecycle/Bindings/`
  - `PLang/App/Events/Lifecycle/Bindings/Binding/`
  - `PLang/App/Channels/Channel/Events/` (channels no longer own bindings)
- Files deleted:
  - `PLang/App/Events/EventType.cs`
  - `PLang/App/Events/this.cs` (the old registry)
- Updated:
  - `PLang/App/GlobalUsings.cs` — remove `AppEvents`, `Lifecycle`, `Bindings`, `EventBinding` aliases.
  - Any `using App.Events;` directives → `using App.Event;` if they still need anything.
- Tests:
  - Full test suite passes (C# + PLang).
  - No compiler warnings about unused imports or unresolved symbols.

**Dependencies:** Stages 1-4 all complete. This is purely a deletion pass.

## Design

### The deletion order

1. **Remove the bridge in `event.on`.** It writes to both old and new registries; the old one is about to disappear. Sanity check by running the full test suite — should still pass because Stage 3 made fire sites read the new registry.
2. **Delete the migration shim** (`Lifecycle.@this.Before.Run(...)` redirecting to `ctx.event.Before`). Anyone still calling the shim is a stale code path — coder fixes those call sites to use the new API directly.
3. **Delete the old registry** `App.Events.this.cs`. Compile errors highlight any remaining old-shape references.
4. **Delete `EventType.cs`** and the `Lifecycle/Bindings/Binding/` folder chain.
5. **Update GlobalUsings.cs** — remove obsolete aliases.
6. **Sweep** — `grep -r "App.Events" PLang/` should show no live references. Same for `EventType`, `EventBinding`, `Lifecycle.@this`, `Bindings.@this`.

### Renames not deletions

Some folder names move from plural to singular (per Ingi 2026-05-12 — singular naming inside event area only):
- `PLang/App/Events/` → `PLang/App/Event/` — done in Stage 1 (new folder created there).
- `PLang/App/Actor/Context/Events/` → `PLang/App/Actor/Context/Event/` — done in Stage 1.

Stage 5 just removes the old paths (which are now empty or only contain Lifecycle subfolders, which we're deleting).

### What about `App.Events.@this` having a property `Events` and the alias `AppEvents`?

`AppEvents = App.Events.@this` is a global alias used by `App.Goal.Step.Action.Modifiers` and others. After Stage 3, all such references are migrated to `Event` / `App.Event.@this`. Stage 5 removes the alias from GlobalUsings.

### Verification checklist before declaring Stage 5 done

- [ ] `grep -r "EventType" PLang/` → no hits.
- [ ] `grep -r "App.Events" PLang/` → no hits.
- [ ] `grep -r "Lifecycle.@this" PLang/` → no hits.
- [ ] `grep -r "Bindings.@this" PLang/ --exclude-dir=Event` → no hits (the new private Binding record may live under Event/).
- [ ] `grep -r "EventBinding" PLang/` → no hits.
- [ ] Full C# build clean — no warnings about unused usings.
- [ ] All C# tests pass.
- [ ] All PLang tests pass (cd Tests && plang --test).

### What NOT to do in this stage

- Don't touch the `On` enum, `Phase` enum, or `Event.@this` class. They're the destination, not the source of deletions.
- Don't widen scope. If a test reveals an issue, it goes to a fixup commit in Stage 5 or back to the relevant stage — don't extend Stage 5 into refactoring.
- Don't migrate anything else in the codebase. The non-events areas stay plural (`Goals`, `Steps`, `Actions`, `Channels`). That's Ingi's broader rename, parked separately.
