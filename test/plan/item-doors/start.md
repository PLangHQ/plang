# item-doors

**Status: design in progress.** Nothing is built.

Traces to: the conversation with Ingi on 2026-10-17, logged in `.bot/item-doors/architect/summary.md`. The branch is cut from runtime2 (c47265322), with kind-is-type merged in. It was called "branch A" until now. llm-provider and llm-writer are parked waiting for it, because their clean answers kept landing here.

## Why

Three things in the core make every new feature build around them:

1. **A value made by name gets a bare type.** It has the kind's name, but none of the kind's facts (its mime, its format). So each type patches around it: the image keeps its own copy of the mime, and text checks its own type's name.
2. **A value writes through two doors and picks the writer's door itself.** It asks `Owns(writer)` and reads the writer's string `Format`, and the writer checks what the value is.
3. **A writer can't learn what a value is,** and schemas come from switches on type names.

## Scope: the five groups, gone through one at a time

The scope isn't settled. I suggested groups 1–3, with 4–5 left for later; Ingi didn't rule on that, and said "you have 5 items in that original table, lets stick to the format". An earlier draft of this line said Ingi had chosen 1–3; he hadn't.

### 1. A type is born whole (agreed)

The type that makes a value hands itself, the registered one, to the value, which keeps it. `type`'s constructor no longer makes a kind from a name (`type/this.cs:146–149`); a type named in a step is looked up in the list. The 29 `Type` overrides go, and so do the image's `_mime` (asked of its kind), text's name floor (`source.cs:115`), the render fix's `Name: "item"` check, and the "is this an action slot?" name checks.

```csharp
// item/this.cs: the value holds the type that made it
protected internal type.@this Type { get; }                 // given at birth, never rebuilt
protected @this(type.@this type) => Type = type;

// image: no copy of the mime, no kind made from a name
public @this(byte[] bytes, type.@this type) : base(type) => _bytes = bytes;
public string Mime => Type.kind.Mime.FirstOrDefault() ?? "application/octet-stream";
```

Also from kind-is-type: the four strict-kind tests (F-SC-1), which wait on this.

### 2. One door for writing (agreed)

`Output` is the only door. Each type's own `Output` writes what it is: its one token, or its members. The sync `Write` (61 overrides) goes, and so does the `IsLeaf` branch in `Output` (`item/this.cs:906–913`; Ingi: "I like that IsLeaf check goes away").

- **The writer carries its output kind**, an object, not `string Format`.
- **`Owns` and its 5 checks go.**
- **The byte door** (844, Ingi's yes): `Bytes(byte[] value, kind kind)` replaces `Bytes(byte[])` and `Raw(byte[])`.
- **The pair registry** (846, Ingi's yes): what an input kind looks like in an output kind, when it isn't the default. json in json is copied as is; a list in formal is a list of arguments, which replaces list's `_formats`. It's looked up in one place, `data.Output`.
- **The json writer's `Value(object)`** keeps only C# primitives.

### 3. A type speaks for itself to a writer (agreed)

- **Data always tells the writer what it is, and each writer decides how much to show.** The record door already exists (`BeginRecord(data)`, `kind/json/writer.cs:69`, `text/writer.cs:84`), so the `EmitsSchema` bool (`IWriter.cs:36`) and Data's two `if`s on it (`data/this.Output.cs:83`, `:126`) go. That's 813.
  - Bare json shows only the value.
  - plang is its own output kind with its own writer: `{"type", "value", "signature"}` on the wire, plus `name` only in the Store view. The wire already leaves the name out (`data/this.Output.cs:94–99`; Ingi: "name should now be serialized on the data class" → "good").
  - The llm table learns a column's type from the cell's record (815).
- **When a type looks different in another output kind, that lives in the type's own folder** (`type/<name>/serializer/<kind>.cs`, discovered as readers are, `type/reader/this.cs:14–18`), as #2's pair-registry entry, through `IOutput` (`type/format/IOutput.cs`).
  - The first entries are each type's JSON Schema (todos.md 2026-09-23), used by llm-provider's tools: number → `"number"`, its int kind → `"integer"`, path → `"string"`.
  - plang's own word rides in JSON Schema's free `format` field (`{"type": "string", "format": "path"}`). OpenAI checks `type`, so plang's word can't go there (Ingi: "can we not use the plang types … if not that is ok").
  - `MapPlangTypeToJsonSchema` (`llm/code/OpenAi.cs:795–816`) goes.

```csharp
// data/this.Output.cs: will become
var value = Error ?? _item;                   // what this Data holds: its value, or its error (today `held`; Ingi: "name it what it is")
writer.BeginRecord(this);                     // the writer sees the Data: its type, its signature (its name in Store)
await value.Output(writer, mode, context);    // the value writes itself (#2)
writer.EndRecord(this);

// kind/json/writer.cs: bare json shows only the value
public void BeginRecord(data.@this record) { }
public void EndRecord(data.@this record) { }

// wire/kind/plang/writer.cs: NEW, plang's own writer, its own output kind
public void BeginRecord(data.@this record)
{
    _json.WriteStartObject();
    if (_view == View.Store) { _json.WritePropertyName("name"); _json.WriteStringValue(record.Name); }
    _json.WritePropertyName("type");  record.Type.Output(this, _view, record.Context);
    _json.WritePropertyName("value");
}
public void EndRecord(data.@this record)
{
    if (record.Signature is { } s) { _json.WritePropertyName("signature"); s.Output(this, _view, record.Context); }
    _json.WriteEndObject();
}

// module/llm/table.cs: a column's type from the cell itself (815)
public void BeginRecord(data.@this record) => _column ??= record.Type;

// type/item/number/serializer/schema.cs: NEW, a number's type as a JSON Schema
public sealed class schema : IOutput
{
    public ValueTask Output(item.@this type, IWriter w, View mode, actor.context.@this? context)
    {
        w.BeginObject();
        w.Name("type");   w.String("number");
        w.Name("format"); w.String(type.ToString());      // plang's own word: number<double>
        w.EndObject();
        return ValueTask.CompletedTask;
    }
}
// type/item/number/kind/int/serializer/schema.cs: "integer" · type/item/path/serializer/schema.cs: {"type": "string", "format": "path"}
```

> **For the coder: you own this code.** These are sketches of intent, agreed with Ingi piece by piece. Where the real code says another shape is better, bring it to the architect.

### 4. A type's members through its own list (agreed)

Today reading and writing a member uses C# reflection (`item/this.cs:277–311`: the clr carrier, `[LlmBuilder]` members invoked and their tasks awaited by reflection; `:209–245`: `Reflect`, `Activator.CreateInstance`, `SetValue`), not the type's own member list, `type.Property` (`type/this.cs:642`), which is what the builder offers. So the two lists can disagree. A member a value lacks throws (`:224`). `IsDefault` decides by type names and text (`type/property/this.cs:118–138`).

```csharp
// item: read and write a member through the type's own list
public virtual async ValueTask<data.@this> Get(data.@this parent, string key)
    => Type.Property?[key] is { } member ? await member.Read(this, parent) : await Setting(parent, key) ?? Kept?[key] ?? parent.Context.NotFound(key);

public virtual async ValueTask<data.@this> Set(string key, object? value, actor.context.@this context)
    => Type.Property?[key] is { } member
        ? await member.Write(this, value, context)                        // answers: Ok, or why it couldn't
        : context.Error(new error.@this($"{Type.Name} has no member '{key}'", "NoMember", 400));   // an answer, not a throw

// property: NEW. A member reads and writes itself on its owner
public async ValueTask<data.@this> Read(item.@this owner, data.@this parent) => …;
public async ValueTask<data.@this> Write(item.@this owner, object? value, actor.context.@this context) => …;

// property.IsDefault: the value asks itself, against the default made as this property's type
public async ValueTask<bool> IsDefault(item.@this? value, actor.context.@this context)
    => Default != null && value != null && await value.Compare(Type.Make(Default, context), context) == 0;
```

Reflection stays only where `property` is built from a C# class (`property/this.cs:21`). It's locked: `item`'s `Get`/`Set`, and `property`'s `Read`, `Write` and `IsDefault`.

### 5. Reading a plang value as tokens (next)

## Demolition, stays, checks, OBP validation

Written when the three groups are designed.
