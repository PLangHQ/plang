# An action's properties hold their values — the program holds no Data

Designed with Ingi, 2026-09-24. **Released to coder 2026-09-24.** Follows remove-context (steps 1-6, landed at `c626beea9`).

> **You (coder) own this.** The rules are settled with Ingi. Shapes, names not fixed here, and the commit split are yours; each commit green. Code in this doc is direction; NEW marks what does not exist.

## Why

The program (`.pr`, loaded once, shared by every run of every actor) still holds `Data`: each action's values are `Data` in `action.Parameter` / `action.Default`, born by the `.pr` reader (`data/reader/this.cs:137`). A `Data` carries a context, and the shared program must hold none. Since remove-context step 1 that `Data` is created **without** a context, on purpose (step 1's explicit exception). That is the only place left where a `Data` has no context, and `Data.Context` must not be nullable (Ingi).

**Fix: the program holds no `Data`.** An action is a class, and a class has **properties** (Ingi). An action's property holds its value, raw as loaded. The run creates the first `Data` from it, with the run's context.

## Vocabulary (Ingi)

"Parameter" is not used. The `.pr` holds an action's properties, and each maps to an action property, `goal/step/action/property/this.cs`. In code, `action.Parameter` and `goal/step/action/parameter/list` become the action's properties. The `.pr` key `"parameter"` becomes `"property"` too (see "The `.pr` key" below).

## The design (settled with Ingi)

1. **One `property` class, two sources, each fills what it knows.**
   - **From the `.pr`** (a program action): `Name`, `Type` (the type the step gave, possibly narrower than declared), `Value` (the raw item as loaded: a `wire`/`source`, or an eager held action/goal.call; never loaded here), the `"properties"` bag (supported; no `.pr` of 1,620 uses it today), and whether the **step set it** or the **build froze it as a default**. Filled by the reader's own field cases (`goal/step/action/serializer/Reader.cs:119-138`). No reflection at load.
   - **From the handler class** (a catalog action, `module[actionName]`, `module/this.cs:79-80`): `Name`, the declared `Type`, `Nullable`, and the `[Default]` value. Reflected as today (`property/this.cs:19-39`).
2. **A program action holds only the properties its `.pr` holds** (Ingi, option (a)): the ones its step set, plus the defaults the build froze. The rules (required, declared type, default) come from its catalog twin, `Module[Name].Property`, when the builder or validation ask. The per-program-action reflection at `goal/step/action/this.Schema.cs:43-44` stops: a program action's `Property` is its `.pr`'s; a catalog action's `Property` is its class's.
3. **Defaults stay frozen in the `.pr`** (Ingi, determinism). The build writes the class's `[Default]` for every property the step didn't set (`module/action/build/code/Default.cs:263-270`). A built app runs the same on a later runtime that changes a default.
4. **The binding order** (Ingi):
   ```
   step value → setting → frozen default (.pr) → [Default] (runtime; only for a .pr without one)
   ```
   An explicit setting wins over a frozen default (`set %!http.request.TimeoutInSec% = 5` applies to a built step whose `.pr` froze `30`); a runtime change of `[Default]` never does. Today's order is different: the generated binding asks `action[name]` (set ?? frozen) first and the setting only when that's empty (`Emission/Property/Data/this.cs:158-160`). So a property knows whether its step set it or the build froze it.
5. **The run's `Data` is the first `Data`.** The generator's door becomes, in direction:
   ```csharp
   var p = action[name];                                        // the property (set ones; the order above decides)
   new Data(p.Name, p.Value, context: context) { … bag … }      // the run's copy, born with the run's context
   ```
   (today `action?[name]?.Copy(context)` / `.As<T>(context)`, `Emission/Action/this.cs:374-381`).
6. **`IsVariable` on `property` goes** (`property/this.cs:33, :61`, no reader). Not to be confused with the value's `data.IsVariable` (`data/this.cs:167`), which stays.
7. **The synthetic `channel` property** (`property/list/this.cs:57-58`) is unchanged: a property whose declaration is written by hand.

## What changes, by area (from coder's inventory, `coder/to-architect-parameter-rows-inventory.md`)

- **`.pr` reader**: `goal/step/action/serializer/Reader.cs:61, :65, :68, :131-132` build properties, not `Data`. The `Data` reader's context-less row door (step 1's exception, `data/reader/this.cs:137` path) goes.
- **`.pr` writer**: `goal/step/action/this.Item.cs:53-57` writes the properties back under the key `"property"` (see "The `.pr` key" below). A `.pr` the writer produces reads back and writes again byte-identical.
- **Selection / generator**: `action[name]` (`goal/step/action/this.cs:141-142`) returns the property; `__Copy` / `__View` (`Emission/Action/this.cs:374-381`), the channel binding (`:317`), the null guards (`:178-185, :201-208`) and `SnapshotParams` (`Emission/Property/Data/this.cs:215-216`) read properties; the binding order (4).
- **Builder**: the default pass (`build/code/Default.cs:267-269`); goal.call's Build (`module/action/goal/call.cs:51-53, :70-72`, today `SetValue` on the program's `Data`) and variable.set's Build (`module/action/variable/set.cs:81-89`, today `Parameter.Add`) change the action's properties; that's building the program, which is allowed at build. The file.read (`file/read.cs:120`), http (`http/HttpBuildHelpers.cs:16`) and llm.query (`llm/query.cs:117, :124`) Build hooks and the action's Build/Callee (`this.Build.cs:46-47`, `this.Callee.cs:19-20`) read property values.
- **Validation** (`goal/step/action/this.Validate.cs:40-46`): compares the program action's properties with its catalog twin's (a required one missing; a name the class doesn't declare).
- **Graft typing** (`goal/step/this.cs:88`): a grafted modifier today shares the action's list by reference (`Parameter = a.Parameter`). It gets its own properties; no two actions share one list.
- **mock/intercept** (`module/action/mock/intercept.cs:87-117`) and **debug** (`module/action/debug/this.cs:289-294, :548-550`) read property values.
- **Tests**: the two builders (`Make.Action` ×107, `TestAction.Create` ×69) build properties; the 34 direct lines in 15 files follow.
- **`module/list/this.cs:173` `GetDefaults`** (verb+noun on the registry): the default pass reads the defaults off the catalog action's properties instead; `GetDefaults` goes.

## Order — each commit green; report after each

Your split; one suggestion:
1. `property` takes a value (Value, the bag, set-or-frozen); the reader builds and the writer writes properties; `action[name]` returns a property; validation against the catalog twin; `IsVariable` goes; test builders.
2. The generator: the run's `Data` from the property; the binding order.
3. Builder hooks, the default pass (`GetDefaults` goes), graft typing, mock/intercept, debug.
4. Removals: `action.Parameter`, `parameter/list`, the data reader's context-less row door, step 1's `parameter.list` no-context enumeration, per-program-action reflection.

## Tests (red first where they aren't red already)

1. **No Data in the program:** after loading a `.pr`, no action holds a `Data` (reflection over an action's properties); two actors running one goal each get a run `Data` born with their own context.
2. **A setting beats a frozen default:** `set %!http.request.TimeoutInSec% = 5`, then a built `http.request` whose `.pr` froze `30` uses 5. A step-set value beats the setting.
3. **A frozen default beats a changed `[Default]`:** a test action whose `.pr` froze one default and whose class declares another uses the frozen one.
4. **Round trip:** a `.pr` the writer produces reads back and writes again byte-identical. An old `.pr` with the key `"parameter"` fails to load with the named error.
5. **Validation:** a `.pr` property the class doesn't declare fails the build; a missing required one fails.
6. **Graft:** a grafted modifier's properties are its own; changing one doesn't change the other.
7. **Build hooks:** goal.call and variable.set produce the right properties.
8. `SharedRow_ReadDirectly_FailsWithNamedError` changes: there is no program `Data` to read; the equivalent is "a property's value can only be read through a run's `Data`".

## Demolition

- `action.Parameter`; `goal/step/action/parameter/list`; the `Data` reader's context-less row door; step 1's `parameter.list` enumeration without a context.
- The per-program-action reflection (`goal/step/action/this.Schema.cs:43-44` on a program action).
- `property.IsVariable`.
- `module/list/this.cs:173` `GetDefaults`.
- The generator's `Copy(context)` / `As<T>(context)` over a program `Data`.

**Stays:** `goal/step/action/property/this.cs` (the one class); a catalog action's reflected properties; frozen defaults in the `.pr`; `app.type.Field` (open-items #16, separate).

## The `.pr` key: renamed now (Ingi)

**The `.pr` key `"parameter"` becomes `"property"` now.** `"default"` stays. No `.pr` is rebuilt for this: the builder regenerates them when it builds, and the existing ones can't run on this branch anyway (Ingi).

- **An old key fails loudly.** The action reader skips keys it doesn't know (`default: reader.Skip()`, `goal/step/action/serializer/Reader.cs:102`), so an old file's `"parameter"` would be silently ignored and the action would load with no properties. Instead, `"parameter"` raises a named error: an old `.pr` format, rebuild it.
- **The writer writes `"property"`** (`goal/step/action/this.Item.cs:53-57`).
- **The builder:** it is built by a python script plus plang parts (Ingi). The plang parts take their instructions from the runtime and need no change. Change these:
  - the python in `tools/decider/` (`params.py`, `stage3b.py`, `build_pr.py`, `pr_bootstrap.py`, `harness.py`; 36 lines mention `parameter`) to write `"property"`
  - the LLM instructions, which teach the answer in the `.pr`'s own keys (`Reader.cs:55-56`: "the LLM answers in them too"): `os/system/builder/llm/Properties.llm:15-84` (the examples and "an action is `{module, name, parameter}`") and the schema in `os/system/builder/BuildGoal/Properties.goal:16` (`parameter?: list<…>` for actions and modifiers)
- **C# tests that read `.pr` files are changed so they work** (Ingi). The round-trip test (test 4) uses files the writer produces, not files on disk.
- **Not renamed:** goal.call's own property named `Parameter`, the arguments passed to the called goal (`Properties.llm:69`, "each argument the step passes is one row in `Parameter`"). It names the goal's parameters, not an action's properties. It stays unless Ingi says otherwise.

## OBP validation

| Surface | Check |
|---|---|
| program | holds no `Data`, so no context; construct at load, evaluate in the run |
| `property` | one class, two sources (the `.pr`, the handler class), each fills what it knows; no parallel name-matched lists |
| program action vs catalog action | instance values vs class rules; the program action asks its catalog twin (`Module[Name]`) |
| run `Data` | the first `Data`, born with the run's context; never stamped |
| binding order | an explicit setting over a frozen default; a frozen default over a runtime default |
| `GetDefaults` | verb+noun on a registry; the defaults are the catalog action's own knowledge |
