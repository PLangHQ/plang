# An action's properties hold their values — the program holds no Data

With Ingi, 2026-09-24. **Draft, being designed with Ingi. NOT for coder.** Coder is waiting on this for the step-1 question below.

**Vocabulary (Ingi):** an action is a class, and a class has **properties**. The `.pr` holds an action's properties, and each maps to an action property (`goal/step/action/property/this.cs`). The word "parameter" is not used. The code and the `.pr` still say it in three places, and all three become "property": the `.pr` key `"parameter": [...]` (read at `goal/step/action/serializer/Reader.cs:58`), `action.Parameter`, and `goal/step/action/parameter/list`.

## Why

Found by coder in remove-context step 1. The `.pr` reader creates each of an action's properties as a `Data` with the loading actor's context (`data/reader/this.cs:137`, `new Data(name, value, context: ctx.Context)`). Until now that context was wiped **by accident**: adding the `Data` to the action's list stamped the list's `null` context onto it (`list.Add`, `item.Context = _context`). Step 1 removes that stamp, so the `Data` would keep the loader's context. That's a stale context on the shared program, which is exactly what remove-context removes.

Making that `Data`'s context null was rejected (Ingi): `Data.Context` must not become nullable. It already is null in practice for some `Data`, hidden by `!` (`data/this.cs:229`, `_context = context ?? parent?._context!`), and that's a problem to remove, not to extend.

## Settled with Ingi

**1. The program holds no `Data`.** Every `Data` is created by a run, with that run's context.

**2. An action's property holds its value.** Today each action object has two lists about the same properties, matched only by name. `action.Property` (`goal/step/action/this.Schema.cs:43-44`, `_properties ??= new(Handler, Module.App.Type)`) holds the rules, reflected from the handler class per action object and cached. The other list holds the values from the `.pr`, as `Data`. They are two halves of one property on one action, so they become one object:

```
action.Property["Path"]  →  Name: Path, Type: path, Value: "file.txt" (raw, as loaded, no context)
```

**3. It lives at `goal/step/action/property/this.cs`** (Ingi).

**4. One class, two sources, each fills what it knows:**
- **From the `.pr`**, in the reader's own field cases (`goal/step/action/serializer/Reader.cs:119-138`): `name` → `Name`, `type` → `Type` (the type the step gave, possibly narrower than the class declares), `value` → `Value` (the raw `wire`/`source`, never loaded here). No reflection at load.
- **From the handler class**, reflected when the builder, validation or the menu ask (`goal/step/action/property/this.cs:19-39`): `Name`, the declared `Type`, `Nullable`, `Default`.

**5. An action holds only the properties its step set (Ingi: option (a)).** For `read file.txt`, the `file.read` class declares `Path` (path, required) and `ResolveVariables` (bool, default false). The step sets only `Path`, so the action holds `Path = "file.txt"`. When the builder or validation need the rules (is `Path` required, what's the default), they ask the handler class, as today. At run time nothing else is needed: the generator binds by name, and a property the step didn't set falls to its setting and `[Default]` in generated code. A `.pr` name the class doesn't declare is caught by validation at build.

**6. The run's `Data` is the first `Data`.** `action[name]` returns the property. The generator creates the run's copy from it with the run's context, through the existing item constructor (`data/this.cs:272-283`):

```csharp
var p = action[name];
new Data(p.Name, p.Value, context: context)        // today: action?[name]?.Copy(context) (Emission/Action/this.cs:374-381)
```

So the shared program holds no context, and `Data.Context` is never null for this reason.

**7. Defaults stay in the `.pr`, frozen at build (Ingi).** The builder writes the class's `[Default]` for every property the step didn't set into the `.pr` (`module/action/build/code/Default.cs:263-270`, via `module/list/this.cs:173` `GetDefaults`). It is **not a copy**: it's the default as it was when the app was built. If a later runtime changes a default (say `ResolveVariables` becomes `true`), a built app still runs the same. That's determinism. So the action holds what its `.pr` holds: the properties its step set, plus the defaults the build froze. Both come from the `.pr`; both are properties of the action.

**8. The order: step value → setting → frozen default → `[Default]` (Ingi).** Two different things are kept apart:
- **The runtime changes its own default** (a new plang version makes `TimeoutInSec` 60). A built app must not notice, and the frozen default protects that.
- **You set a value on purpose** (`%!http.request.TimeoutInSec%` = 5 at execution). That's an explicit choice, and it wins.

```
step value → setting → frozen default (.pr) → [Default] (runtime; only for a .pr without one)
```

This reverses today's order. The generated binding (`Emission/Property/Data/this.cs:158-160`) asks `action[name]` first (the set value ?? the frozen default, `action/this.cs:140-142`), and consults the setting only when that's empty. So a frozen `[Default(30)]` (`module/action/http/request.cs:40`) would shadow `set %!http.request.TimeoutInSec% = 5`. The binding becomes: the step's value, else the setting, else the frozen default, else `[Default]`.

So **a property knows whether its step set it or the build froze it**, because a setting sits between the two. Today the `.pr` already keeps them apart (`"parameter"` and `"default"`); the property carries that.

## Open

1. **`IsVariable`** (`property/this.cs:59-61`) goes: it only repeats "the type is `variable`".
2. **The `.pr` property bag** (`data/reader/this.cs:138`, `d.Properties = properties`): does the property keep it?
3. **The synthetic `channel` property** (`property/list/this.cs:57-58`).
4. **`app.type.Field`** (`type/Field.cs`, `Name` plus `TypeName` as a string) describes a type's fields (open-items #16). It stays separate for now, since `property` stays at `goal/step/action/property`.
5. **Cost, not counted yet:** every reader of the action's value list or `action.Default` as `Data`: the builder, validation, graft typing, the `.pr` writer, mock/intercept, goal.call's arguments.
6. **Coder's step-1 question** (the test `SharedRow_ReadDirectly_FailsWithNamedError` now reads the loader's context) waits on this.
