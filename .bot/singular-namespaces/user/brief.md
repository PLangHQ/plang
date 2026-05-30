# Singular Namespaces — Architect Brief

**Branch:** `singular-namespaces`
**Author:** Ingi (with claude as scribe)
**Scope:** `PLang/app/**` only — folder/namespace shape. No PLang `.goal` code changes.

---

## 1. Why

When working with an object you are always working with *one* object at a time —
even inside `foreach`, even inside parallel branches. Plurality is a property of
the *collection container*, not of the entity. The folder/namespace tree should
reflect that: an entity's home is singular; a collection over it is a child of
the entity.

This corrects a long-standing inconsistency in `PLang/app/**` where some folders
are plural (`goals/`, `channels/`, `variables/`, `errors/`, `formats/`,
`types/`, …) and some are PascalCase (`builder/Types/`, `tester/Test/`,
`http/Response/`, `mock/Mock/`, `Services/Service/`).

---

## 2. The rule

For each domain entity `X`:

- **`X/this.cs`** — the singular entity (the `@this` class).
- **`X/list/this.cs`** — the collection over `X` (LINQ / Add / Remove / index).
  This is the collection container that used to live in the plural folder's
  `this.cs`.
- **`X.@this.List`** — public property on the entity carrying the collection.
  Each entity reaches the registry through its back-reference to App; the
  property is a navigation shortcut.
- **`app.@this.X`** — property of type `X.@this` returning the **first loaded
  instance** of X. Plural access is `app.X.List`.
- Where the current tree has `plural/singular/` (registry + child entity),
  **collapse to one** `singular/` folder. The plural's `this.cs` becomes
  `singular/list/this.cs`.
- Folder names are **lowercase** throughout (no PascalCase under `app/`).
- API property names on entities may still read as English plurals
  (e.g. `Call.Children`, `Goal.Bindings`) — the rule governs *folder /
  namespace / type* shape, not the navigation-property name on a parent.

---

## 3. Complete rename list

### 3.1 Collapse (plural + singular-child → singular)

| From | To |
|---|---|
| `app.channels` + `app.channels.channel` | `app.channel` |
| `app.channels.serializers` + `…serializers.serializer` | `app.channel.serializer` |
| `app.goals` + `app.goals.goal` | `app.goal` |
| `app.goals.goal.steps` + `…steps.step` | `app.goal.step` |
| `…step.actions` + `…actions.action` | `app.goal.step.action` |
| `app.events.lifecycle.bindings` + `…bindings.binding` | `app.event.lifecycle.binding` |
| `app.variables.calls` + `…calls.call` | `app.variable.call` |

For each: the parent `this.cs` (the registry) moves to `<singular>/list/this.cs`;
the child folder's contents move up into `<singular>/`.

### 3.2 Rename (plural folder with no singular-child)

| From | To | Notes |
|---|---|---|
| `app.events` | `app.event` | `events.@this` → `event/list/this.cs`. `EventType.cs` stays at root. |
| `app.variables` | `app.variable` | `variables.@this` (per-actor store) → `variable/list/this.cs`. `Variable.cs` becomes `variable/this.cs`. |
| `app.variables.navigators` | `app.variable.navigator` | 4 INavigator implementations + registry → `navigator/list/this.cs`. |
| `app.errors` | `app.error` | ~15 error-type files stay at namespace root. `errors.@this` → `error/list/this.cs`. |
| `app.errors.trail` | `app.error.trail` | Rides along. |
| `app.formats` | `app.format` | `formats.@this` (file-format registry) → `format/list/this.cs`. |
| `app.types` | `app.type` | Heterogeneous registry (name↔CLR + scheme). Becomes `type/this.cs`. |
| `app.types.choices` | `app.type.choice` | Vocabulary registry → `choice/list/this.cs`. |
| `app.callstack.call.children` | `app.callstack.call.child` | See §3.5. |
| `app.callstack.call.diffs` | `app.callstack.call.diff` | `Diff` record promoted to `diff/this.cs`. |
| `app.callstack.call.errors` (ns PascalCase `Errors`) | `app.callstack.call.error` | Also fixes the PascalCase namespace. |
| `app.callstack.call.tags` | `app.callstack.call.tag` | |
| `app.channels.channel.events` | `app.channel.event` | Confirm contents before move (only `this.cs` today). |
| `app.goals.setup` | `app.goal.setup` | Rides along. |

### 3.3 PascalCase → lowercase

| From | To |
|---|---|
| `app.builder.Types` | `app.builder.type` |
| `app.builder.Types.Spec` | Collapse: 2 records (`Action.cs`, `Example.cs`) hoist into `app.builder.type/` |
| `app.tester.Test` | `app.tester.test` |
| `app.http.Response` | `app.http.response` |
| `app.mock.Mock` | Collapse: inner `Mock/this.cs` becomes `mock/this.cs` |
| `app.Services.Service` | `app.service.@this` |

### 3.4 Module — exceptional (no `app.module` navigation property)

`modules/` is renamed to `module/` (lowercase + singular), but the OBP rule
about `app.X` carrying a singular navigation entity does **not** apply.

- Folder `modules/` → `module/`.
- All action modules keep their names: `app.module.file`, `app.module.list`,
  `app.module.llm`, `app.module.loop`, `app.module.http`, …
- The current `modules/this.cs` (flat action registry) is **removed from
  `app.@this`'s public surface**. The class stays — the runtime needs it —
  but moves to `module/registry.cs` (no longer a `this.cs`, so no top-level
  `module.@this` entity claim). Internal consumers (source generator, action
  resolver, dispatch) reach it via wherever the runtime holds it (private/
  internal field on `app.@this`).
- This removes the collision between "registry accessor `module.list`" and
  "the `list` action module".
- `app.modules.module` (action module *about* modules) is folded into
  `app.module.environment` as part of this pass.
- `app.modules.@event` (keyword-escaped) → `app.module.@event` — the `@`
  stays (still a C# keyword).

Rationale: action modules are *dispatched*, not *navigated*. They don't fit
the "first loaded instance" frame the way goals, channels, variables do.
Making `module` the deliberate exception is cleaner than reshaping every
action module to dodge the collision.

### 3.5 Action-module return-shape types

Each action module currently has a single `types.cs` file holding one record
matching the module's name (the action's return shape):

| Today | After |
|---|---|
| `app.modules.list.types.list` | `app.module.list.type.list` |
| `app.modules.loop.types.loop` | `app.module.loop.type.loop` |
| `app.modules.math.types.math` | `app.module.math.type.math` |
| `app.modules.module.types.module` | `app.module.environment.type.module` (folds with §3.4) |
| `app.modules.output.types.output` | `app.module.output.type.output` |

Folder `types/` → `type/` (singular). Record name unchanged. If a module
later grows a second return-shape record, the `type/` folder absorbs it
without restructuring.

### 3.6 `children` — strict folder rule, plural property name

The rule says folder `children/` → `child/`. But there is no separate `child`
entity — children of a `Call` are themselves `call.@this` instances.

Resolution:

- Folder: `app.callstack.call.child/`
- Inside: only `list/this.cs` (the collection container). No `child/this.cs`
  because the entity *is* `call.@this`.
- Property on `Call.@this`: **`public child.list.@this Children { get; }`** —
  property name reads as natural English plural; type is the strict-rule
  singular.

Same pattern applies if `Goal` later grows a `Children` (sub-goals) property:
folder `goal/child/`, property `Goal.Children`.

### 3.7 Leave-alone (already singular or pure category buckets)

`actor`, `actor.context`, `actor.context.trace`, `actor.permission`,
`callstack`, `callstack.audit`, `callstack.call`, `config`, `data`,
`data.code`, `keepalive`, `snapshot`, `tester`, `types.path` and all of
`path/*`, individual action-module folders (`file`, `llm`, `loop`, …),
`*/code/` folders, `modules.builder.warning`.

---

## 4. Property renames on `app.@this`

| Today | After | Type today | Type after |
|---|---|---|---|
| `App.Goals` | `App.Goal` | `goals.@this` | `goal.@this` (first loaded) |
| `App.Channels` *(on Actor)* | `App.Channel` | `channels.@this` | `channel.@this` |
| `App.Variables` *(on Actor)* | `App.Variable` | `variables.@this` | `variable.@this` |
| `App.Errors` | `App.Error` | `errors.@this` | `error.@this` |
| `App.Formats` | `App.Format` | `formats.@this` | `format.@this` |
| `App.Types` | `App.Type` | `types.@this` | `type.@this` |
| `App.Events` | `App.Event` | `events.@this` | `event.@this` |
| `App.Navigators` | `App.Navigator` | `navigators.@this` | `navigator.@this` |
| `App.Modules` | *(removed)* | `modules.@this` | not exposed; see §3.4 |
| `App.CallStack` | `App.CallStack` *(unchanged)* | already singular | — |

Plural access goes through `.List` on the singular: `App.Goal.List`,
`App.Channel.List`, etc.

**Open design question for architect:** how does "first loaded instance" work
when the registry is empty? Suggested: `App.Goal` returns null if no goals
loaded; consumers that today touch `App.Goals.Get(...)` migrate to
`App.Goal.List.Get(...)`. Architect to confirm.

---

## 5. Scope confirmation

- **In scope:** `PLang/app/**` folders + namespaces + types.
- **In scope (mechanical follow-through):**
  - `PLang.Tests/App/**` mirror folders (PascalCase mirror per CLAUDE.md stays;
    inner references to renamed types update mechanically).
  - `PLang.Generators/**` — any tracking-name strings or namespace literals
    referencing the renamed namespaces.
  - `PLang/Runtime2/**`, `PLang/Building/**`, `PLang/GlobalUsings.cs` — every
    `global using` alias touching a renamed type updates; consumer files
    follow.
  - `Documentation/**` — references to renamed paths/namespaces updated.
- **Out of scope:**
  - PLang `.goal` code. The user has confirmed no PLang-level code references
    `goals.goal.steps.step.actions.action.@this`-style namespaces.
  - Behavior changes. This is a rename pass; no logic moves, no APIs gain or
    lose capabilities beyond the `App.X` singular-vs-list reshaping.

---

## 6. What the architect should deliver

A staged plan covering:

1. **Order of operations** — which folder/namespace to rename first so the
   build stays at most temporarily broken, not catastrophically broken.
   (Leaf-up vs root-down; collapses before standalone renames or vice versa.)
2. **`global using` migration** — `PLang/Runtime2/GlobalUsings.cs` and
   `PLang.Tests/GlobalUsings.cs` carry many aliases pointing at these
   namespaces. Update strategy.
3. **`app.@this` property reshaping** — singular nav property + `.List`
   collection. Migration of every consumer.
4. **Module exception (§3.4)** — concrete plan for moving `modules/this.cs`
   off `app.@this` and into `module/registry.cs` without breaking action
   discovery, dispatch, or source-gen.
5. **Source-generator updates** — `PLang.Generators/` has tracking-name
   constants referencing these namespaces. List them and the diffs.
6. **Test mirror sync** — `PLang.Tests/App/**` folder renames (PascalCase
   mirror) + inner namespace alias updates.
7. **Documentation sweep** — `Documentation/Runtime2/**`, `Documentation/v0.2/**`,
   `CLAUDE.md` reference updates (the OBP convention block in `CLAUDE.md`
   mentions several plural namespaces by name).
8. **Verification** — `dotnet build` clean, `dotnet run --project PLang.Tests`
   green, `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green
   after a clean rebuild (per the stale-binary trap in CLAUDE.md).

---

## 7. Non-goals / explicit deferrals

- **Do not rename the `list` action module.** The exception in §3.4 means
  there's no collision to resolve.
- **Do not rename PLang vocabulary** (no `file` → `path-action`, no `list` →
  `collection`).
- **Do not restructure action handlers' internal shape** — only their
  namespace home and `types.cs` → `type/` follow-through.
- **Do not touch `Documentation/**` content beyond reference updates** —
  no rewrites of explanation, no new sections.
