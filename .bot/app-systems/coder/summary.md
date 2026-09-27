# coder — app-systems

**Version:** v4

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree. The architect's plan
(`.bot/app-systems/architect/plan.md`) lays it out in 13 stages.
- v1: plan review and stage 0.
- v2: stage 1 (`Start`).
- v3: stage 3 (one set of types).
- v4: stage 4 (the collected type).

## What was done
- **v1–v3**: see `v1/result.md`, `v2/plan.md`, `v3/result.md`.
- **v4, stage 4** (details, commits and decisions in `v4/result.md`):
  - `item.history`.
  - Strict `list<T>`.
  - `type<T, L>` with `list`, `Get(key)` (async, 404 on a miss) and `current(context)`; `IMatch`/`ICurrent`/`IList`.
  - The types are a `list<type>` behind one `Admit` guard, holding no context.
  - Kinds live on their types.
  - `app.type` is the type named `type` (`%!app.type%`), its `list` the types.
  - Programmer-written type names ask `await app.type.Get`.
- **Results:** six suites at or under the baseline, prompt twins byte-equal, `plang --test` unchanged, no timing regression.
- **Next:** stage 5, faces and honest facts. Trace first.

## Code example
The app's types, as C# reaches them:
```csharp
// app/this.cs
public global::app.type.@this<global::app.type.@this, global::app.type.list.@this> type { get; }
type = new(this);
type.list.Replace(type);   // %!app.type% and the list's entry named type are one object

// a name the programmer wrote — a miss is a result, not a throw
var named = await context.App.type.Get(typeName);
if (!named.Success) return Refused(context, $"Unknown type '{typeName}'", "UnknownType");

// a lookup by another key, on the list
var type = context.App.type.list.Mime(mime, context);
```
