# Piped Variables — Design Vision

## The Problem

PLang variables today support increasingly complex expressions:

- `%name%` — simple variable lookup
- `%user.name%` — property access (dot notation)
- `%user.addresses[0].street%` — indexed + nested property access
- `%user.name.ToUpper()%` — property access + C# method call (planned)

Currently, all of this is parsed at **runtime** — the runtime receives the raw string `%user.name.ToUpper()%` and must figure out what to do with it character by character. This gets harder to parse reliably as expressions get more complex, and there's no way to extend it beyond property/method calls.

## The Vision: Build-Time Structured Pipes

Instead of runtime string parsing, the **builder (LLM) decomposes variable expressions at build time** into a structured pipeline of typed stages. Each stage describes exactly what operation to perform.

### What the developer writes

```plang
- write out %user.name.ToUpper()%
```

### What the builder produces in the .pr file

```json
{
  "variable": "user",
  "pipes": [
    { "type": "property", "name": "name" },
    { "type": "method", "name": "ToUpper" }
  ]
}
```

### The killer feature: action pipes

The `type` field isn't limited to property/method. It can also be `action` — meaning you can pipe a value through a PLang module action:

```plang
- write out %text | llm | toupper%
```

```json
{
  "variable": "text",
  "pipes": [
    { "type": "action", "module": "llm", "action": "ask" },
    { "type": "method", "name": "ToUpper" }
  ]
}
```

This is essentially **Unix pipes meets template filters** inside PLang variable resolution.

## What This Unlocks

| Expression | Meaning |
|-----------|---------|
| `%data \| json%` | Serialize to JSON inline |
| `%html \| sanitize%` | Security as a pipe |
| `%list \| sort \| first%` | Chained collection ops |
| `%input \| llm("summarize this")%` | LLM as a transformation |
| `%text \| translate("es")%` | Translation pipe |
| `%user.name \| toupper%` | Property access + method |

## Two Levels of Composition (Separate Concerns)

### Variable-level pipes (`%...%`)

When the developer uses `%...%`, the expression inside is decomposed into a pipe structure at build time. This is resolved during parameter resolution.

**Rule: `%...%` = pipe territory.**

### Step-level action chains (no `%...%`)

When there's no `%...%`, the builder decomposes the step into multiple actions (existing multi-action chaining). This is the step execution model.

**Rule: no `%...%` = step-level action chain.**

These are separate concerns. The developer controls which path is taken by whether they use `%...%` or not.

## Design Decisions Made

### 1. Everything is async

PLang is already fully async. Variable pipe resolution is just one more async operation. Property/method pipes complete instantly (no real async overhead). Action pipes (like `| llm`) are genuinely async.

If a pipe stage fails, it produces a `Data.FromError()` and the pipe short-circuits — the step gets an error just like any other action failure.

### 2. Both dot notation and pipe syntax work

`%user.name%` and `%user | name%` would both work. The builder compiles `%user.name%` into the pipe structure at build time anyway, so the runtime gets the same pre-parsed pipeline in both cases. Less runtime parsing = faster execution.

### 3. The builder trusts the developer

The builder doesn't know at build time whether `.name` actually exists on the `user` object. It takes the developer's word for it and structures the pipe. If `.name` doesn't exist, it fails at runtime with a clear error. Same as today.

### 4. Natural language can map to pipes

PLang doesn't really have syntax — the developer writes natural language and the builder (LLM) understands intent. So:

```plang
- read file.txt to %content% with llm "summarize this"
```

The builder could map this to: read file → pipe result through llm with prompt "summarize this". The step text doesn't need explicit pipe syntax — the builder figures it out. But `%...%` is the explicit way to use pipes inline.

## Open Questions for Schema Design

### Where does the pipe definition live in the .pr file?

See [pipe-schema.md](pipe-schema.md) for proposed options.

### Parameters to piped actions

`| llm` probably needs a prompt, model, etc. How does the builder express action parameters in the pipe? Options:
- The developer writes `%text | llm("summarize this")%` and the builder maps the string arg to the module's parameter
- The builder uses its knowledge of the llm module to fill in the action parameters in the pipe structure
- Some combination

### Caching

If `%text | llm%` is inside a loop, does it call LLM every iteration? Step caching wouldn't apply here since it's inside variable resolution, not a step. Pipe-level caching might be needed.

### Error display

When a pipe fails, the error should say which stage failed and what the input was. E.g., "Pipe failed at stage 2 (llm.ask): API timeout. Input was: 'Hello world...'"
