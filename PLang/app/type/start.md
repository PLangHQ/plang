# The type system — a C# developer's guide

A plang type is what a value is: its name, its kind, and the C# class that
implements it. This page covers the type system for the developer working on
Runtime2 in C#. It explains the two things called "type" (the entity and the
registry), how a CLR type maps to a plang type, why a file format is a kind,
and where each piece lives in the tree.

```
app.type.@this        PLang/app/type/this.cs         namespace app.type        the type entity
app.type.list.@this   PLang/app/type/list/this.cs    namespace app.type.list   the registry
app.type.root.@this   PLang/app/type/root/this.cs    namespace app.type.root   %!app.type%
```

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> Everything here answers three questions: **what is this, where is the source,
> and how does the plang I write reach this C#?** The root of the runtime is
> described in `PLang/app/start.md`.

---

## 1. Two halves, one word

The word "type" names two different things. Keep them apart and the rest of
this page reads cleanly.

| | The entity | The registry |
|---|---|---|
| C# | `app.type.@this` | `app.type.list.@this` (alias `AppTypes`) |
| Source | `PLang/app/type/this.cs` | `PLang/app/type/list/this.cs` |
| Is | one type: `text`, `number`, `image/gif` | every type, one set |
| Holds | `Name`, `kind`, `Strict`, `Template`, facts | the set; selection and lifecycle only |
| Plang | a value: `as image/gif, strict`, `set %t% = %x!type%` | `%!app.type%`, `%!app.type.image%` |

- **The entity** is a value. `type.@this` is itself an `item`
  (`[PlangType("type")]`), so a type can be authored, held in a variable and
  written into a `.pr`.
- **The registry** is a set of entities. It finds a type by name, alias, C#
  class, MIME or extension. It does not build types. Construction lives on the
  entity.

> **Rule.** The registry is selection and lifecycle only. A type-switch inside
> `type.list` is misplaced behaviour. Put it on the type, the kind, or the
> element instead.

---

## 2. The type entity

The identity of a type is four facts. The class is `app.type.@this`, and it
extends `item.@this`.

```csharp
public class @this : item.@this, item.ICreate<@this>, item.IMatch<@this>, item.ILoad<@this>,
    item.IList<@this, list.@this>

public @this(string name, string? kind = null, bool strict = false,
             global::app.type.item.template.kind.@this? template = null)
```

| Member | Meaning |
|---|---|
| `Name` | The family or primitive: `text`, `number`, `image`, `datetime`. Lowercased in the constructor. A CLR name is not a plang name: `string` and `int` are spelled `text` and `number`. |
| `kind` | The subtype refinement: `md`, `gif`, `int`. Never null. A type with no kind holds its empty kind (`kind.empty.@this`, `IsEmpty` true), which is never written. |
| `Strict` | Default `false`. Turns `kind` into a requirement. Literals are checked at build time, `%var%` values at run time. |
| `Template` | The kind of template the value is (`plang`): "may hold variables". Set by the build, never inferred from content. `null` for a plain value. |
| `format` | `new(_kind)`. The formats this type reads, as a view over its kinds. |

The entity also carries folded catalog facts (`Property`, `Values`, `Shape`,
`Description`, `Example`, `Alias`). The builder's LLM reads them. The entity
writes itself as `{name, kind?, strict?, template?}`:

```csharp
public override void Write(global::app.type.format.IWriter writer)
{
    writer.BeginObject();
    writer.Name("name"); writer.String(Name);
    if (!kind.IsEmpty) { writer.Name("kind"); writer.String(kind.Name); }
    if (Strict) { writer.Name("strict"); writer.Bool(true); }
    if (Template != null) { writer.Name("template"); writer.String(Template.Name); }
    writer.EndObject();
}
```

> **Read-only once born.** A type object is shared: a program row's declared
> type and the registry's entries are the same objects. A different kind is a
> different type object. Do not mutate a type after it is made.

---

## 3. The registry — selection only

`app.type.list.@this` is the set. Its constructor takes nothing. It holds no
context and no app. A lookup that needs the app's formats takes the caller's
context.

| You have | You call | You get |
|---|---|---|
| a name or alias | `this[string name]` | the type. Throws `KeyNotFoundException` on a miss. |
| a name, as a question | `Contains(name)` | `bool`. The presence question beside the indexer. |
| a CLR type | `this[System.Type clrType]` | the entity for the CLR type's plang identity. An unowned CLR type becomes `clr`. |
| a type with facts | `this[type, context]` | the full type: identity plus the facts its class declares. |
| a MIME | `Mime(mime)` | the kind that decodes it. An unknown MIME is opaque bytes: `binary`'s kind. |
| a MIME, name, alias or extension | `Format(named)` | the format kind, or `null` when no type reads it. |
| a file extension | `Extension(ext, context)` | the type a file of that extension holds. Unknown: `binary`. |
| a bare kind name | `Kind(string name)` | the kind. Ambiguous across two types, it throws: ask the type. |
| a type and a kind name | `Kind(string type, string? kind)` | the kind that type holds by that name, coined if absent. |
| a CLR host object | `Kind(System.Type clr)` | the `item` kind for that C# form. Never null: the `*` kind is the fallback. |
| a kind name, as a question | `HasKind(name)` | `bool`. |
| the reader for a (type, kind) | `Reader` | the per-(type, kind) read dispatch. |

> **Ambiguity is an error.** Two types may each hold a kind with the same name
> (`{hash, sha256}` and `{binary, sha256}`). Asked by the bare name,
> `Kind(string)` throws, naming both owners. A caller that holds the type asks
> the type.

---

## 4. CLR to plang — `PlangName`

The registry names a CLR type with one private step, `PlangName(System.Type)`.
It returns a `{name, kind}` pair. The entity door (`this[System.Type]`) builds
its entity from that pair, and so does every property's type.

| CLR shape | plang | Where it is decided |
|---|---|---|
| `int`, `long`, `decimal`, `double`, … | `number`, kind `int`, `long`, `decimal`, … | `item/number/this.Owns.cs`, `OwnedClrTypes` |
| `string` | `text` | `item/text/this.Owns.cs` |
| `bool` | `bool` | `item/bool/this.Owns.cs` |
| `DateOnly` | `date` | `item/date/this.Owns.cs` |
| `List<T>`, `IList<T>`, `T[]`, `list<T>` node | `list`, kind = the element: `list<path>` | `ContainerFamily` |
| `Dictionary<K, V>` | `dict`, kind = the value | `ContainerFamily` |
| enum | `choice`, kind = the set name | `item/choice/set/this.cs`, `For(clr)` |
| `Data<T>` | `T` | `PlangName`, first branch |
| plain `Data` | `item` | `PlangName`, first branch |
| a C# shape a type owns | that type | the type's `OwnedClrTypes` |
| anything unowned | `clr` | the fallback |

The ownership is distributed. Each family declares its own CLR types as a
static `OwnedClrTypes` of `convert.OwnedClr` records. Adding a CLR type a family
owns is an edit to that family alone, never to the registry.

```csharp
// PLang/app/type/item/number/this.Owns.cs
public static IReadOnlyList<global::app.type.convert.OwnedClr> OwnedClrTypes { get; }
    = new[]
    {
        new global::app.type.convert.OwnedClr(typeof(int), "int"),
        new global::app.type.convert.OwnedClr(typeof(long), "long"),
        // ...
    };
```

> **`OwnedClr.Assignable`.** When true, the family owns the CLR type and every
> subclass of it. `path` declares its scheme subclasses this way.

---

## 5. A file format is a kind of its type

A **kind** is the subtype token that names how a value is specialised. `json`,
`md`, `int` and `png` are kinds. A format is a kind that has a MIME or an
extension, so the format is not a separate store:

- `png` is a kind of `image`.
- `md` is a kind of `text`.
- `int` is a kind of `number`.

`format.@this` is a view over the type's own kinds. `Format("md")` finds the
kind by its extension, and the registry's `Format`, `Mime` and `Extension` all
walk the same kinds.

### Kind is behaviour

The kind carries the behaviour for its form. A value does not look up a
registry to decode itself. The kind's verbs are virtual, and the base class
holds the defaults. A real kind subclasses under the type it specialises and
overrides what it does.

```csharp
// PLang/app/type/kind/this.cs (abridged)
public class @this
{
    public string Name { get; }
    public bool IsEmpty => Name.Length == 0;
    public virtual IReadOnlyList<string> Mime => _mime;
    public virtual IReadOnlyList<string> Extension => _extension;
    public virtual IReadOnlyList<string> Values => null;
    public virtual bool IsClosed => false;

    public virtual item.@this? Parse(object raw, actor.context.@this ctx) => null;
    public virtual Task<data.@this> Decode(byte[] raw, ...);
    public virtual Task<data.@this> Encode(Stream stream, ...);
    public virtual object? Clr(object host, Type target, actor.context.@this ctx);
}
```

The base defaults throw "not X" for the verbs a kind does not support. A kind
overrides only what it can do. `Parse` is a single synchronous decode body: it
returns `null` to decline, and the type's reader takes over. `Decode` and
`Encode` are the I/O doors. For the serializer protocol (`IWriter`, `IReader`,
`IOutput`), see `Documentation/Runtime2/data-spec.md` §15a, §16 and §16a. This
page does not repeat it.

### The kind collections

| Type | Role |
|---|---|
| `kind.empty.@this` | the "no kind" kind, and the node holding all of a type's kinds. `Add(kind)` registers one. |
| `kind.list.@this` | a type's kinds, projected as the full types they make. `%!app.type.number.kind.list%` lists `number<int>`, `number<long>`, and so on. |
| `kind.subclass.@this` | the kind a subclass of a `[Kinds]` family is. `Owner` is the family, `ClrForm` is the class. |

---

## 6. `app.type` and `type.root` — the format in play

`%!app.type%` is the type named `type` sitting over the registry. Its class
is `root.@this`, and it is the one place the format in play is read and
written.

```csharp
public sealed class @this(global::app.@this app) : type.@this<type.@this, type.list.@this>(app)
{
    public kind.@this? Format { get; set; }         // the app's own format
    public kind.@this FormatOf(actor.context.@this context)
        => context.call.Format ?? Format ?? list.Mime("text/plain");
    public kind.@this? Named(string named) => list.Format(named);
}
```

| Member | Plang | What it does |
|---|---|---|
| `Format` | `%!app.type.format%` | the app's own format. Plain text unless the app was started with `--format=…`. |
| `FormatOf(context)` | — | the format in play for the asker: the nearest calling goal's, else the app's, else text. |
| `Named(named)` | — | the format a name selects. |
| `Get` | dot-walk | `format` reads the format in play. Any other key reads the registry's node. |
| `Set` | `set %!app.type.format% to …` | sets the format for the calling goal's frame, until it returns. A format no type reads is refused with `FormatNotFound` (400). |

Format is set per call frame, not per app. A goal that sets it holds for the
goals it calls. Channels such as the console read and write in the format in
play.

---

## 7. Property and the catalog

`property.@this` is PLang's `PropertyInfo`. It is a named, typed slot. It serves
both a type's navigable members and an action's parameters.

| Member | Meaning |
|---|---|
| `Name` | the slot's name, lowercased first letter |
| `Type` | an `app.type.@this`. Consumers read `Type.Name`, never a `System.Type`. |
| `Nullable`, `Required` | `Required` is `!Nullable && !HasDefault` |
| `Arguments`, `IsMethod` | the parameters of a callable member. `IsMethod` is `Arguments != null`. |
| `Default`, `HasDefault` | the value used when the step omits the slot |
| `IsInput` | the slot is marked `[Input]` |
| `Data(context)` | the run's own `Data` for this slot. A property holds no `Data` of its own. |

`property.list.@this` is a read-only list of properties. It has three sources,
chosen by its constructor:

- the **program** list, filled by the `.pr` reader and the builder;
- a **type's** list, from the record members of its class;
- the **catalog** list, `new(module, actionName)`, which reflects an action's
  declared parameters lazily. Catalog reflection skips the framework slots the
  LLM must never author: `[Code]`, capability interfaces, `EqualityContract`,
  and non-`Data` slots.

---

## 8. `current<T, L>` — the list and the current

A concept execution runs inside has both a `.list` and a `.current`. The
generic that gives both is `type.current.@this<T, L>`.

```csharp
public sealed class @this<T, L> : type.@this<T, L>
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ICurrent<T>, item.ILoad<T>, item.IList<T, L>
    where L : item.list.@this<T>
{
    public data.@this<T> current(actor.context.@this context)
        => T.Current(context) is { } one ? new data.@this<T>("current", one, context: context)
            : data.@this<T>.FromError(new Error($"no {Name} is current", "NotFound", 404));
}
```

It is the base for `app.goal`, `app.actor`, `app.test` and `app.error`. A concept
nothing is ever inside has no current. It uses `type.@this<T, L>` instead. This
is the collection-node pattern from `PLang/app/start.md` §4, in code.

`Get` on a current node reads, in order: `current`, the node's own members, the
type's facts, then the current element's member. A dot therefore reaches the
running goal's name as `%!goal.current.Name%`.

---

## 9. Birth and make

A value comes into being two ways. The distinction matters, because only one
of them fires events.

| | Entry point | Fires `on.create`? |
|---|---|---|
| **Birth** | `type.@this.Create(raw, context)` | yes. `before` gets the raw. `after` gets the value. |
| **Make** | `type.@this.Make(raw, context)` (internal) | no. |

- A **birth** is a value coming into being from outside the type system: an
  action making a value, or a file's bytes decoded into a new value.
- A **make** is a re-type inside the type system: a lazy value materialising,
  or a `.pr` slot being read.
- A value that already *is* this type is answered as it is. Nothing is fired.

The entity's instance `Create` hides (via `new`) the static apex
`item.@this.Create(raw, ctx)`, which means "build whatever the raw is". The
apex is covered on the `item` page.

A separate static form makes a *type* from what names it:

```csharp
public static @this? Create(object? raw, @this? declared, global::app.data.@this data)
```

A text is the type it names (`path`). A dict describes one
(`{name: "number", kind: "int", strict: true}`). Anything else declines with
`CreateItemDeclined` (400).

---

## 10. Find a type's source

| You have | You want | Where |
|---|---|---|
| a plang type name (`text`, `number`) | the entity's identity | `app.type.list.@this` → `this[string]` → `PLang/app/type/item/<name>/` |
| a CLR type (`int`, `List<T>`) | its plang name | `PlangName` in `PLang/app/type/list/this.cs`; the owned shapes in `item/<family>/this.Owns.cs` |
| a kind name (`md`, `png`) | its kind class | `PLang/app/type/kind/this.cs` and the kinds under `item/<family>/` |
| a format (`application/plang`, `.md`) | the kind that reads it | `Format(named)` and `Mime(mime)` in `PLang/app/type/list/this.cs` |
| the format in play | the value | `PLang/app/type/root/this.cs`, `FormatOf` |
| a type's property | its slot | `PLang/app/type/property/this.cs` |
| the type's teaching prose | the markdown the builder reads | `os/system/type/<type>/` (see the Notes and Guide members in `this.cs`) |
| the rule for the serializer | the spec | `Documentation/Runtime2/data-spec.md` §15a, §16, §16a |

---

## The tree from here

| Doc | Covers |
|---|---|
| `PLang/app/type/item/start.md` | the value apex, `item.@this`, `item.list`/`item.dict`, and the scalar families |
| `PLang/app/start.md` | the app root, the collection-node pattern, and the reading fundamentals |
| `PLang/app/module/start.md` | the module system, which dispatches actions over these types |
