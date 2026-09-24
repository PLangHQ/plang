# `app.X` is the X system, and it describes itself

With Ingi, 2026-09-24. **PARKED by Ingi 2026-09-24 ("a big change, we dont need it now"). NOT for coder.** Everything settled below stands for when it is picked up.

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

## Settled with Ingi: the system is the concept's own class, one element takes the doubled name

Rejected: calling the systems `registry` (Ingi doesn't like the name) and keeping `list/`. Chosen: truly one to one. **`X/this.cs` is the X system, reached at `app.X`; one X moves to the doubled name `X/X/this.cs`** (the architecture has accepted a doubled name before: `App.FileSystem.Permission.Permission`). **Pilot on `type`** (the pass after the current queue), then decide goal, actor, module and test with that experience. Cost of moving the element class (references by full name): `app.type.@this` 306, `app.goal.@this` 83, `app.test.@this` 92, `app.actor.@this` 16, `app.module.@this` 8.

The type system, approved by Ingi ("looks pretty good, no objections"). Existing members are marked with their line in today's `type/list/this.cs`:

```csharp
namespace app.type;

// type/this.cs — the app's TYPE SYSTEM (today: type/list/this.cs, type.list.@this). Reached at app.type.
public sealed partial class @this
{
    // the types: ONE set of elements (one type = type/type/this.cs, app.type.type.@this)
    // NEW: two private lookups over the same elements, replacing the five parallel maps in
    // Registry.cs (_nameToType, _typeToName, _runtimeNameToType, _clr, _clrTypeFullNames, :27-39) and
    // CatalogByName (:129-131). Each element owns its name, aliases, C# mates and facts.
    private readonly Dictionary<string, type.@this> _byName = new(StringComparer.OrdinalIgnoreCase);   // NEW
    private readonly Dictionary<System.Type, type.@this> _byClr = new();                                // NEW

    [Out] public IEnumerable<type.@this> list => _byName.Values.Distinct();      // NEW — %!app.type.list%

    // its other parts (all here today)
    [Out] public item.choice.list.@this Choice { get; }              // :40  — %!app.type.choice%
    [Out] public item.path.scheme.@this Scheme { get; } = new();     // :48  — %!app.type.scheme%
    internal kind.list.@this Kind { get; private set; }              // :56
    public reader.@this Reader { get; } = new();                     // :75
    public renderer.@this Renderer { get; } = new();                 // :66  (vestigial)

    // selection
    public type.@this this[string name] { get { … } }                // :174 — "string" → text, "text/markdown" → {text, md}
    public type.@this this[type.@this type] { get { … } }           // :205 — the full type for an identity
    public type.@this? this[System.Type clr] { get { … } }           // :240
    public bool Contains(string name) => …;                          // :117

    // lifecycle
    public void Add(type.@this type) { … }        // NEW — the one way to add a type (replaces Register :403,
                                                  //   RegisterRuntime Registry.cs:100 and the static Loader); the guard lives here
    private void Load(IEnumerable<Assembly> assemblies) { … }   // NEW name for the startup scan (IndexAssembly etc.)
}
```

| plang | C# | file |
|---|---|---|
| `%!app.type%` | `App.Type` | `type/this.cs`, the type system |
| `%!app.type.list%` | `App.Type.list` | a member |
| `%!app.type.text%` | `App.Type["text"]` | `type/type/this.cs`, one type |
| `%!app.type.choice%` | `App.Type.Choice` | `type/item/choice/list/this.cs` |

The type system keeps a context (infrastructure, not an item): it's used for its kinds and for canonicalising kinds through the formats. Inside namespace `app.type`, `type.@this` means one type; outside, `app.type.type.@this` or an alias.

Also coming into the type system (ruled 2026-09-24, queued for coder): `app.Type.Mime(mime)`, `app.Type.Extension(ext)` and a kind's family through `app.Type.Kind[…]`, moved off the format registry.

## Open

1. ~~The system class's name and place~~: settled above.
2. ~~`.list` on every system~~: in the sketch.
3. **The type-system cleanup, same class, done together** (in the sketch): the five parallel maps in `type/list/Registry.cs` (`_nameToType`, `_typeToName`, `_runtimeNameToType`, `_clr`, `_clrTypeFullNames`, `:27-39`) plus `CatalogByName` collapse into one set of type elements, each owning its name, aliases, C# mates and facts. The collection keeps private lookups by name and by C# type. `Registry.cs` (a partial of the list, `:23`) dissolves into `this.cs` or a `this.<aspect>.cs`. `Loader.cs` (`public static class Loader`, `:30`, the plugin loader, #26) is a static and a second way to add a type at runtime; adding a type is the collection's `Add`.
4. Which facts each system's face shows.

## Later piece (Ingi, 2026-09-24): registering systems on `%!app%`

Not needed now. Ingi wants others to be able to extend `%!app%`: register a mapping `%!app.XXX%` → a system.

- **Today** `%!app%` is reflection over the C# `App` object (`actor/this.cs:90`, `new data.DynamicData("!app", () => app, Context)`), so plang reaches any public `App` property.
- **Then** `%!app%` is the app system holding its registered systems. `%!app.list%` gives the registered names. Built-ins register themselves at startup (type, goal, module, actor, test, event, format, service, setting, code, build, debug, …), and a plugin loaded with `code.load` registers its own (`%!app.stripe%`). `write out %!app%` describes what the app has.
- This also answers the systems that live under `module/action/` (code `:141`, build `:191`, debug `:179`, cache `:155`): the registered name gives `%!app.code%`, and the folder needn't match.
- **Rules proposed:** one name, one system (a plugin can't take a built-in name; it fails loudly). Register the object (a system that answers navigation and writes itself), not a lambda. An unknown `%!app.foo%` is a plang error (developer-caused), not an exception. C# keeps its typed properties (`App.Type`), and the registered name is plang's face of the same object.
- **`%setting.X%` stays as a short form (Ingi, two ways on purpose).** `variable.list.RegisterNavigable(name, resolver)` (`variable/list/this.cs:30`) is used once, for `%setting.X%` (`actor/this.cs:87`). With app mounts, settings are also `%!app.setting.X%`, and both stay.
