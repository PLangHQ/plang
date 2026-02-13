# Pipe Schema — Design Options

This document captures the open schema design questions for piped variables. The goal: a structured pipe definition in the .pr file that the runtime can execute without string parsing.

---

## Pipe Stage Types

Every pipe is an array of stages. Each stage has a `type`:

| Type | Meaning | Example |
|------|---------|---------|
| `property` | Access a property/key on the current value | `.name` |
| `index` | Access by index | `[0]`, `[idx]` |
| `method` | Call a C# method on the current value | `.ToUpper()`, `.Trim()` |
| `action` | Pipe through a PLang module action | `\| llm`, `\| json` |

---

## Example Decompositions

### Simple variable
```plang
%name%
```
```json
{ "variable": "name", "pipes": [] }
```

### Property access
```plang
%user.name%
```
```json
{
  "variable": "user",
  "pipes": [
    { "type": "property", "name": "name" }
  ]
}
```

### Nested with index
```plang
%user.addresses[0].street%
```
```json
{
  "variable": "user",
  "pipes": [
    { "type": "property", "name": "addresses" },
    { "type": "index", "value": 0 },
    { "type": "property", "name": "street" }
  ]
}
```

### Property + method
```plang
%user.name.ToUpper()%
```
```json
{
  "variable": "user",
  "pipes": [
    { "type": "property", "name": "name" },
    { "type": "method", "name": "ToUpper" }
  ]
}
```

### Method with arguments
```plang
%text.Substring(0, 10)%
```
```json
{
  "variable": "text",
  "pipes": [
    { "type": "method", "name": "Substring", "args": [0, 10] }
  ]
}
```

### Action pipe
```plang
%text | llm%
```
```json
{
  "variable": "text",
  "pipes": [
    { "type": "action", "module": "llm", "action": "ask" }
  ]
}
```

### Action pipe with parameters
```plang
%text | llm("summarize this in 3 bullets")%
```
```json
{
  "variable": "text",
  "pipes": [
    {
      "type": "action",
      "module": "llm",
      "action": "ask",
      "parameters": [
        { "name": "prompt", "value": "summarize this in 3 bullets" }
      ]
    }
  ]
}
```

The input to the action (the current pipe value) would be passed as the action's primary/first parameter (or a designated "input" parameter).

### Chained action pipes
```plang
%article | llm("summarize") | toupper%
```
```json
{
  "variable": "article",
  "pipes": [
    {
      "type": "action",
      "module": "llm",
      "action": "ask",
      "parameters": [
        { "name": "prompt", "value": "summarize" }
      ]
    },
    { "type": "method", "name": "ToUpper" }
  ]
}
```

### Mixed: property + action
```plang
%user.bio | llm("translate to spanish")%
```
```json
{
  "variable": "user",
  "pipes": [
    { "type": "property", "name": "bio" },
    {
      "type": "action",
      "module": "llm",
      "action": "ask",
      "parameters": [
        { "name": "prompt", "value": "translate to spanish" }
      ]
    }
  ]
}
```

---

## Where Does the Pipe Definition Live in the .pr File?

### Option A: On each parameter (alongside value)

The pipe definition is attached directly to the parameter that contains the variable reference.

```json
{
  "module": "output",
  "action": "write",
  "parameters": [
    {
      "name": "Content",
      "value": "Hello %user.name | toupper%, your balance is %balance | format_currency%",
      "type": "string",
      "pipes": {
        "user.name | toupper": {
          "variable": "user",
          "pipes": [
            { "type": "property", "name": "name" },
            { "type": "method", "name": "ToUpper" }
          ]
        },
        "balance | format_currency": {
          "variable": "balance",
          "pipes": [
            { "type": "action", "module": "format", "action": "currency" }
          ]
        }
      }
    }
  ]
}
```

**Pros:** Self-contained — everything needed to resolve a parameter is right there.
**Cons:** Duplicated if the same pipe expression appears in multiple parameters.

### Option B: On the action level

All pipe definitions for all parameters of an action in one place.

```json
{
  "module": "output",
  "action": "write",
  "parameters": [
    {
      "name": "Content",
      "value": "Hello %user.name | toupper%",
      "type": "string"
    }
  ],
  "pipes": {
    "user.name | toupper": {
      "variable": "user",
      "pipes": [
        { "type": "property", "name": "name" },
        { "type": "method", "name": "ToUpper" }
      ]
    }
  }
}
```

**Pros:** No duplication across parameters. Single lookup table.
**Cons:** Slightly less self-contained.

### Option C: Replace value entirely with structured format

Instead of keeping the human-readable string with `%...%`, the parameter value becomes the pipe structure itself.

```json
{
  "name": "Content",
  "value": [
    { "literal": "Hello " },
    {
      "variable": "user",
      "pipes": [
        { "type": "property", "name": "name" },
        { "type": "method", "name": "ToUpper" }
      ]
    },
    { "literal": ", welcome!" }
  ],
  "type": "string"
}
```

**Pros:** No regex parsing at all — fully structured. Fastest runtime.
**Cons:** Harder to read in .pr files. Breaking change to parameter format. Mixed literal+variable strings become arrays.

---

## Runtime Execution Model

The pipe executor receives the pipe definition and processes stages sequentially:

```
Input: value from MemoryStack (the root variable)
For each stage in pipes:
    switch stage.type:
        "property" → navigate to property (ValueNavigators)
        "index"    → navigate to index (ValueNavigators)
        "method"   → call C# method via reflection
        "action"   → execute PLang action (async), pass current value as input
    If error → short-circuit, return Data.FromError()
    Current value = stage output
Output: final transformed value
```

### Method resolution

For `{ "type": "method", "name": "ToUpper" }`:
1. Get the current value's CLR type
2. Find method by name (case-insensitive) via reflection
3. If args provided, convert them via TypeMapping
4. Invoke and use the return value as next pipe input

### Action resolution

For `{ "type": "action", "module": "llm", "action": "ask", "parameters": [...] }`:
1. Look up the handler from ActionRegistry
2. Build the action parameters — inject the current pipe value as the primary input
3. Execute via Engine (async)
4. Use the Data.Value result as next pipe input
5. If Data.Error → short-circuit the entire pipe

---

## Backward Compatibility

The pipe system should be **additive**:

- If a parameter has no `pipes` field, resolution works exactly as today (regex-based in source-generated code)
- If a parameter has a `pipes` field, the runtime uses the structured pipe executor instead
- The `value` field still contains the human-readable string for debugging/display
- Old .pr files (without pipes) continue to work unchanged

This means the pipe system can be rolled out incrementally — the builder starts producing pipe definitions for new builds, while old .pr files still work.
