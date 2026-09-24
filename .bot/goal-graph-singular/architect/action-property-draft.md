# An action's properties hold their values — the program holds no Data

With Ingi, 2026-09-24. **Draft, being designed with Ingi. NOT for coder.** Coder is waiting on this for the step-1 question below.

## Why

Found by coder in remove-context step 1. The `.pr` reader creates each parameter as a `Data` with the loading actor's context (`data/reader/this.cs:137`, `new Data(name, value, context: ctx.Context)`). Until now that context was wiped **by accident**: adding the `Data` to `action.Parameter` stamped the list's `null` context onto it (`list.Add`, `item.Context = _context`). Step 1 removes that stamp, so the `Data` in `action.Parameter` would keep the loader's context. That's a stale context on the shared program, which is exactly what remove-context removes.

Making that `Data`'s context null was rejected (Ingi): `Data.Context` must not become nullable. It already is null in practice for some `Data`, hidden by `!` (`data/this.cs:229`, `_context = context ?? parent?._context!`), and that's a problem to remove, not to extend.

## The direction

**The program holds no `Data`.** Every `Data` is created by a run, with that run's context.

**An action's property holds its value.** Today each action object has two lists about the same slots, matched only by name:

- `action.Property` (`goal/step/action/this.Schema.cs:43-44`, `_properties ??= new(Handler, Module.App.Type)`): the slot rules, reflected from the handler class, per action object, cached.
- `action.Parameter`: the values from the `.pr`, as `Data`.

They are two halves of one slot on one action. One object: the action's property holds its rules and its value, like a property on a C# object has a value.

```
action.Property["Path"]  →  Name: Path, Type: path, Value: "file.txt" (raw, as loaded, no context)
```

**One class, two sources, each fills what it knows:**

- **From the `.pr`**, in the reader's own field cases (`goal/step/action/serializer/Reader.cs:119-138`): `name` → `Name`, `type` → `Type` (the type the step gave, possibly narrower than the slot's), `value` → `Value` (the raw `wire`/`source`, never loaded here). No reflection at load.
- **From the handler class**, reflected when the builder, validation or the menu ask (`goal/step/action/property/this.cs:19-39`): `Name`, the declared `Type`, `Nullable`, `Default`. At run time nobody needs these; the generator bakes `[Default]` and the required-check into the generated handler code.

**The run's `Data` is the first `Data`.** `action[name]` returns the property; the generator creates the run's copy from it with the run's context, through the existing item constructor (`data/this.cs:272-283`):

```csharp
var p = action[name];
new Data(p.Name, p.Value, context: context)        // today: action?[name]?.Copy(context) (Emission/Action/this.cs:374-381)
```

So the shared program holds no context, and `Data.Context` is never null for this reason.

## Open — to settle with Ingi

1. **Where `property` lives.** Today it's at `goal/step/action/property` (the handler's declared slots). `app.type.Field` (`type/Field.cs`: `Name` plus `TypeName` as a string) describes a type's fields and properties (`type/this.cs:439, :445`, built at `type/list/this.cs:539-550`). Same concept (open-items #16). The 23-Sept todo "property rows as plang reflection" (one `property`, like C#'s `PropertyInfo`, for a type's fields and an action's slots) was left to decide with "actions as LLM tools". Fold it in now as `app.type.property`, or keep them apart?
2. **How the two sources meet on one action.** A slot the step doesn't give (it uses the default) has no value from the `.pr`. A name in the `.pr` that the handler doesn't declare: validation catches it at build, since the reader doesn't reflect.
3. **`action.Default`** merges the same way: a default is a value for a property.
4. **`IsVariable`** goes: it only repeats "the type is `variable`".
5. **The `.pr` property bag** on a parameter (`data/reader/this.cs:138`, `d.Properties = properties`): does the property keep it?
6. **The synthetic `channel` slot** (`property/list/this.cs:57-58`).
7. **Cost, not counted yet:** every reader of `action.Parameter` / `action.Default` as `Data` (the builder, validation, graft typing, the `.pr` writer, mock/intercept, goal.call's arguments).
8. **Coder's step-1 question** (the test `SharedRow_ReadDirectly_FailsWithNamedError` now reads the loader's context) waits on this.
