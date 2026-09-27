# The app tree and where it disagrees (2026-09-27, at 1388b4254)

The rule: the plang path, the C# namespace + class, and the file path name the same thing. Nodes are lowercase, verbs PascalCase, facts keep their C# names. Every `app.X` is the type X (round 5). No statics, one door per question. Everything below was read at 1388b4254; file:line references are to that commit.

## The tree

### `%!app%`: `app.@this`, `app/this.cs`

| plang | C# member → class | file | agrees? |
|---|---|---|---|
| `id`, `name`, `created`, `updated`, `version` | `Id`, `Name` string; `Created`, `Updated` DateTime; `Version` string (`[Store]`, read from `.build/app.pr`) | `this.cs:40-64` | facts; see 11, 12 |
| `absolutepath`, `osdirectory`, `osabsolutepath` | strings (CLAUDE.md exempts these anchors) | `this.cs:69,76,96` | see 10 |
| `parent` | `app.@this?` | `this.cs:85` | yes |
| `environment`, `culture`, `startedat`, `uptime`, `shutdowntoken` | string, CultureInfo, DateTime, TimeSpan, CancellationToken | `this.cs:102-123` | see 12 |
| `mode` | `choice<app.Mode>` | `this.cs:198`, `Mode.cs` | yes |
| `setting` | `ISetting<app.setting.@this>` (only `Create`) | `setting/this.cs` | yes; see 11 |
| `type` | `type<type, type.list>` | `type/this.cs`, `type/list/` | yes |
| `goal` | `type<goal, goal.list>` | `goal/`, `goal/list/` | yes |
| `module` | `type<module, module.list>` | `module/`, `module/list/` | yes |
| `test` | `type<test, test.list>` | `test/`, `test/list/` | yes |
| `actor` | `type<actor, actor.list>` | `actor/`, `actor/list/` | yes |
| `variable` | `type<variable, list<variable>>` | `type/item/variable/` | see 7 |
| `system`, `user` | `actor.list.System` / `.User` | `this.cs:228,233` | see 5 |
| `code` | `AppCode` = `app.module.action.code.@this` | `module/action/code/this.cs` | **no**, 1 |
| `cache` | `ICache`, default `app.module.action.cache.Memory` | `module/action/cache/` | **no**, 1 |
| `debug` | `Debug` = `app.module.action.debug.@this` | `module/action/debug/this.cs` | **no**, 1 |
| `build` | `app.module.action.build.@this` | `module/action/build/this.cs` | **no**, 1 |
| `statics` | `AppStatics` = `app.Statics.@this` | `Statics/this.cs` | **no**, 2 |
| `services` | `app.service.list.@this` | `service/list/this.cs` | **no**, 3 |
| `keepalive` | `app.keepalive.@this` | `keepalive/this.cs` | case only |
| `store` | `Task<app.store.@this>` | `store/this.cs` | **no**, 4 |
| (C# only) `step`, `action`, `channel` | `internal type` entries | `this.cs:219-221` | planned to go (stage 10) |

### Under each type

| plang | C# | file | agrees? |
|---|---|---|---|
| `app.type.list` | `type.list.@this`: `Kind(…)`, `Mime`, `Extension`, `[name]`, `Assemblies`, `Sealed`, `Reserved` | `type/list/this.cs`, `Registry.cs` | yes |
| `app.type.list.reader` / `.renderer` | `app.type.reader.@this` / `app.type.renderer.@this` | `type/reader/`, `type/renderer/` | **no**, 6; renderer is dead, 14 |
| `app.type.<t>` | a `type.@this`: `name`, `namespace`, `kind`, `format`, `strict`, `template`, `alias`, … | `type/this.cs` | see 8 |
| `app.type.<t>.format.list` | `type.format` → `app.type.format.@this` | `type/format/this.cs` | yes, but see 9 |
| `app.goal.list.setup` | `app.goal.setup.@this` | `goal/setup/this.cs` | **no**, 6 |
| `app.goal.list.setting` | `ISetting<goal.list.setting>` | `goal/list/setting/` | yes |
| `app.module.<m>.action` / `.modifier` | `module.Action` / `.Modifier` (list views) | `module/this.cs:93,97` | yes |
| a module's action class | `app.module.action.<m>.<a>` | `module/action/<m>/<a>.cs` | **no**, 6 (already stage 9) |
| `app.module.list.app` | `module.list.App` (public back-reference) | `module/list/this.cs:21` | see 13 |
| `app.test.setting` | `IConcept<test.setting>` | `test/setting/` | yes |
| `app.test.list.report` | `app.test.report.@this` | `test/report/` | **no**, 6 |
| `app.test.list.session` | `app.channel.type.test.@this` (a channel) | `channel/type/test/` | **no**, 6 |
| `app.actor.list.system` / `.user` | `actor.list.System` / `.User` | `actor/list/this.cs:20,23` | see 5 |
| `app.actor.<a>.callstack` | `app.callstack.@this` | `callstack/` | **no**, 6 |
| `app.actor.<a>.channel` | `app.channel.list.@this` | `channel/list/` | **no**, 6 |
| `app.actor.<a>.identity` | `app.module.action.identity.Identity` | `module/action/identity/type/identity.cs` | **no**, 6 |
| `app.actor.<a>.setting`, `.permission`, `.context` | `app.actor.setting`, `.permission`, `.context` | `actor/setting/`, … | yes |
| `app.actor.<a>.app`, `.cancellationtoken` | back-reference; CancellationToken | `actor/this.cs:68,75` | see 12, 13 |
| `app.variable.list` | the memory's `.list`, born on each read | `type/item/variable/list/this.cs:342` | **no**, 7 |

## Where it disagrees

**Nodes that aren't their type** (round 5: every `app.X` is the type X):

1. **`code`, `cache`, `debug`, `build` live in their module's action folder.** `%!app.code%` is `app.module.action.code.@this` in `module/action/code/this.cs`, so the three paths are `app.code` / `app.module.action.code` / `module/action/code`. The same holds for debug and build. Cache is typed as the interface `ICache`, and its class is `Memory` (not `@this`). Debug also holds its setting as a stored copy (`debug.Setting`, set by `app.Refresh`, `this.cs:408-409`), which is the planned `on.set.after` binding.
2. **`statics`** is `app.Statics.@this`: a PascalCase folder, a flat alias (`AppStatics`), and `GetBag(key)` (verb+noun) handing out a public `ConcurrentDictionary<string, object?>` (naked collection). The code's own TODO says to replace it.
3. **`services`**: plural. `service.list` is a hand-rolled `IEnumerable` over a dictionary with `New(parent)`, not a `type<service>`. A service also has its own `Channels` list (plural, beside the actor's `Channel`), and `service.Identity => Parent.App.System.Identity` (`service/this.cs:31`) walks to the system's identity whatever the parent is (a middleman).
4. **`store` is a `Task<store>`.** Navigation awaits a method's result (`variable/code/Method.cs:84`) but not a property's, so `%!app.store%` reaches a Task.
5. **Three doors to one actor:** `%!app.system%` (`this.cs:228`), `%!app.actor.list.system%` (`actor/list/this.cs:20`) and `%!app.actor.system%` (`Match` by name, `actor/this.cs:19`). The same for user.

**Paths that disagree:**

6. **A sub-node whose class lives elsewhere:**
   - `app.goal.list.setup` → `app.goal.setup`;
   - `app.test.list.report` → `app.test.report`;
   - `app.type.list.reader` / `.renderer` → `app.type.reader` / `.renderer` (the namespace drops `.list`);
   - `app.test.list.session` is a channel (`app.channel.type.test`);
   - the actor's `callstack` and `channel` live at `app.callstack` and `app.channel`, not under `app.actor`;
   - `identity` is class `Identity` in namespace `app.module.action.identity`, in the file `…/identity/type/identity.cs`, where all three names differ;
   - a module's actions are `app.module.action.<m>.<a>` in C# but `app.module.<m>.action` in plang (already on the stage 9 worklist).
7. **`variable/list/this.cs` is the memory, not the list.** It's a `ConcurrentDictionary` of Data by name, and plang's `%!app.variable.list%` is its `.list` property, a new `list<variable>` built on every read (`:342`). So the C# path says `variable.list.list`. Also, `variable.List(app)` throws `InvalidOperationException` (`type/item/variable/this.cs:27`).
8. **Types live under five roots, and `%!app.type.<t>%` reaches them all:**
   - `app.type.item.*` (26: text, number, …);
   - `app.type.*` (code, clr, table, which are items too);
   - `app.*` (goal, step, action, module, test, actor, channel, error, mock, snapshot, mode, trigger);
   - `app.module.action.*` (identity, hash, httpmethod, llmmessage, ask, level).
   
   `%!app.type.text%` is `app.type.item.text`, but `%!app.type.code%` is `app.type.code`. Items are split between two depths.
9. **`app.type.format` means two things.** The C# namespace holds the writers, readers and filters (decision 128). The plang path `%!app.type.format%` is the type named `type`'s own formats (`type.format`, `type/this.cs:116`). Separately, the word `format` is also declared by test's report enum (`[PlangType("format")]`, `test/Format.cs:4`).

**Stored twice:**

10. **Two members for the os folder.** `OsDirectory` is set to `OsAbsolutePath` by the CLI (`Executor.cs:45`) and copied from the parent (`this.cs:263`). Only tests make them differ.
11. **The app's identity facts.** The plan (Settings) puts `id`, `name`, `create` and `environment` in `app/setting/this.cs`, which has only `Create`. Also, `Save` writes the `[Store]` face through the format (`this.cs:447`), but `Identity()` reads it back by hand with `JsonDocument` (`this.cs:413-437`): the write and the read are two different mechanisms.

**CLR leaves and back-references:**

12. **CLR leaves plang navigates:** `Created`, `Updated` and `StartedAt` (DateTime → datetime), `Uptime` (TimeSpan → duration), `Culture` (CultureInfo), `ShutdownToken` and `actor.CancellationToken` (CancellationToken, plumbing plang can reach), and `Environment` and `Id` (string → text). The path strings are exempt.
13. **Public back-references plang can walk around:** `%!app.system.app%`, `%!app.module.list.app%`.

**Dead weight:**

14. **The renderer table is dead.**
    - `renderer.Of` has no caller.
    - `json.Writer` keeps the table and never reads it (`type/format/json/writer.cs:19,28`).
    - `Has` has one use, `Registry.cs:189`, which refuses a DLL type that ships no renderer. So it demands a renderer that nothing calls.
    - 15 static `Write` classes in `serializer/` folders are reached only through `Of`, for example `number/serializer/Default.cs`, `image/serializer/protobuf.cs` and `url/serializer/Default.cs`.
    
    The `serializer/` folders (34) keep the old word. The static `Read` classes and `Reader.cs` there are live (the reader registry).

## Already planned vs new

- **Already planned:**
  - item 6 (module actions: stage 9);
  - the internal `step/action/channel` fields (stage 10);
  - debug's stored setting (the `on.set.after` binding);
  - item 11's `id/name/environment` (Settings section, not yet done).
- **New:**
  - 1–5, 6 (the rest), 7, 8, 9, 10, 11 (the read/write asymmetry), 12, 13, 14.
