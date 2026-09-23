# Births pass — every value is born with its context; the type object is the registry's own

Designed with Ingi 2026-09-23. Numbers from coder's inventory (`coder/births-inventory.md`, `dff9f80a7`, a Roslyn pass over the real compilation).

> **You (coder) own this.** Rules and the order are settled; shapes, names and commit split are yours. Code below is direction, marked NEW where it does not exist.

## Why

A plang value today is usually born without its context. Two things break because of it:

- **The type object can't reach the registry.** Every item builds a bare copy of its own type (`Type => new("goal", typeof(@this))`, 34 overrides). Asked for more than its name — a choice's values, a record's fields — it reaches for the registry from inside itself (`Promote()`), has no context, and throws (`type/this.cs:593`), or falls back to a static table (`type/this.cs:163` → `GetPrimitiveOrMime`). This is why the menu can't show a choice's values yet.
- **Context is nullable where it exists.** `list` and `dict` declare `private actor.context.@this _context = null!;` (`type/item/list/this.cs:176`, `type/item/dict/this.cs:136`) — non-nullable in type, null in practice.

Ingi: "items should have their own context — it's just a reference; what stopped us was injecting it everywhere." This pass injects it everywhere, so that a value's context is a private, non-nullable birth fact, and a value's type is the one the registry holds.

## The three rules (Ingi, 2026-09-23)

1. **Errors are created by the context.** An error is an item (`error/Error.cs:22`). Nearly every one is built and immediately handed to `context.Error(...)`, which has the context — so the context creates it.
2. **A value made from another value takes that value's context.** A list's count takes the list's; `a + b` takes `a`'s.
3. **Something with no context of its own hands over its raw fact; the asker mints the value with the asker's context.** Program nodes (the graph and its lists) are context-free by law. Example: `{% if a.Parameter.size > 0 %}` (`actionFormal.template:1`) reaches the parameter list's `Count => CountRaw` (`list/this.cs:188`, an implicit int → number with no context). The navigation door that got there carries the asker's context (`type/item/this.cs:235-237`, `new clr.@this(this, parent.Context)`), so the value is minted there. The engine keeps reading typed C# internals and never mints.

## The doors (from the inventory)

| Door | Production sites | Has context | This pass |
|---|---|---|---|
| Readers + json parser | 68 | yes | — |
| Typed ask `Create(raw, data)` | 39 | yes | — |
| Result doors `context.Ok(raw)`, `new Data(name, raw)` | 150 (+~800 generated) | yes | — |
| Pure core `T.Create(object? raw)` | 4 callers (+29 self-delegations) | no | takes the context (`ICreate.cs:31-40`'s "context-free on purpose" has no real reason: a comparison runs inside a run) |
| Implicit conversions INTO an item | 237 (number 187, bool 41, text 31, choice 10, duration 5, binary 3) | no, and C# can't give them one | die |
| Direct `new` of a value | 120 | no | a result door, or `T.Create(raw, context)` |
| Direct `new` of an error | 270 (+547 generated, one template) | no | created by the context (rule 1) |
| Direct `new` of a domain item | 61 | no | case by case, by the rules |
| `Type => new(...)` overrides | 34 (+2 base) | no | ask the registry |
| `new app.type.@this(...)` outside the registry | 7 sites | no | the registry |

Tests: ~430 direct `new`s (grep lower bound) — repointed by the same doors.

## How code reads afterwards

```csharp
return Data(result);                               // a handler result — already context-born (generated Data(raw) => Context.Ok(raw))
var n = number.Create(5, context);                 // the type known in C#: the type's own Create, with the context
var v = context.App.Type[name].Create(raw, context);// the type known only by name (a .pr row, the LLM's answer, `as text`)
return context.Error(/* NEW: the context creates the error */);
```

`new X(...)` of a value appears only inside X's own `Create` and reader.

## The structural decision — where the context field lives

Program nodes (goal, step, action, modifier, and their lists) are items but **context-free by law** — shared across runs. So the context cannot sit on the item base. Recommended:

- **Values** (number, text, bool, path, list, dict, choice, errors, …) hold `private readonly actor.context.@this _context`, set by their constructor, non-nullable, never stamped.
- **Program nodes** hold no context field at all; they hold their birth facts (parent, App) and reach the registry through their App.
- **The list collision.** The value `list` holds a context, while the program lists (`action.list`, `step.list`, `parameter.list`, `tag.list`) derive from it and deliberately hold none (`list/this.cs:106-113`). With a non-nullable context the program lists can't derive from the value list. Proposed: the list's behaviour (rows, chunks, iteration, index, Output) sits in a context-free base; the value `list` adds its context; the program lists derive the base. **Ingi to confirm** — it decides the first commit.

## Demolition — what must not survive

- Every implicit conversion INTO an item (text `:222`, number `:83-…`, bool, choice `:47-49`, duration, binary). Conversions OUT stay (they create nothing).
- `ICreate`'s context-free pure core `Create(object? raw)` as a context-free door (it takes the context).
- `type.@this`'s `Context` property and `Promote()` with its throw; `ClrType`'s static fallback (`type/this.cs:163`).
- The static primitive table and `GetPrimitiveOrMime`, `ClrFromMime` (open-items #14 — this pass is their executioner).
- The 34 `Type => new(...)` overrides (items ask the registry); `new app.type.@this` outside the registry.
- `_context = null!` in list and dict; every `Context ??=` / post-construction `.Context = …` stamp on a value.
- `test.Create` as a static factory (open-items #24) — the test collection mints its tests.
- `action.ReturnTypeName` as a string (open-items #15) — the return type is the type object.

**Stays:** program nodes context-free with their birth facts; the result doors; the readers; the typed ask; conversions OUT; `ICreate`'s static `Create` members (C#-mandated statics).

## Order — each step compiles and is its own commit

1. **The structural split** (after Ingi's confirmation): the context-free list base; values get the context constructor parameter beside today's constructor (both exist during the migration).
2. **Errors** — the context creates them; the generator's error template changes once (547 generated sites).
3. **Implicit conversions IN** — number, bool, text, choice, duration, binary, one type per commit; each call site becomes a result door or `T.Create(raw, context)` (rule 2 where it is made from another value).
4. **Direct `new` of values and domain items** — by file, busiest first (OpenAi, assert, Ed25519, file operations, build, http…).
5. **The pure core** takes the context.
6. **Remove the context-less constructors** — the compiler now proves no site is left; `_context = null!` dies.
7. **The type object is the registry's own** — items ask the registry (`Type` overrides die), the registry mints each type once holding itself, `Promote()`/static fallback/primitive statics die; the menu template gets choice values.
8. **#15 and #24** — the return type object; the test collection mints its tests.
Tests move with the door they use, in the same commits. Six suites by name after each.

## OBP validation

| Surface | Check |
|---|---|
| value's `_context` | private, non-nullable, constructor-set — no late stamp |
| program nodes | no context field; birth facts only (the graph law) |
| context-free list base + value list | one behaviour, two birth kinds — not a fork: the base has no context-dependent member |
| `T.Create(raw, context)` | the type owns its creation; one door per type |
| `context.Error(...)` creating the error | the context owns error birth; no `new` + hand-over |
| registry `Type[...]` | selection + lifecycle; each type object minted once |
| names | no new names beyond the context parameter; nothing verb+noun |
