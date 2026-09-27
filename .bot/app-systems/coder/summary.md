# coder — app-systems

**Version:** v5

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the
plang path, the C# path and the file path agree. The architect's plan
(`.bot/app-systems/architect/plan.md`) lays it out in 13 stages. v1–v4 covered stages 0, 1, 3 and 4;
v5 is stage 5, faces and honest facts.

## What was done
- **v1–v4:** see `v1/result.md` … `v4/result.md`.
- **v5, stage 5** (details, commits, the eval and the decisions in `v5/result.md`):
  - Every type has a real description and example (one eval run: 58/58 on the golden goals).
  - A type written out shows its face (Out view only); type slots stay the identity.
  - The obsolete view is gone.
  - `goal.Comment` is the one description, and the hash covers comments.
  - 342 goal files put the description above the name, with the `.pr` re-saved (comment and hash only).
  - The cache merge now counts a folded body as cached.
  - `start.md` docs.
- **Results:** six suites at or under the baseline, twins equal, `plang --test` 7/0/317.
- **Waiting on Ingi:** the kind faces. A kind needs to become navigable; the cost is in `v5/plan.md`.
- **Next:** stage 6, the reference (the variable parser, `"variable"` lists in the `.pr`). Trace first.

## Code example
A type's face, and the identity every other view keeps:
```csharp
// type/this.cs
public override async ValueTask Output(IWriter writer, View mode, actor.context.@this? context)
{
    if (mode != View.Out || context == null) { Write(writer); return; }   // the identity
    var full = context.App.type.list[this, context];
    // name, description, example, alias, kind names …
}
```
