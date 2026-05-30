# Stage 3: Accessor reshape — kill the `App*` wrappers

> Code/paths here are suggestions that pin the shape — you own the final form. See "You own this" in `plan.md`.

**Goal:** Turn `app.X` from a wrapper alias into the collection node with verb-less selectors — `app.X["name"]` (select), `app.X.list` (enumerate), `app.X.current` (only where execution has a current), `app.type.of<T>()` — move all per-element behavior off the registries onto the elements, delete the four `App*` aliases, and migrate the ~286 call sites.

**Scope.** Included: property renames (`Goals`→`goal`, `Channels`→`channel`, `Events`→`event`, `Modules`→`module`, `Types`→`type`, `Formats`→`format`, `Errors`→`error`, `Variables`→`variable`, `Navigators`→`navigator`); the selector surface on each `X.list.@this`; `.current` on `goal` only; the channel I/O relocation onto `channel.@this`; deletion of `AppGoals`/`AppChannels`/`AppEvents`/`AppModules`; the call-site migration. **`app.type[name]` returns the type entity** (Ingi), not a bare `System.Type` — the entity is `app.data.type` at this point; Stage 4 relocates it to `type.@this` (the indexer's return type follows, same entity). Excluded: the entity *move* + Entry fold + registry/builder reshape (all Stage 4). The deferred module action-renames stay deferred.

**Deliverables:** each registry reshaped to selection + lifecycle + enumeration (+ `.current` for goal); behavior moved onto elements (channel `Write(data)`/`Read()` polymorphic, no registry type-switch); `app.type[name]` returns the type entity; `module` reachable as `app.module` (`app.module["file"]`, `app.module.list`), no `.current`, the 6 ops kept as methods; the four aliases gone; ~286 sites migrated; index-miss throws a typed error (hard, uniform); clean rebuild + both suites green.

**Dependencies:** Stage 1 (rename), Stage 2 (non-null — so the accessor never threads `App?.`).

## Design

Full model and rules in `plan/accessor-model.md`. The migration surface by subsystem (from the scan):

| Subsystem | Sites | Shape of the migration |
|---|---|---|
| `type` | ~30 + 12 | `App.Types.*` ~30 sites: `Get`/`Clr`→`[name]` (returns the entity), `GetTypeName`/`Name`→`[Type].Name`, `GetValidValues`→`[t].ValidValues`; child registries (`Scheme`, `Choices`, `Kinds`, `Renderers`) reached via `app.type.*`; `IsClrTypeName` stays a registry query. Plus the 12 `builder.Types.Entry` sites — those fold onto the entity in **Stage 4**, not here. |
| `variable` | 63 | `Get`/`Set`/`GetValue`/`Resolve`/`Remove` on `context.variable`. `Get(name)`→`["name"]` where it reads cleaner; `Set` stays a verb (mutation, not selection). Largest by count. |
| `channel` | 37 | `WriteTextAsync`/`WriteAsync`→`["name"].Write(data)`; `Register`/`Contains`/`Resolve`/`Get`→registry selection+lifecycle; `Serializers` sub-registry unchanged. |
| `event` | 12 | `Register`/`Unregister`/`GetBindings` — lifecycle + query on the binding registry; no `.current`. |
| `goal` | 9 | `Get`→`["name"]`/`[prPath]`; `FirstOrDefault`→`.list` LINQ or `[name]`; new `.current` replaces hand-rolled callstack digging. |
| `error` | 8 | `Count`/`Push`/`Error`/`Trail`/`RestoreTrail` — registry + trail; keep verbs that are real operations. |
| `module` | 6 | `App.Modules`→`app.module`; the 6 ops (`GetCodeGenerated`, `Discover`, `Describe`, `Contains`, `Remove`) stay as methods on `module.@this`. No demote, no `.current`. |
| `format` | 5 | `Mime`/`KindOf`/`Compressible` — `format` collection; keep as named lookups. |
| `navigator` | 2 | `Get` → `[type]`. |

Two judgment calls you own (guided by the memory rule — `GetX`/`IsX` are smells *only* when they're property-shaped questions; verb methods that do real work are fine):

- **Not every `Get` becomes an indexer.** Pure selection → `["name"]`. But `Set`, `Push`, `Register`, `Resolve`, `Mime` do real work or mutate — keep them as verb methods. Don't force `app.variable["x"] = v` if `Set` reads better; the contract is "selection is verb-less," not "delete every verb."
- **Sub-registries (`Serializers`, `Scheme`, `Choices`, `Spec`) keep their surface** — reach them through the renamed parent.

**Channel I/O** is the worked example in `plan/accessor-model.md`: the registry's `is channel.stream.@this` type-switch becomes a virtual `Write`/`Read` on `channel.@this` (stream overrides), and the by-name I/O conveniences dissolve into `actor.channel[name].Write(...)`. **No `WriteText`** — it decomposes `data`. `ReadTextAsync`/`ReadChannelAsync<T>` look dead; confirm and delete.

**Module** is a no-`.current` service, not an exception: `module/this.cs` = `module.@this` is the action registry, reached at `app.module`. `app.module["file"]` selects, `app.module.list` enumerates. The `.list`-vs-`list`-action collision isn't real (member vs indexer key). Earlier draft demoted it — dropped.

**Index-miss throws a typed error** (Ingi) — uniform across every collection, no setting, no silent noop, no create-on-demand. `app.X["nope"]` selecting an absent name is a bug at the call site, surfaced immediately. The indexer throws directly (no `data.Fail` carrier needed). Distinguish from `app.goal.current` being null at rest, which is a legitimate state. Coder picks the exact error shape with test-designer. See `plan/accessor-model.md`.
