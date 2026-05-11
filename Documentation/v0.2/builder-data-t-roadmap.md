# Builder Data<T> Roadmap

The builder sends action descriptions to the LLM via system prompt. Now that all action
properties are `Data<T>`, the builder needs to:
1. Show clean types to the LLM (e.g., `path` not `Data<path>`)
2. Tell the LLM what complex types look like (schema)
3. Stamp correct types on .pr parameters during validation

## Step 1: See current state

Run `plang p build --debug` on a simple .goal file and inspect the system prompt
sent to the LLM. Focus on:
- How action parameters are described (the output of `Modules.Describe()`)
- What type names appear for Data<T> properties (e.g., does `file.read` show `path` or something else?)
- How complex types (GoalCall, LlmMessage) appear
- Whether type schemas are sent alongside type names

Key files:
- `PLang/App/Modules/this.cs:150` — `Describe()` method, builds parameter list via reflection
- `PLang/App/Utils/TypeMapping.cs:192` — `GetTypeName(Type)`, maps CLR types to PLang names
- `PLang/App/Utils/TypeMapping.cs:662` — `GetComplexTypeSchemas()`, discovers and describes complex types
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

## Step 3: Ensure LLM knows what types look like

The LLM sees type names like `path`, `llmmessage`, `goal.call`. It needs to know
what to put in the value field. Types fall into two categories:

### String-valued types (no schema needed)
These are resolved from a plain string value at runtime. The LLM just writes a string.
- `path` — file path string, resolved via `Path.Resolve()` at runtime
- `actor` — actor name string, resolved at runtime

The type name in the .pr file tells the runtime how to resolve the string.
Example: `{ name: "Path", type: "path", value: "/data/file.txt" }`

### Structured types (schema needed)
These have internal structure the LLM must produce correctly. They already have
`[LlmBuilder]` attributes on their properties, and `GetComplexTypeSchemas()` discovers
and describes them.
- `llmmessage` — `{ role: string, content: string }` (has `[LlmBuilder]`)
- `goal.call` — `{ name: string, description: string, ... }` (has `[LlmBuilder]`)

### Types that may need attention
- `Path` has NO `[LlmBuilder]` attributes — but it doesn't need them since it's
  string-valued. However, `GetComplexTypeSchemas` might try to add it as a complex
  type with an empty schema. Verify this doesn't confuse the LLM.
- Any new Data<T> types added in the future: if T is string-valued, no schema needed.
  If T has structure, add `[LlmBuilder]` to its properties.

## Step 4: Validate stamps correct types on .pr parameters

When the LLM returns its action mapping, the builder's `validate` step should:
- Look at each parameter in the LLM response
- Match it against the action schema (from `Modules.GetActionType`)
- If the schema property is `Data<Path>`, stamp `type: "path"` on the parameter
- This happens in the default `IBuilder.Validate()` (`App/modules/builder/code/Default.cs`) or `NormalizeParameterTypes()`

Currently `NormalizeParameterTypes` converts string values like `"false"` → `false`.
Extend it to also stamp the correct PLang type name from the schema.

Key: the .pr file should have `{ name: "Path", type: "path", value: "/some/file.txt" }`
so the runtime knows to resolve it as `Data<Path>`.

The type on the .pr parameter drives the source generator's `__ResolveData().As<T>()`
conversion at runtime. Without the correct type, the value stays a raw string.

## Step 5: Verify end-to-end

Build a simple .goal file, check:
- The .pr file has correct types on all parameters
- Runtime resolves parameters correctly from the .pr data
- Data<T> properties get the right inner type at execution time
