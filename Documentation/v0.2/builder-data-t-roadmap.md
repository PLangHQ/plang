# Builder Data<T> Roadmap

The builder sends action descriptions to the LLM via system prompt. Now that all action
properties are `Data<T>`, the builder needs to:
1. Show clean types to the LLM (e.g., `path` not `Data<path>`)
2. Stamp correct types on .pr parameters during validation

## Step 1: See current state

Run `plang p build --debug` on a simple .goal file and inspect the system prompt
sent to the LLM. Focus on:
- How action parameters are described (the output of `Modules.Describe()`)
- What type names appear for Data<T> properties (e.g., does `file.read` show `path` or something else?)
- How complex types (GoalCall, LlmMessage) appear

Key files:
- `PLang/App/Modules/this.cs:150` — `Describe()` method, builds parameter list via reflection
- `PLang/App/Utils/TypeMapping.cs:192` — `GetTypeName(Type)`, maps CLR types to PLang names
- `system/builder/*.goal` — the PLang builder goals that construct the LLM prompt

## Step 2: Fix Describe to unwrap Data<T>

`Describe()` at line 169 calls `TypeMapping.GetTypeName(prop.PropertyType)`.
Since properties are now `Data.@this<Path>`, this returns the wrong name.

Fix: `GetTypeName` should unwrap `Data.@this<T>` → `T`, similar to how it already
unwraps `List<T>`, `Nullable<T>`, and arrays. Add a check:

```
if (generic == typeof(Data.@this<>))
    return GetTypeName(type.GetGenericArguments()[0]);
```

Also handle plain `Data.@this` → `"object"`.

After this, the LLM sees `path`, `string`, `list<llmmessage>` — not Data wrappers.

## Step 3: Validate stamps correct types on .pr parameters

When the LLM returns its action mapping, the builder's `validate` step should:
- Look at each parameter in the LLM response
- Match it against the action schema (from `Modules.GetActionType`)
- If the schema property is `Data<Path>`, stamp `type: "path"` on the parameter
- This happens in `DefaultBuilderProvider.Validate()` or `NormalizeParameterTypes()`

Currently `NormalizeParameterTypes` converts string values like `"false"` → `false`.
Extend it to also stamp the correct PLang type name from the schema.

Key: the .pr file should have `{ name: "Path", type: "path", value: "/some/file.txt" }`
so the runtime knows to resolve it as `Data<Path>`.

## Step 4: Verify end-to-end

Build a simple .goal file, check:
- The .pr file has correct types on all parameters
- Runtime resolves parameters correctly from the .pr data
- Data<T> properties get the right inner type at execution time
