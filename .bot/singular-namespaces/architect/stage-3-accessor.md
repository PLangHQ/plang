# Stage 3: Accessor reshape — kill the `App*` wrappers

**Goal:** Turn `app.X` from a wrapper alias into the collection node with verb-less selectors — `app.X["name"]` (select), `app.X.list` (enumerate), `app.X.current` (only where execution has a current), `app.type.of<T>()` — move all per-element behavior off the registries onto the elements, demote the module registry off `app.@this`, delete the four `App*` aliases, and migrate the ~286 call sites.

**Scope.** Included: property renames (`Goals`→`goal`, `Channels`→`channel`, `Events`→`event`, `Modules`→removed, `Types`→`type`, `Formats`→`format`, `Errors`→`error`, `Variables`→`variable`, `Navigators`→`navigator`); the selector surface on each `X.list.@this`; `.current` on `goal` only; the channel I/O relocation onto `channel.@this`; the module demote to `module/registry.cs`; deletion of `AppGoals`/`AppChannels`/`AppEvents`/`AppModules`; the call-site migration. Excluded: the `type.@this` entity promotion (stage 4 — here `app.type[...]` may still return today's shape). The deferred module action-renames stay deferred.

**Deliverables:** each registry reshaped to selection + lifecycle + enumeration (+ `.current` for goal); behavior moved onto elements (channel `Write(data)`/`Read()` polymorphic, no registry type-switch, no `WriteText`); `module/registry.cs` held on an internal field, 6 sites rerouted; the four aliases gone; ~286 sites migrated; index-miss policy wired to a setting with a chosen default; clean rebuild + both suites green.

**Dependencies:** Stage 1 (rename), Stage 2 (non-null — so the accessor never threads `App?.`).

## Design

Full model and rules in `plan/accessor-model.md`. The migration surface by subsystem (from the scan):

| Subsystem | Sites | Shape of the migration |
|---|---|---|
| `type` | 80 | `Get`/`Clr`→`[name]`, `GetTypeName`/`Name`→`[Type].name`, sub-registries (`Spec` 19, `Scheme` 5, `Choices` 2) reached via `app.type.*`. Heaviest; `Spec`/`Entry` reshape is mostly stage 4. |
| `variable` | 63 | `Get`/`Set`/`GetValue`/`Resolve`/`Remove` on `context.variable`. `Get(name)`→`["name"]` where it reads cleaner; `Set` stays a verb (it's a mutation, not a selection). Largest by count — sequence carefully. |
| `channel` | 37 | `WriteTextAsync`/`WriteAsync`→`["name"].Write(data)`; `Register`/`Contains`/`Resolve`/`Get`→registry selection+lifecycle; `Serializers` sub-registry unchanged. The I/O relocation is the OBP win here. |
| `event` | 12 | `Register`/`Unregister`/`GetBindings` — these are lifecycle + query on the binding registry; no `.current`. |
| `goal` | 9 | `Get`→`["name"]`/`[prPath]`; `FirstOrDefault`→`.list` LINQ or `[name]`; the new `.current` replaces hand-rolled callstack digging. |
| `error` | 8 | `Count`/`Push`/`Error`/`Trail`/`RestoreTrail` — registry + trail; keep verbs that are real operations. |
| `module` | 6 | demote: `app.Modules.*` → internal `module/registry.cs` field. |
| `format` | 5 | `Mime`/`KindOf`/`Compressible` — `format` collection; keep as named lookups. |
| `navigator` | 2 | `Get` → `[type]`. |

Two judgment calls the coder owns, guided by the memory rule (`GetX`/`IsX` are smells *only* when they're property-shaped questions; verb methods that do real work are fine):

- **Not every `Get` becomes an indexer.** `Get(name)` that is pure selection → `["name"]`. But `Set`, `Push`, `Register`, `Resolve`, `Mime` do real work or mutate — keep them as verb methods. Don't force `app.variable["x"] = v` if `Set` reads better; the contract is "selection is verb-less," not "delete every verb."
- **Sub-registries (`Serializers`, `Scheme`, `Choices`, `Spec`) keep their surface** — they're nested nodes, not the thing being reshaped. Just reach them through the renamed parent.

**Channel I/O** is the worked example in `plan/accessor-model.md`: the registry's `is channel.stream.@this` type-switch becomes a virtual `Write`/`Read` on `channel.@this` (stream overrides), and the by-name I/O conveniences dissolve into `actor.channel[name].Write(...)`. No `WriteText` — it decomposes `data`. `ReadTextAsync`/`ReadChannelAsync<T>` look dead; confirm and delete.

**Module demote:** `module/registry.cs` is not a `this.cs`, so there's no `module.@this` and no `app.module` nav-entity claim. Held on an internal/private field of `app.@this`; the 6 consumers (`GetCodeGenerated`, `Discover`, `Describe`, `Contains`, `Remove`) reach it through that field. This also kills the `module.list`-vs-`list`-action-module collision.

**Index-miss** is governed by a setting with a chosen default (see `plan/accessor-model.md`) — not a silent noop, not a hard throw baked into the indexer. Wire it with test-designer; the contract is "configurable and defaulted."
