# To architect — action parameter rows: `properties` on disk, and who reads them as Data

Read-only inventory, branch `goal-graph-singular`, tree at 3e44a167a plus uncommitted remove-context step 1. No code changed for this.

## 1. `properties` next to name/type/value on a parameter or default row

**None.**

- **Scanned:** 1,620 `.pr` files under `os/`, `Tests/` and `PLang.Tests/` (traces, bin and obj skipped).
  - The scan walked every object that has `module` and a `parameter(s)` / `default(s)` array.
  - That is 8,098 rows, and **0** of them carry a `properties` key.
- **Where `properties` does appear in `.pr` files:** 28 files. None of them is a row's Data properties:
  - JSON-schema `"properties": {…}` inside LLM `ReturnScheme` / step `Properties` blocks, in older-format files under `os/`.
  - A type entity written in its old full form as a `variable.set` `Type` value, for example `Tests/LazyDeserialize/.build/readcsv_landsastable.test.pr:64`:
    ```json
    "value": { "name": "file", "kind": "csv", "strict": false,
               "fields": null, "values": null,
               "properties": [ { "name": "path", "typename": "path" } ] }
    ```
    The same shape appears in `Tests/CompareRedesign/Cut2_LazyReadAndNarrow/.build/cut2.test.pr:106` and `…/Narrow_ChainWideBangBothBranches/.build/…test.pr:105`. These are type facts inside a value, not row properties.
- **In code:**
  - The reader accepts row properties at `data/reader/this.cs:138` / `:145` (`d.Properties = properties`).
  - No C# reads `Properties` off a parameter row.

## 2. Sites that read an action's Parameter / Default list as Data

The members each site uses are in brackets. "Row" means a Data inside `action.Parameter` / `action.Default` (`parameter.list`). "Run copy" means the generator's own Data (`action[name]?.Copy(context)`), which is not the row.

### `.pr` reader
- `goal/step/action/serializer/Reader.cs:61`: `action.Parameter.Add(Parameter(raw, ctx, dataReader))`. This births the row (via `data/reader/this.cs:137`, with the loading actor's context).
- `goal/step/action/serializer/Reader.cs:65, :68`: `action.Default = new()` / `.Add(dataReader.Read(...))`.
- `goal/step/action/serializer/Reader.cs:131-132`: an `action`-typed row holds a held action, `new Data(name, action, ctx.Context)`.
- `data/reader/this.cs:137-138, :144-145`: the row birth itself, plus `[Properties]` (set only, never read).

### `.pr` writer
- `goal/step/action/this.Item.cs:53-57`: `Parameter.Output(...)`, `Default.Output(...)` [Output].

### Selection / generator (the run's door to a row)
- `goal/step/action/this.cs:141-142`: the `action[name]` indexer. It checks Parameter, then Default [Name].
- `PLang.Generators/Emission/Action/this.cs:374-381`:
  - `__Copy` → `action?[name]?.Copy(context)`
  - `__View<T>` → `action?[name]?.As<T>(context)`

  [Copy, As]. Every lazy property of every action goes through here.
- `PLang.Generators/Emission/Action/this.cs:317`: `action?["channel"]?.Copy(context)` [Copy].
- `PLang.Generators/Emission/Action/this.cs:178-185, :201-208`: null guards, `action?.Parameter.FirstOrDefault(d => d.Name == …)?.Peek()` [Name, Peek].
- `PLang.Generators/Emission/Property/Data/this.cs:215-216`: `SnapshotParams`, Parameter then Default `FirstOrDefault(p => p.Name …)` [Name, Peek].

### Builder (Build hooks and the build pass)
- `module/action/build/code/Default.cs:267-269`:
  - `foreach p in a.Parameter` [Name]
  - `a.Default = new parameter.list(modules.GetDefaults(...))`, which writes Default (Data built in `module/list/this.cs:173`).
- `module/action/goal/call.cs:51-53, :70-72` (`Build`): `foreach row in __action.Parameter` [Name, **SetValue**]. **This writes the program row.**
- `module/action/variable/set.cs:81-89` (`Build`): `foreach p in __action.Parameter` [Name], then `__action.Parameter.Add(new Data("Type", …))`. **This adds a row.**
- `module/action/file/read.cs:120` (`Build`): `FirstOrDefault(p.Name == "Path")?.Peek()` [Name, Peek].
- `module/action/http/HttpBuildHelpers.cs:16`: same shape [Name, Peek].
- `module/action/llm/query.cs:117, :124` (`Build`): Schema / Format rows [Name, Peek].
- `goal/step/action/this.Build.cs:46-47`: `foreach parameter in Parameter`, `parameter.Peek() is action held` [Peek].
- `goal/step/action/this.Callee.cs:19-20`: same [Peek].

### Validation
- `goal/step/action/this.Validate.cs:40-41, :45-46`: `foreach p in Parameter`, `emitted.Add(p.Name)` [Name].
  - `row.Default` at `:45` is `property.Default`, the attribute default on the property row, not a Data.

### Graft typing
- `goal/step/this.cs:88`: a grafted modifier is born with `Parameter = a.Parameter`. **The same list instance is shared by reference** between the catalog action and the modifier.
- `goal/step/action/property/list/this.cs:50` and `module/action/build/validate.cs` read property rows (`property.@this`, `row.Type` = the type entity), not Data. No Data-row reads.

### mock / intercept
- `module/action/mock/intercept.cs:87-91`: `foreach param in action.Parameter`, `result[param.Name] = ResolveParamValue(param, …)`.
- `module/action/mock/intercept.cs:100-104`: `FirstOrDefault(p.Name == name)`.
- `ResolveParamValue` (`:114-117`): `param.Peek()`, including `text.Template` [Name, Peek].

### goal.call arguments (run time)
- `module/action/goal/call.cs:110-116` reads its own `Parameter` property. That is the **run copy** (generator `__Copy`), not the row:
  - `args.Items(Context)`, then `arg.Peek()`, `arg.Name`, `arg.Copy(Context)`.
  - The rows are reached only through `action[name]`.
- `module/action/llm/code/OpenAi.cs:1046`: `Call.Parameter?.Peek()`, the held goal.call handler's run copy [Peek, Items].

### debug
- `module/action/debug/this.cs:289-294`: `foreach p in action.Parameter` [Name, Peek].
- `module/action/debug/this.cs:548-550`: same [Peek, text.Template].

### Templates / .goal
- `os/system/builder/templates/actionFormal.template:1`: for `a.Parameter` and `m.Parameter` (modifiers) it reads `p.Name`, `p.Type`, `p.Value | formal`, `a.Parameter.size`. Fluid reflects the Data rows.
- `os/system/builder/llm/templates/propertiesUser.template:20` reads **property** rows (`p.Type.Name/Kind/Values`), not Data.
- No `.goal` file reads `action.parameter` / `default`.

### Not rows (excluded)
- `ui/code/Fluid.cs:197-199` is the Render handler's own `Parameter` property (a run copy).
- The provider `Default` classes, `View.Default` and `DefaultAttribute`.

### Tests
- **Direct row reads:** 34 lines in 15 files.
  - Calls: `.Parameter.First` ×12, `.FirstOrDefault` ×7, `.Parameter[i]` ×4, `.Parameter.Add` ×3, `.Parameter.Count` ×2, `.Parameter.Any` ×1, `.Default!.Count` ×2, `.Default.FirstOrDefault` ×1, `.Default.Add` ×1.
  - Members: `p.Name` ×22, `row.Peek` ×5, `row.Value` ×3, `param.Type` ×1.
- **Row builders:**
  - `Make.Action(...)`: 107 call sites.
  - `TestAction.Create(...)`: 69 call sites.

  Both construct Data rows for `action.Parameter`, so every C#-composed action test depends on the row being a Data.

## Summary
- **Members used on a row:** Name (everywhere), Peek (builder, validation, debug, mock, generator guards), Copy / As (the generator's run door), Output (the `.pr` writer), SetValue and Add (two Build hooks that **mutate the program row**: goal.call and variable.set), Type (template, one test).
- **Not used on a row:** Properties is never read. Value is used only in tests.
- **Heaviest dependency:** test construction (176 builders) plus the generator's `action[name]?.Copy(context)`, which is every action property.
