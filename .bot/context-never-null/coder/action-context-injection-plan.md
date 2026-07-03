# Plan: born actions WITH context (constructor injection)

**Author:** coder · **For:** architect + implementation · **Branch:** context-never-null
**Related:** builder-read-unification-plan.md, the value-type `Context` non-null (D) work.

## Problem

An action (`variable.set`, `builder.load`, …) is **constructed empty, then context is
stamped on** — the construct-then-attach smell, at the action level. Trace:

```
action/this.cs:307   (handler, _) = app.Module.GetCodeGenerated(this);
module/this.cs:116     var handler = entry.Create();
module/this.cs:611       return (ICodeGenerated)Activator.CreateInstance(Type)!;   // new load() — NO context
   load.Action.g.cs:13     public Context { get; set; } = null!;                    // ← null! here
   load.Action.g.cs:15     public Path { ... } = new Data<path>("path", (path)(".")); // default runs, Context null!
action/this.cs:336   await handler.Attach(this, context);
   load.Action.g.cs:52     Context = context;                                        // ← context set HERE, later
```

Two consequences:
1. **`Context` is `null!`** between construction and `Attach`.
2. **Field-init defaults can't born-with-context** — a property initializer runs before
   `Attach`, so a context-needing default like path `(path)(".")` is context-less. This is
   the exact blocker for making `path.Context` (and `dict`/`list`/`type`) non-null.

## Fix: inject context in the constructor (primary ctor)

Emit the action with a **primary constructor** taking context. Primary-ctor params are
visible in field initializers, so defaults born-with-context; `Context` is never `null!`.

```csharp
// generated:
partial class load(global::app.actor.context.@this __ctx)
    : global::app.module.ICodeGenerated, global::app.module.IClass
{
    public global::app.actor.context.@this Context { get; } = __ctx;
    public partial Data<path> Path { get => field!; init => field = value; }
        = new Data<path>("path", ".", context: __ctx);   // born WITH context, no (path)(".") operator
    ...
    // Resolve: pass context into the ctor
    var __instance = new load(context) { Path = __Path };
    // Attach: no longer sets Context — only wires [Code] providers + __action
}
```

Dispatch threads context to construction:
```csharp
// module/this.cs
public (ICodeGenerated?, IError?) GetCodeGenerated(action action, context context) { ... entry.Create(context) ... }
// ActionEntry.Create(context) → Activator.CreateInstance(Type, context)
```

## What this solves

- `Context = null!` gone everywhere (every generated action).
- Field-init defaults born-with-context → **removes the implicit `string→path` operator need**
  and unblocks `path.Context`/`dict`/`list`/`type` → non-null (the D work).
- Actions born-with-context — the construct-then-stamp smell dies at the action level too.
- `Attach` shrinks to "wire the runtime markers (`__action`) + `[Code]` providers".

## Scope / touch points

1. **Generator** (`PLang.Generators/Emission/Action/this.cs` + `Property/Data/this.cs`):
   - Emit the primary ctor `(context __ctx)`; `Context { get; } = __ctx`.
   - Field-init default: `new Data<T>(name, rawDefault, context: __ctx)` (drop `({T})(literal)` casts / the implicit operators).
   - Resolve: `new load(context) { … }`.
   - Attach: drop `Context = context`.
   - `__ResolveData` etc. already take `context` — unchanged.
2. **Dispatch** (`app/module/this.cs`): `GetCodeGenerated` takes `context`; `ActionEntry.Create(context)` → `Activator.CreateInstance(Type, context)`.
3. **Callers of `GetCodeGenerated`** (`action/this.cs:307,352`, builder `RunBuildPass`): pass `context` (already in scope at every site).
4. **path**: once defaults born-with-context, remove the implicit `string→path` operator,
   flip `path.Context` + ctors to non-null, delete the `: context-less stub` reader branches.
5. **Tests**: shared-`Instance` registration (`Module.Register(mod, act, instance)`) is
   **test-only** (`output.write` capture, `throwing`, `legacy`, `disposable`) — those adapt
   (construct with context / keep Attach for the mock). Object-init `new load { }` test sites
   pass a context.

## Non-goals / open questions

- **Shared-instance actions**: none in production; the mock path can keep a null-context
  Attach or be made per-call. Architect: keep `Instance` registration at all, or drop it?
- **PreboundHandler** (`action/this.cs:300`, inline C# composition) — those construct the
  handler directly; they'll pass context to the ctor too.
- Non-context field-init defaults (`text`/`number`/`bool`) don't need `__ctx` but passing it
  is harmless (their Data ctor ignores/uses it uniformly). Keep the emission uniform.
- Sequencing with the read-path unification: both are "born-with-context"; this one is the
  action layer, that one the value/deserialize layer. Independent, complementary.
