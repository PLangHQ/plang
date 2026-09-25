# Stage 4a — the formal writer and reader in C#

Branch `builder-formal`. Commits:
- `43b5ecaa3`: the writer.
- `bf83411af`: the catalogue filter.
- `3455313e3`: the reader.

Before them, `d5df952a1` added number coverage (python).

## Results

| check | result |
|---|---|
| the 58 golden steps: `.pr` rows → the real step reader → `formal.Writer` → python's formal | **58/58 byte for byte** (`FormalWriterTests`) |
| typed formal → `Formal` reader → writer → same text | **58/58** |
| untyped formal (the LLM's / a `.goal` step's) → reader → writer → the typed text | **58/58** |
| python's 23 parse errors → the C# twin gives the same message, line and column | **23/23** (`EveryPythonError_HasItsTwin_SameMessageLineAndColumn`) |
| an error is returned, keyed `FormalInvalid`, the fix in `FixSuggestion` | yes |
| every action holds its step; nested modifiers outermost first; `?=` round-trips | yes |
| six suites against the baseline | **no new failures** (Wire +9 new, all green; Modules: `ReflectionRead_ReproducesTheGoalGraph_LikeStj` now passes) |

The fixtures (`PLang.Tests/Wire/App/Serialization/formal_golden.json`, `formal_errors.json`) are written by `tools/decider/formal_fixture.py` from python's `formal.py`, so the python lab stays the reference.

## The code

**The writer**, `app/channel/serializer/formal/writer.cs`, is a format beside json and text.
- Values write through it unchanged (leaf pushes → formal literals; `BeginArray` → `[`; `BeginObject/Name` → `{"k": v}`).
- The program's structure is its own surface: `BeginCall(module, name)`, `Row(name, type, frozen)`, `BeginRows()` (arguments), `BeginActions()`, `BeginBody()`, `BeginWrap()`.
- It owns separators, quoting (JSON's escaping) and indentation.

**The action writes itself**, `goal/step/action/this.Item.cs`:

```csharp
if (writer is global::app.channel.serializer.formal.Writer formal)
{
    await Formal(formal, mode, context);
    return;
}
…
private async ValueTask Formal(formal.Writer writer, View mode, context? context)
{
    foreach (var m in Modifier)
    {
        await m.Call(writer, mode, context);
        writer.BeginWrap();
    }
    await Call(writer, mode, context);
    if (Child.Count > 0)
    {
        writer.BeginBody();
        foreach (var step in Child.Items())
            foreach (var action in step.Action.Items()) await action.Output(writer, mode, context);
        writer.EndBody();
    }
    for (var i = 0; i < Modifier.Count; i++) writer.EndWrap();
}

private async ValueTask Call(formal.Writer writer, View mode, context? context)
{
    writer.BeginCall(Module.Name, Name);
    foreach (var p in Property) await p.Row(writer, frozen: false, mode, context);
    foreach (var p in Default) await p.Row(writer, frozen: true, mode, context);
    if (Recovery.Count > 0)
    {
        writer.Row("Recovery", "list<action>");
        writer.BeginArray((int)Recovery.Count);
        foreach (var action in Recovery.Items()) await action.Output(writer, mode, context);
        writer.EndArray();
    }
    writer.EndCall();
}
```

**The row is the property's**, `type/property/this.cs`:

```csharp
public async ValueTask Row(formal.Writer writer, bool frozen, View mode, context? context)
{
    writer.Row(Name, Type.Kind is { } kind ? $"{Type.Name}<{kind.Name}>" : Type.Name, frozen);
    await (Value ?? @null.@this.Instance).Output(writer, mode, context);
}
```

**Bare `%x%` is the value's own decision:**
- `variable.Write` writes `%{Name}%` raw in formal.
- `source.Write` writes a whole `%ref%` raw when its declared type isn't `text`. So `Value: item = %!data%` is bare, and `Data: text = "Total: %x%"` stays quoted.

**Arguments** are a list's formal format (`type/item/list/format/formal.cs`, beside its `text` format). A list whose every element is a named Data writes `{kind: text = "x", path: item = %path%}`.

**The reader**, `goal/step/action/serializer/Formal.cs`: `new Formal(step).Read(text, context)` answers `Ok(action.list)` or the error. Typing and birth of a row:

```csharp
// the type: the declared one; in an open slot the written one, else the literal's own
var typeName = declaredFace != "item" ? declaredFace : written ?? LiteralType(value);
var type = _context.App.Type[typeName];
global::app.type.item.@this born;
if (value.Action != null) born = value.Action;                                   // an action held as a value
else if (declared.Type.Name == "list" && value.Entries != null) born = Born(type, Arguments(value));   // {k: v} → argument rows
else …
else born = Born(type, Json(value));
…
// Each value born through the type's own door — the one a .pr row's value reads through.
private item.@this Born(type.@this type, string json) { … return type.Read(ref reader, new ReadContext(_context, "plang")); }
```

A formal literal is a JSON literal (a quoted text is JSON-escaped, numbers and lists and dicts are JSON), so a value is read by `type.Read`, the same door as a `.pr` row. There's no converter per type. Templates, variables and lazy wires come out as they do from a `.pr`.

## Choices to review

1. **A modifier wrap's action order:** `Modifier.Insert(0, …)` as each wrap is read outside-in, so the list is outermost first, the order `step.Nest` produced (lowest Position first) and the writer emits.
2. **A condition's inline `{ }` becomes one child step** of the step's goal. Its text is the body's source text as written (python sets the body's untyped formal; neither is written back, since formal writes the body's actions).
3. **The reader stops at the first problem** through a private exception type, caught at `Read` and returned as the `FormalInvalid` error. No exception leaves the reader; this is control flow inside one class, not an error path.
4. **The catalogue filter changed now (`bf83411af`), not in 4c:** the reader types `channel.set(Goal=…)` and `build.fold(Goal=%goal%)` from their catalogue rows, so it had to come with 4a. Only `clr` stays hidden.

## Next

4b (the `.pr` envelope: each step's `action` is one formal string, read through `Formal`, and the JSON action rows go) starts with its check-in: the envelope's shape, and `step.Nest` / `modifier.list`.
