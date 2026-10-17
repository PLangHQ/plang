# item-doors

**Status: design in progress.** Nothing is built.

Traces to: the conversation with Ingi on 2026-10-17, logged in `.bot/item-doors/architect/summary.md`. The branch is cut from runtime2 (c47265322), with kind-is-type merged in. It was called "branch A" until now. llm-provider and llm-writer are parked waiting for it, because their clean answers kept landing here.

## Why

Three things in the core make every new feature build around them:

1. **A value made by name gets a bare type.** It has the kind's name, but none of the kind's facts (its mime, its format). So each type patches around it: the image keeps its own copy of the mime, and text checks its own type's name.
2. **A value writes through two doors and picks the writer's door itself.** It asks `Owns(writer)` and reads the writer's string `Format`, and the writer checks what the value is.
3. **A writer can't learn what a value is,** and schemas come from switches on type names.

## Scope: groups 1–3 (Ingi, 2026-10-17)

Groups 4 (a type's members through its own list) and 5 (a structural reader over plang values) are left for a later branch.

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

### 3. A type speaks for itself to a writer (next)

## Demolition, stays, checks, OBP validation

Written when the three groups are designed.
