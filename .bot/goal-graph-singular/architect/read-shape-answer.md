# Read side — keep `ITypeReader` (option a). The two flags are one bug; the fix + target shape (settled w/ Ingi 2026-07-18)

Answer to `coder/to-architect-read-shape.md`. Ruling: **(a)** — `ITypeReader`/`serializer/Reader.cs` IS the canonical runtime read. Your write side (self-`Output`, `Visibility→choice`, `InputParameters` delete) stays. The three read files stay too — they just have local bugs.

> **You own this.** The reader bodies below are the shape, not the final text — field lists, the sibling-reader wiring, the exact goal-item child-list init are yours. The RULING is: keep `ITypeReader`, walk the handed `IReader` (never `new` one), no `Read<Type>` statics, never construct a collection class, readers live in the target `goal.step` namespace with their element.

## Why (a), not (b)

`goal` already reads through the type-reader registry, by name, and has since `read-path-unification` made it "one door":

```
channel/serializer/plang/this.cs:199  Read(source, ctx)
  :202  typeReader = ctx.Context.App.Type.Reader.Reader(type.Name, type.Kind?.Name, ctx.Context)   // "goal" resolves HERE
  :204  var utf8 = new Utf8JsonReader(bytes); utf8.Read();
  :206  var reader = new json.Reader(utf8);                    // the channel makes the ONE reader
  :207  return typeReader.Read(ref reader, type.Kind?.Name, ctx);   // hands it in, at the first token
```

Provenance: `goal/serializer/Reader.cs` landed in `read-path-unification` ("source.Value reads through the serializer, one door"); the actions-collection reader in `c240c92c2`; `variable`/`table`/`type`/`identity` readers all predate this branch. So reading the graph through `ITypeReader` is the **standing** mechanism a deliberate unification created — not a fork this branch invents. Retiring it (option b) would undo that unification and push a `static virtual Read` onto ~26 value types to chase a symmetry that can't exist: **write is instance (renders the `this` it holds); read constructs (there is no `this` yet).** The "one way" both sides already satisfy is *the type owns its wire, in its own folder* — `Write`/`Output` on `this.cs`, `Read` in `serializer/Reader.cs`, both colocated, the registry only selecting by `(name, kind)`.

## Your flag 3, reconciled

*"this has nothing to do with typereader, that's for types"* — the shipped code says otherwise (`goal` resolves via `App.Type.Reader.Reader("goal", …)`), so this reads as **3a: the shape you wrote wasn't a proper typereader**, not "the graph shouldn't use one." A real `ITypeReader.Read` *walks the handed `IReader`*; yours pulled `RawValue()` and re-parsed with a fresh `json.Reader`. Fix the shape and it IS a typereader — consistent with all 26.

## The two flags are one bug

`ReadGoal`/`ReadStep`/`ReadAction` are typed `ref json.Reader` statics. To feed them a concrete `json.Reader`, you did `RawValue()` → `new Utf8JsonReader` → `new json.Reader`. **The static forced the re-parse.** Delete the static → the `Read<TReader>` body walks the handed `ref reader` generically → the re-parse is gone. Both of Ingi's flags fall to one change. (It was also a latent correctness bug: `RawValue()`-then-reparse only works at the top level; a nested child-goal element re-parses per level. Walking `ref reader` in place is the `IReader` contract.)

## Corrected reader — target `goal.step` namespace, walks the handed reader, builds `List<child>`

```csharp
namespace app.goal.step.serializer;   // reader lives with its element; the 'steps' folder collapses in the sweep

public sealed class Reader : app.type.reader.ITypeReader
{
    public string Kind => app.type.reader.@this.AnyKind;

    // sibling reader for the child list. New the concrete (stateless, colocated), OR resolve once from
    // App.Type.Reader — both call Read<TReader> the same way; the channel proves interface-dispatch with a
    // ref-struct TReader works (this.cs:207). Your call.
    private readonly action.serializer.Reader _action = new();

    public app.type.item.@this Read<TReader>(ref TReader reader, string? kind, app.type.reader.ReadContext ctx)
        where TReader : app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new app.type.item.@null.@this("step", kind);

        // scalars …
        var action = new List<action.@this>();          // NOT new actions.@this() — that class is deleted (Gate 2)

        reader.BeginObject();
        while (reader.NextName(out var field))
        {
            switch (field)
            {
                // … scalar cases: reader.String()/Bool()/Long() straight off the handed reader …
                case "action":                            // wire key stays plural until the §5 sweep flips it with the writer
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Add((action.@this) _action.Read(ref reader, kind, ctx));
                    reader.EndArray();
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        return new step.@this { /* scalars … */ Action = action };   // the step item owns its list face; init takes List<action>
    }
}
```

Goal reader is the same shape one level up: `var step = new List<step.@this>()` (never `new goal.steps.@this()`), sub-goals into `var child = new List<goal.@this>()` (`Goals→Child`, D4), recursion for child goals is `(goal.@this) Read(ref reader, kind, ctx)` — the same walk, no static. Then `new goal.@this { Step = step, Child = child, … }`.

## Two corrections baked into the sketch

1. **Never construct a collection class.** `goal.steps.@this` / `actions.@this` / `modifiers.@this` delete **this increment** (Gate 2). A reader that news one compiles at Gate 1 and breaks at Gate 2. Build a plain `List<child>` and hand it to the item — children are properties now.
2. **The reader's namespace tracks its element.** Element + reader are `goal.steps.step` today and both become `goal.step` in the Gate-4 sweep, as one atomic rename — don't hand-collapse only the reader (it would reference a `step.@this` still living in the old namespace). Sketches above show the target form for readability; mechanically the sweep does the move.

## Standing rule (goes for every reader, graph or value)

**Walk the handed `IReader` generically; never `new` a `json.Reader`.** The channel already made the one reader (`this.cs:206`) and handed it in positioned at the first token. A reader that re-parses `RawValue()` is doing the channel's job and breaks on nested elements. No `Read<ConcreteType>` statics — the `Read<TReader>` body is the walk; recursion and child lists call sibling `Read<TReader>` through `ref reader`.

## Ties off two Gate-1 demolition items

- `goal/serializer/Reader.cs` — your real item reader REPLACES the `[Obsolete]` reflect-and-wrap-`clr<goal>` body; the `clr<goal>` return + `GoalReadOptions` doc-ref go with it.
- `goal/steps/step/actions/serializer/Reader.cs` — the `[Obsolete]` actions-collection reader is **deleted, not replaced**: a native list of `action` items has no bespoke collection reader; each element reads through `action`'s own reader. Verify no `(actions, kind)` registry entry is still looked up.
