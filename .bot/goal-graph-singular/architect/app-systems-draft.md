# `app.X` is the X system, and it describes itself

With Ingi, 2026-09-24. **Draft, being designed with Ingi. NOT for coder yet.** Planned as a pass after births 3.6, together with the type-system cleanup below (same class).

## Why

Ingi wants the plang path, the C# path and the file path to map one to one. Today they don't for `app.type`:

| | `app.type` | `app.type.list` | one type |
|---|---|---|---|
| Ingi's plang reading | singular: "one type" | the list of types | `app.type["text"]` |
| C# | the collection (`App.Type` is `type.list.@this`, `app/this.cs:202`) | (type has no `.list` member; `goal.list` has one, `goal/list/this.cs:259`) | `App.Type["text"]` |
| files | `type/list/this.cs` | — | `type/this.cs` |

The same holds for every concept (`App.Goal` → `goal.list.@this` `:146`, `App.Actor` → `actor.list.@this` `:211`, `App.Module` → `module.list.@this` `:135`).

Brainstormed and rejected: making `app.type` one type with `.list` hanging off it (the collection would live on the element); a separate concept-node class holding `List`/`Current` (the wrapper the architecture removed); moving every concept's files (one element needs a new home, and every concept moves).

## The insight (Ingi)

`App.Type` is already more than its types. It holds the kinds (`Kind`, `type/list/this.cs:56`), the path schemes (`Scheme`, `:48`), the choice sets (`Choice`, `:40`), the readers (`Reader`, `:75`), and answers lookups by name, by type and by C# class (`:174, :205, :240`). **It is the app's type system: one thing.** So the singular reading holds:

- `%!app.type%` → the type system (one object, in plang and in C#)
- `%!app.type.list%` → all the types
- `%!app.type.text%` / `app.type["text"]` → one type
- `%!app.type.kind%`, `.scheme`, `.choice`, … → its other parts
- a concept that execution flows through also has `.current` (`%!app.goal.current%`)

The real collision is the word **list**: the type system's class lives at `type/list/this.cs` and is called `type.list.@this`, while `app.type.list` is the types inside it. "list" names both the system and its items.

## The system describes itself

Today `write out %!app.type%` fails: the class declares no `[Out]` face, so the reflection writer throws `NoWireContract` (`type/item/kind/reflection/this.cs:247-253`).

As the type system, it writes **its own face**: a description of the whole system, a sketch:

```json
{
  "list":   [ { "name": "text", "description": "…", "kinds": ["md", "csv", "…"] },
              { "name": "number", "description": "…", "kinds": ["int", "long", "double", "decimal"] }, "…" ],
  "scheme": ["file", "http", "https"],
  "choice": [ { "name": "actor", "values": ["system", "user"] }, "…" ]
}
```

**Through the Output door, like any other item (Ingi).** The system writes itself through `Output(writer, mode, context)`. The format is whatever writer is in use at the time (json, text, plang, …), never JSON specifically.

**The same for the others (Ingi):** `%!app.goal%` (its goals, and which one is running), `%!app.module%` (the modules with their actions), `%!app.actor%`, `%!app.test%`, `%!app.event%`, `%!app.format%`, …

## Rules

1. **A face is a summary; navigation gives the detail.** `%!app.goal%` lists names and short facts, not every goal with its steps; `%!app.goal.Start%` goes deeper.
2. **Facts, not prompts.** The system writes its facts. The builder's LLM prompt stays a template that renders them (serialization is the value writing itself; presentation belongs to templates). The builder's separate catalogs (`type/list/view`, the module/action catalog, the menu) become the systems' own faces, rendered by templates.
3. **Only what's declared goes out.** Only `[Out]`-tagged members are written (an untagged class throws); secrets are `[Sensitive]`. Each face is chosen deliberately: settings, identities, permissions.

## Open

1. **The system class's name and place,** so it isn't called "list": the class reached at `app.X`. Today it's `X/list/this.cs` for every concept.
2. **`.list`** as the items member on every system (type has none today).
3. **The type-system cleanup, same class, done together:** the five parallel maps in `type/list/Registry.cs` (`_nameToType`, `_typeToName`, `_runtimeNameToType`, `_clr`, `_clrTypeFullNames`, `:27-39`) plus `CatalogByName` collapse into one set of type elements, each owning its name, aliases, C# mates and facts. The collection keeps private lookups by name and by C# type. `Registry.cs` (a partial of the list, `:23`) dissolves into `this.cs` or a `this.<aspect>.cs`. `Loader.cs` (`public static class Loader`, `:30`, the plugin loader, #26) is a static and a second way to add a type at runtime; adding a type is the collection's `Add`.
4. Which facts each system's face shows.
