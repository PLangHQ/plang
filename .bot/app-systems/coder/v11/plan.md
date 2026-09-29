# coder v11 — stage 10c: the app's facts are plang values

Contract: `test/plan/app-systems/start.goal` Stage10c, and its goals
`app/AppFactsArePlangValues.test.goal` and `app/setting/AppNameIsItsSetting.test.goal`.
Both goals are unbuilt today. Each slice is proposed to the architect, then committed with its pins.

## What is there (trace)
- `app/this.cs`:
  - `Id`, `Name`, `Created`, `Updated`, `Version`: `[Store]` CLR `string`/`DateTime`.
  - `StartedAt`: `DateTime`. `Uptime`: `TimeSpan`. `Environment`: `string`, set in the ctor.
- `store`: `Task<store.@this>` behind a `Lazy` (`Open()`, in memory in test mode, scoped `system-{Id}`).
  - `store.@this` is an abstract `IDisposable`, not an item.
  - 66 references; production callers are actor, actor.setting, goal.setup, llm OpenAi/TypeSafe and app.
- Identity:
  - `Identity()` parses `.build/app.pr` by hand with `JsonDocument`.
  - `Save()` writes it through the `.pr` format (`clr<app>`'s `[Store]` face), so writing and reading take different roads.
- `app.setting.@this` has one option, `Create`.
- Channel `Direction`/`Buffer`/`Timeout`/`Mime` are CLR (`ChannelDirection`, `long`, `TimeSpan`, `string`). The goal channel lowers its dict settings into them at birth, which its comment calls "the interim boundary".

## Slices
1. **Facts as plang values.** `Created`, `Updated` and `StartedAt` become `datetime`; `Uptime` becomes `duration`. `Save`/`Identity` carry them. Pin: AppFactsArePlangValues' first three asserts.
2. **The store is born ready.** `app.store` is a `store.@this`, not a Task. The store opens itself on first use, behind its own verbs, which are already async. store is a plang type (`%!app.store!type.name%` = store). Every `await app.store` consumer loses one await.
3. **Identity read through the format that writes it.** `Identity()` reads `.build/app.pr` through the `.pr` format's read of the `[Store]` face, the pair of `Save`. No JsonDocument.
4. **id, name, environment are the app's settings.** They move to `app.setting` options. `%!app.name%` answers from the asker's `app.setting`, and `set %!app.setting.name% = "Shop"` is seen.
5. **A channel's settings are plang values.** Direction becomes `choice<ChannelDirection>`, Buffer a number, Mime text. The transport lowers them at its own use; the goal channel's birth stops lowering.

## Questions for the architect (blocking 2, 4, 5)
- (2) Making `store.@this` an item makes `%!app.store%` navigable. Should it answer only its identity (a leaf naming its kind, sqlite/memory), or also its tables?
- (4) The test-mode store is scoped by `App.Id` (`system-{Id}`), and the setting rows live IN the store. If `Id` becomes a setting row, the store needs the Id before it can read the row. Proposal: `id` stays in the app's identity (app.pr) and is exposed read-only through `app.setting`, and only `name` and `environment` are writable settings. Or should the test store scope by another key?
- (4) `%!app.name%` answers from the asker's settings, so it needs a context. Options: a one-context member `Name(context)`, which the catalog lists as a property, or the system actor's settings.
- (5) Timeout is held for Ingi ("channel Timeout"). Should slice 5 leave Timeout as `TimeSpan` and convert only Direction, Buffer and Mime?

## Order
Slice 1 now (no open question), then 3, then 2, 4 and 5 as ruled.
