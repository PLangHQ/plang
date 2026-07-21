# To architect — `module ⇒ action ⇒ parameter` should be a real ownership tree (action.Module is a naked string)

**Raised by:** Ingi, while reviewing `action.@this.Create(dict)` this session. He likes the direction; wants your call on whether/how to do it.

## The observation

An action holds its module as a **string**, and the registry re-resolves that string to a handler at dispatch:

```csharp
// PLang/app/goal/step/action/this.cs
public string Module { get; set; } = "";            // "variable"  ← naked string
public string ActionName { get; set; } = "";        // "set"
public string Name => $"{Module}.{ActionName}";      // string concat

// dispatch: app.Module.GetCodeGenerated(this, ctx) → RegisterType(module, actionName, type) keyed by the string
```

So the shape is flat: `action { module: "variable", name: "set", parameter: [...] }` + a stringly-typed lookup at run time. It is **not** the strict `module ⇒ action ⇒ parameter` ownership — `module` is a first-class concept (`app.module.@this`, which already owns `.Actions`/`.Modifiers` and carries catalog context) but the action references it by a bare string. That's the *flat/naked-reference* smell: a concept held as a string, re-resolved everywhere instead of owned once.

It surfaced in the dict-construction path: `action.Create(dict)` reads `d.Get("module")?.ToString()`. Ingi's framing: *"should it be a module rather than a string? the module could then load the action — module ⇒ action ⇒ …, we're not following strict execution there."*

## The strict version

The module owns action construction; an action is reached **through** its module:

```csharp
// read: resolve the module element first, hand off — the module builds/owns its action
var module = ctx.App.Module["variable"];   // a real module.@this (already exists, already owns Actions)
var action = module.Load(dict);            // module ⇒ action

// action.Module : module.@this   (Name => $"{Module.Name}.{ActionName}")
// dispatch flows through the held module element, not a string re-lookup
```

`module.@this` already exists (`PLang/app/module/this.cs`) and already mints/owns catalog actions with context. So the module-as-loader isn't a new type — it's routing construction + reference through the element that already owns the actions.

## Scope (why it's architect-sized, not a quick edit)

`action.Module : string → module.@this` touches:
- **Serialization** — the `.pr` stores `"module"` as a string. Read resolves it to the element (needs the registry/context at read time); write emits `Module.Name`. The action wire reader (`serializer/Reader.cs`) currently does `action.Module = reader.String()`.
- **Registry/dispatch** — `app.module.list` keys handlers by the `(module, actionName)` strings today; this would route through the resolved element.
- **~11 `.Module` read sites** across the codebase (mostly `a.Module == "x"` string compares and `$"{a.Module}.{a.ActionName}"`).
- **Catalog context** — a resolved `module.@this` carries the catalog context the render path already needs (this is the same context whose absence caused the stepActionDetails RenderError earlier — see `todos.md`). Routing through the module could make that context always-present by construction.

## Two seams to decide

1. **Module owns construction** — `module.Load(dict) → action` (Ingi's "the module could load action"). Strongest ownership; `action.Create(dict)` becomes a thin resolve-module-then-delegate.
2. **Action still builds itself, but holds a typed `module.@this`** — `action.Create` stays the owner, resolves `ctx.App.Module[name]` at read. Smaller blast radius; keeps the action self-constructing.

Coder lean: option 1 (module as loader) is the strict hierarchy Ingi is after, but it's the larger change. Either way the win is the same: no naked module string, no dispatch-time string re-lookup, catalog context present by construction.

## Not blocking

The current session shipped `action/step/goal.Create(dict)` with `Module` as a string (fail-loud on wrong shape) — the builder builds BuilderSanity green on it. This note is the follow-up structure, not a regression. Coder is finishing the fail-loud shape-fix + a cosmetic `%!data%` output; happy to take the `module ⇒ action` refactor once you rule on the seam.
