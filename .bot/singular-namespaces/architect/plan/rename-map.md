# Rename map (reference)

The complete inventory. Verified against the live tree; two corrections to the original brief are called out first because the coder must trust this map over the brief.

## Corrections to the brief

1. **`Attributes/`, `Diagnostics/`, `Statics/`, `Utils/` stay PascalCase.** They are C# infrastructure, not PLang domain concepts. `/CLAUDE.md` is explicit: "`app/` is lowercase for PLang vocabulary … and PascalCase for C# infrastructure (`Attributes`, `Diagnostics`, `Services`, `Statics`, `Utils`)." The original brief listed only `Services/Service` for lowercasing (§3.3) — that one *is* in scope; the other four are **not**. (Note the tension: CLAUDE.md groups `Services` with the PascalCase infra, but the brief explicitly lowercases `Services/Service` → `service`. Following the brief. If Ingi wants `Services` to stay PascalCase too, drop that one row.)
2. **`serializers/` collapses to singular, and `channels/channel/events/` → `channel/event/`.** The rename rule is singular throughout; an early scan left them plural. `serializers` (registry over `serializer`) collapses into `serializer/`; nested `filters/` → `filter/` by the same rule. Coder confirms leaf contents before moving.

The crossing rule that makes the whole thing recoverable: aliases shield consumers, so rename **one subsystem at a time** — move folder, fix its `namespace`, fix that alias's RHS in both `GlobalUsings.cs`, fix direct `global::app.<old>` refs, fix generator strings for that namespace, rebuild green, next subsystem.

## Folder transforms

| Current | Target | Kind |
|---|---|---|
| `app/goals` + `app/goals/goal` | `app/goal` | collapse (registry→`goal/list/`, element→`goal/`) |
| `app/goals/goal/steps` … `/step/actions/action/modifiers` | `app/goal/steps` … (drop the `goals/goal` prefix) | collapse (ride along) |
| `app/goals/setup` | `app/goal/setup` | collapse |
| `app/channels` + `app/channels/channel` | `app/channel` | collapse |
| `app/channels/channel/{goal,message,noop,session,stream}` | `app/channel/{goal,message,noop,session,stream}` | collapse |
| `app/channels/channel/events` | `app/channel/event` | collapse + singular |
| `app/channels/serializers` + `…/serializers/serializer` | `app/channel/serializer` | collapse |
| `app/channels/serializers/filters` | `app/channel/serializer/filter` | collapse + singular |
| `app/channels/serializers/{json,serializer/plang}` | `app/channel/serializer/{json,plang}` | collapse (coder confirms leaf shape) |
| `app/events` (+ `/lifecycle`, `/lifecycle/bindings`, `/lifecycle/bindings/binding`) | `app/event` (+ ride-along) | rename |
| `app/errors` + `app/errors/trail` | `app/error` + `app/error/trail` | rename |
| `app/formats` | `app/format` | rename |
| `app/variables` (+ `/calls`, `/calls/call`, `/navigators`) | `app/variable` (+ ride-along) | rename |
| `app/types` (+ `/choices`) | `app/type` (+ `/choice`) | rename (path subtree `type/path/**` unchanged below `type/`) |
| `app/modules` | `app/module` | module-special (see below) |
| `app/builder/Types` (+ `/Spec`) | `app/builder/type` (+ collapse `Spec`'s 2 records into `builder/type/`) | pascalcase |
| `app/tester/Test` | `app/tester/test` | pascalcase |
| `app/http/Response` | `app/http/response` | pascalcase |
| `app/mock/Mock` | `app/mock` (collapse inner `Mock/this.cs` up) | pascalcase + collapse |
| `app/Services/Service` | `app/service` | pascalcase + collapse (per brief; see correction #1) |
| `app/callstack/call/children` | `app/callstack/call/child` | singular (only `list/this.cs` inside; no `child` entity — items are `call.@this`) |
| `app/callstack/call/diffs` | `app/callstack/call/diff` | singular (`Diff` record → `diff/this.cs`) |
| `app/callstack/call/errors` (ns PascalCase `Errors`) | `app/callstack/call/error` | singular + fix PascalCase ns |
| `app/callstack/call/tags` | `app/callstack/call/tag` | singular |

**Stays:** `actor`, `actor/context`, `actor/context/trace`, `actor/permission`, `callstack`, `callstack/audit`, `callstack/call`, `config`, `data`, `data/code`, `keepalive`, `snapshot`, `tester`, `type/path/**`, individual action-module folders, `*/code/` folders, `modules/builder/warning`. Plus the PascalCase infra in correction #1. `filesystem/Default/` keyword carve-out stays.

## Registry file moves

| From | To |
|---|---|
| `goals/this.cs` | `goal/list/this.cs` |
| `channels/this.cs` | `channel/list/this.cs` |
| `channels/serializers/this.cs` | `channel/serializer/list/this.cs` |
| `events/this.cs` | `event/list/this.cs` |
| `errors/this.cs` | `error/list/this.cs` |
| `formats/this.cs` | `format/list/this.cs` |
| `variables/this.cs` | `variable/list/this.cs` |
| `variables/navigators/this.cs` | `variable/navigator/list/this.cs` |
| `types/choices/this.cs` | `type/choice/list/this.cs` |
| `modules/this.cs` | `module/this.cs` (= `module.@this`, the action registry; stays on the public surface as `app.module`) |

`types/this.cs` itself stays `type/this.cs` (the registry is the concept node; see `type-entity.md` for its promotion).

## Module

- `modules/` → `module/`; action-module subfolders keep their names (`module/file`, `module/llm`, `module/loop`, …).
- `modules/this.cs` (flat action registry) → `module/this.cs` = `module.@this`, kept on `app.@this`'s public surface as `app.module` (the earlier demote to `module/registry.cs` was dropped). The 6 operations (`GetCodeGenerated`, `Discover`, `Describe`, `Contains`, `Remove`) stay as methods on it. No `app.module.current` — see `accessor-model.md`.
- `modules/*/types.cs` (per-module return-shape record) → `module/*/type/` (singular folder, record name unchanged): `loop`, `list`, `math`, `output`, `identity`, `http`, `settings`, `module`, `builder`.
- `app.modules.@event` (keyword-escaped) → `app.module.@event` (`@` stays).
- Deferred (do not bundle): folding `modules/module` into `module/environment`; `app.run`→`environment.run`; `builder.app`→`builder.load`.

## GlobalUsings — two files

**`PLang/app/GlobalUsings.cs`** — RHS updates: `AppGoals`, `GoalCall`, `GoalSteps`, `Step`, `ErrorOrder`, `CacheSettings`, `StepActions`, `ActionModifiers` (all `app.goals…`→`app.goal…`); `AppEvents`, `Lifecycle`, `Bindings` (`app.events…`→`app.event…`); `AppModules`, `AppCode`, `ICache`, `Debugging` (`app.modules…`→`app.module…`); `AppChannels`, `Channel`, `ChannelDirection`, `Serializers`, `SerializeOptions`, `DeserializeOptions` (`app.channels…`→`app.channel…`); `Variables` (`app.variables`→`app.variable`); `AppStatics` (`app.Statics`→`app.statics` — wait: `Statics` stays PascalCase per correction #1, so this alias's RHS does **not** change). When the accessor reshape lands, the four wrapper aliases `AppGoals`/`AppChannels`/`AppEvents`/`AppModules` are *deleted*, not just retargeted.

**`PLang.Tests/GlobalUsings.cs`** — ~28 aliases (note: uses older `Engine*` prefix). Same RHS updates: `EngineGoals`, `GoalCall`, `GoalSteps`, `Step`, `ErrorOrder`, `CacheSettings`, `StepActions`, `PrAction`, `ActionModifiers`, `Goal`, `Visibility` (goals→goal); `EventType`, `EngineEvents`, `EventBinding`, `Lifecycle`, `Bindings` (events→event); `EngineModules`, `ICache`, `Debugging` (modules→module); `EngineChannels`, `Channel`, `StreamChannel`, `ChannelDirection`, `Serializers`, `SerializeOptions`, `DeserializeOptions` (channels→channel); `Variables` (variables→variable); `FileSystem`, `path`, `filepath`, `httppath`, `EngineTypes` (types→type).

## Generator strings (critical — compiler will not catch these)

`PLang.Generators/` — these are string literals and emitted-code templates; a miss compiles fine in the generator and breaks at *consumer* compile time. Verify with a clean rebuild.

| File | What |
|---|---|
| `Emission/Action/this.cs:177` | `const string prefix = "app.modules."` → `"app.module."` (extracts module name from namespace) |
| `Emission/Action/this.cs` (templates) | emitted namespace literals: `app.modules.ICodeGenerated`/`IClass`, `app.channels.channel.@this`, `app.goals.goal.steps.step.actions.action.@this`, `app.goals.goal.steps.step.@this`, `app.errors.IError`/`ServiceError`/`ParamSnapshot`, `app.callstack.call.@this`. `app.data`, `app.actor.context` unchanged. |
| `Discovery/this.cs:59,65,155,175` | string predicates `"app.modules"` → `"app.module"`. `"app.data"` unchanged. Comment at `:41` (`app.variables.Variable`). |
| `Emission/Property/Data/this.cs` | emitted `app.errors.ParamSnapshot` → `app.error.ParamSnapshot` |
| `Diagnostics/Plng002.cs:45-46` | message text `app.types.path` → `app.type.path` |

## Test mirrors + docs

- **`PLang.Tests/App/**`** — PascalCase mirror folders stay PascalCase (per `/CLAUDE.md` test-alias-clash rule); only inner namespace/type refs update. Mirrors touching renames: `GoalsTests`/`Goals`, `VariablesTests`, `Errors`, `ChannelsTests`, plus scattered `global::app.<old>` instantiations (e.g. `app.modules.list.types.list`, `app.formats.@this`, `app.goals.goal.steps.@this`). The folder *names* need no change; the references inside do.
- **`Documentation/**`** — reference-only updates (no content rewrites per brief §7): `good_to_know.md`, `architecture.md`, `snapshots.md`, `io-channels.md`, `variables.md`, `action-catalog.md`, `code-vs-goals.md`, `Runtime2/todos.md`. Update renamed namespace tokens.
- **`/CLAUDE.md` and other repo `CLAUDE.md`** — docs-owned and read-only mid-pipeline. The OBP block names plural namespaces (lines ~22, 24, 47). **Do not edit directly** — file a `.bot/singular-namespaces/claude-md-proposals.md` entry so the docs bot updates them at merge.
