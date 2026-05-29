## architect — v1 — 2026-05-29
**Target:** /CLAUDE.md
**Why:** This branch renames `app/**` plural/PascalCase folders to singular+lowercase and reshapes `app.X` into a collection-node accessor. The repo CLAUDE.md's Runtime2 Conventions block names several plural namespaces by name (`app.channels.@this.Output`, `Data<app.variables.Variable>`, `app.types.path`, etc.) and the lowercase-vocabulary list enumerates the plural folder names — these become stale references on merge. Also, the accessor convention is new canonical guidance future `app/` work must follow.
**Proposed change:**

Reference-token updates (apply on merge, after the rename lands):
- `app.channels.@this.Output` → `app.channel.@this.Output`
- `Data<app.variables.Variable>` → `Data<app.variable.Variable>`
- `app.types.path...` → `app.type.path...` (the System.IO-ban rule's verb-surface references)
- The lowercase-vocabulary list — update the enumerated folder names to singular: `actor, goals→goal, variables→variable, channels→channel, errors→error, events→event, filesystem, formats→format, keepalive, snapshot, tester, types→type, config, callstack, data`.

New convention to add to the Runtime2 Conventions block:

- **`app.X` is the collection node, not a wrapper.** Each concept `X` exposes its collection at `app.X` (type `X.list.@this`, folder `X/list/this.cs`), owned once by the singleton app (or `actor` for channel). Select with `app.X["name"]`, enumerate with `app.X.list`. A concept that execution flows *through* also has `app.X.current` (e.g. `app.goal.current` reads `CallStack.Current.Action.Step.Goal`); a concept nothing is ever *inside* (type, channel, event, module, format) has no `.current`. There are no "entities vs services" — only collections, some with a current. The collection never lives on the element and is never a flat `App<Plural>` property (the deleted `AppGoals`/`AppChannels`/`AppEvents`/`AppModules` aliases were that smell). **Registry = selection + lifecycle; all behavior lives on the element** — a type-switch (`is X.subtype`) inside a registry is misplaced behavior, push it onto the element as a virtual member. `module` is a no-`.current` service (action modules are dispatched, not navigated): `module/this.cs` = `module.@this` is the action registry reached at `app.module`, with no `app.module.current`.
