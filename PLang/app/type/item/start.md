# The item value lattice

Every value in Runtime2 is an `item`. A number, a piece of text, a list, a
dict, a path, a date: each is a class that derives from `item.@this`. This page
is for the C# developer who needs to know what a value *is* before touching
the code that moves one around. It covers the apex, the one door that lifts a
raw CLR value into a value, the native `list` and `dict`, the scalar types,
how a value's kind and rank work, and where each type's source lives.

```
app.type.item          PLang/app/type/item/          namespace app.type.item
app.type.item.list     PLang/app/type/item/list/     namespace app.type.item.list
app.type.item.dict     PLang/app/type/item/dict/     namespace app.type.item.dict
```

> **Scope.** This is the value side only. How a type is *named* and *found*
> (the type list, kinds as a family, formats) is the type page, `../start.md`.
> How a value is *carried* (`Data`, the wire shape, signing) is in §8. The
> `os/system/...` teaching markdown is the PLang user's side and is not
> repeated here.

---

## 1. The apex: `item.@this`

`item.@this` is the base of every value. It is roughly C#'s `object`.

```csharp
namespace app.type.item;

[global::app.Attributes.PlangType("item")]
public abstract class @this : global::app.data.IBooleanResolvable, ICreate<@this>
```

The apex follows three rules.

- **It is storage-free.** The apex owns no value field. Each subtype keeps its
  own backing: an `int`, a `string`, a `List<object?>`. The base carries
  behaviour only, so a narrowed subtype never inherits dead state.
- **It is the only family a value slot admits.** A `Data<T>` slot can say
  `where T : item`, so a raw CLR value never sits in a value slot.
- **Its universal contract is small.** Truthiness, the lazy narrow, and the
  door (`Value`). Ordering and equality are opt-in interfaces
  (`IOrderableValue`, `IEquatableValue`). The apex implements neither, because
  `dict` would otherwise inherit an order it cannot honour.
- **Its members are the value's own surface.** The ones a developer meets
  most:

| Member | What it answers |
|---|---|
| `Create(object? raw, context)` | the inferred lift: any raw value to an item (§2) |
| `Value(Data)` | "I am going to use this value, make yourself ready." May answer as a different type |
| `Rank` | comparison precedence; higher drives (§7) |
| `Compare(other, context)` | the reconcile between two values; non-virtual |
| `IsTruthy()` / `AsBooleanAsync(context)` | truthiness; the async form is where I/O truthiness (a path's existence) lives |
| `IsLeaf` | `true` for a scalar that rides the wire as one bare token |
| `Get(parent, key)` / `Set(key, isIndex, value, context)` | read and write a child by name |
| `Output(writer, view, context)` | write itself to the wire, through the format-neutral `IWriter` |
| `Clr(Type)` | the CLR exit door; internal, see the note below |

> **The CLR exit door is internal.** `Clr(System.Type)` is the one place a
> value hands its backing to a CLR caller. It is engine plumbing for the typed
> boundary, not part of the plang-facing surface. A lossy conversion throws
> rather than defaulting. `number` throws `FractionalToInteger` when a
> fractional value narrows to an integral target.

`Value(Data)` is the door, and the default returns the value itself. A value
that must be loaded or parsed overrides it. A `file` loads its bytes and
answers with the parsed `dict`. Failure is reported on the `Data` the caller
holds (`data.Fail(error)`), and the door answers `Absent`.

---

## 2. `item.Create`: the inferred lift

`item.@this.Create(object? raw, context)` answers the question "build whatever
this raw is". It is the apex's own `ICreate` face, used when no target type is
declared. It always returns a value, never `null`.

The rungs run in order, and each one is a navigation rather than a `switch`
over a type list.

| Rung | The raw is | The answer |
|---|---|---|
| 1 | `null` | `null.@this.Instance` |
| 2 | already an `item` | itself (idempotent) |
| 3 | a bare `Data` | throws. A `Data` is never stored as a value |
| 4 | a sequence of `Data` or `item`, `List<object?>`, `Dictionary<string, object?>`, or any other `IEnumerable` except `string` and `byte[]` | a native `list` or `dict`, stored raw |
| 5 | `System.Enum` | `choice<TEnum>`. A CLR enum is plang's closed set |
| 6 | anything else | `context.App.type.list[raw.GetType()].Make(raw, context)`: the owning type's own creator |

Rung 6 is the single terminal rung. The type list answers, for a CLR type,
the type entity that owns it, and that entity builds the value. A CLR type no
value type owns rides a `clr` carrier.

The declared ask is a different door. `App.type.list[name].Create(raw, ctx)`
builds the type the caller named, and that is the type page's subject.

### Walk-throughs

**A raw `int`.** `5` is not null, not an item, not a `Data`, and not a
sequence. It reaches rung 6. The entity that owns `int` is `number`, and its
pure core keeps the CLR kind (`int v => v`). The result is a `number` whose
kind is `int`.

```csharp
// rung 6, the owner builds it
context.App.type.list[typeof(int)].Make(5, context);   // a number, kind int
```

**A raw `string`.** `"hello"` reaches rung 6. The owner is `text`, which
stores the string as its `_value`. A string that reads as a number is still a
`text` here. The `number` pure core is what decides a string's literal shape
later, in comparison.

**A raw `List<object?>`.** `new List<object?> { 1, "two" }` reaches rung 4.
It becomes a native `list` that aliases the same `List<object?>` as its
backing. No copy is made, so a million-row source is O(1). The elements stay
raw until something reads them (§3).

> **Rung order is the contract.** Rung 4 comes before rung 6 on purpose. A
> read-only domain collection (an `IReadOnlyList<T>` such as a module list)
> would otherwise fall through to the entity door, which answers `list` but
> cannot build one. Narrowing first is what keeps that from looping.

---

## 3. How `item.list` works

`item.list` is the native plang list. It is the type behind every `.list` a
developer navigates in the app. It lives at
`PLang/app/type/item/list/this.cs`:

```csharp
[global::app.Attributes.PlangType("list"), global::app.Attributes.Kinds]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
```

It has alias `array` and **Rank 750**, the highest ranked type after `null`.

### Store raw, type on read

The backing is `List<object?> _items`. A slot holds *either* a raw CLR value
(a literal read off the wire, or a native sub-container) *or* a `Data` or
`item` (a value that carries its own type and signature). The list never
converts a slot to a `Data` and writes it back. When a reader asks for a
slot, the list hands out a fresh `Data` born with the asker's context. The
backing stays pristine, so enumeration is safe and an aliased source keeps its
identity.

```csharp
// the reader's view: each slot is born into a Data as it is reached
public IEnumerable<Data> Items(actor.context.@this context)
{
    var rows = Now;
    for (int r = 0; r < rows.Length; r++)
    {
        if (rows[r] is Chunk chunk)
            foreach (var e in chunk.List.Items(context)) yield return e;
        else yield return Element(r, rows[r], context);
    }
}
```

`Items(context)` is a yield, not a build. Nothing is materialised up front. A
chunk row yields its own list's items inline.

### Chunks: extend in O(1)

A `Chunk` is a row that holds another list. `Add(list)` appends a chunk and
does not read the existing rows. The public surface (`Count`, `Items`, `At`,
enumeration) is the *element* view. A chunk contributes its list's elements,
and every other row contributes one element, whatever its value. A list-valued
row that is not a chunk is one element that happens to be a list. The row
structure is never visible to a caller.

`Count` is a `number` walked on demand, not a stored counter. A chunk aliases a
list that may change elsewhere, so a cached count would go stale.

### Mutation and snapshots

Every mutation goes through one seam, under the list's private `_gate` lock.
Readers walk a snapshot. `Now` returns an array copied once after a change, and
shared by every reader until the next change. A reader takes the lock only when
the snapshot is stale, so no reader ever sees a list mid-change.

### Construction

| Source | Path | Note |
|---|---|---|
| `IEnumerable<Data>` | `new list(items)` | each element keeps its own `Data` |
| `IEnumerable<item>` | `new list(values)` | values are wrapped as needed |
| `List<object?>` | `new list(raw)` | **aliases** the list as backing, no copy |
| a scalar value | `list.Create(...)` courier | wraps one value as a list of one; empty from blank |

### The query surface

The query methods go through the one comparison path (§7), so `sort` and
`if a > b` agree. The verbs that build a new list say so: the receiver is
unchanged.

| Method | Returns | Behaviour |
|---|---|---|
| `Sort(by, descending)` | `Task<Data>` | a new list, ordered by each element or by its `by` field. Nulls sort last. Equal keys keep their order |
| `Unique()` | `Task<Data>` | a new list with duplicates dropped, equal through the compare path |
| `Flatten()` | `Task<Data>` | a new list with every nested list's elements lifted in place, however deep |
| `Group(key, …)` | `Task<Data>` | groups elements by a key |
| `Where(field, op, value)` | `Task<Data>` | the elements that match a field comparison |
| `Any(field, op, value)` | `Task<Data>` | whether any element matches a field comparison |
| `Join(separator)` | `Task<Data>` | the elements written as one text, separated |
| `Index(needle)` | `number` | the position of the first match |
| `Contains(needle)` | `bool` | membership, by element equality through the compare path |
| `At(index)`, `First`, `Last`, `Random` | `Data?` | one element by position |
| `Add`, `SetAt` | `list` or `Task<Data>` | extending and positional writes |
| `IsSequence` | `true` | the list holds its elements by position |
| `IsTruthy()` | `Count > 0` | an empty list is falsy |

The `Sort` body shows the pattern every query follows. Each element is resolved
through the door first, then the keys are ordered:

```csharp
// list.Sort: resolve every key through the door, then order them
var keys = new Dictionary<Data, (Data key, object? value)>(ReferenceEqualityComparer.Instance);
foreach (var d in flat)
{
    var key = by == null ? d : await d.Get(by.ToString());
    keys[d] = (key, await key.Value());
}
// ... SortAsync over OrderOf(ka, kb, va, vb)
return await context.App.type.list["list"].Create(sorted, context);
```

### A domain `X.list` is an `item.list<X>`

This is the tie back to the app page (`../../start.md` §4 and §6). Every
collection a developer navigates is a native list of its element type. The
module registry shows the pattern (`PLang/app/module/this.cs`):

```csharp
public global::app.type.item.list.@this<@this> list => Root._modules!;
```

`item.list.@this<T>` is a generic subclass that adds no storage and no
behaviour. It exists so a slot can name its element type (`list<module>`), and
the element type lives in the type rather than beside it. Everything else
(the serializer, the navigators, comparison) is the untyped base. So
`app.module.list`, `app.goal.list`, and `app.error.list` are all the one native
list type, tagged with their element. That is why plang has no plurals: the
collection is always reached by navigating to `.list`.

```
%!app.module.list%   ->  app.module.list   ->  list<module>   ->  each element is a module
```

---

## 4. `item.dict`: the native dict

`item.dict` is the symmetric twin of the list, for keyed values. It lives at
`PLang/app/type/item/dict/this.cs`.

```csharp
[global::app.Attributes.PlangType("dict")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
```

It has aliases `dictionary` and `map`, and **Rank 700**: above every scalar,
below `list`. A dict is equality-only. It does not honour ordering, so it does
not implement the ordering contract.

The backing is `Dictionary<string, object?>`, which keeps insertion order. A
dict that plang builds itself is case-insensitive.

| Member | Answers |
|---|---|
| `Keys` | the keys, as a `list<text>` |
| `Has(key)` | key membership |
| `Get(key, context)` | the `Data` at a key, or `null` |
| `Get<T>(dottedPath, context)` | a typed read along a dotted path |
| `Set(key, value)` | writes a key |
| `Entries(context)` | each entry as a `Data` |
| `Contains(needle)` | key membership, as the generic `Contains` |
| `IsTruthy()` | `Count > 0` |

The dict overrides the apex's child navigation, so `%d.name%` is a key lookup
and not a reflection of the class.

---

## 5. The scalar value types

Each scalar is a sealed class with one backing field. Every one derives from
`item.@this`, and every one implements `ICreate<T>` so its pure core can
decline a raw value (returning `null`) rather than throwing. The courier owns
the error channel.

| Type (PLang name) | Class | Backing | Rank | Notes |
|---|---|---|---|---|
| `number` | `item/number/this.cs` | a boxed CLR numeric, `object _value` | 300 | the kind is the storage size (§6) |
| `text` | `item/text/this.cs` | `string _value` | 100 | the floor of the ranked types; atomic, not enumerable |
| `bool` | `item/bool/this.cs` | `bool Value` | 200 | the truthiness floor |
| `null` | `item/null/this.cs` | a singleton, `Instance` | 1000 | always drives in a comparison |
| `datetime` | `item/datetime/this.cs` | `DateTimeOffset` | 550 | the date family, §5 |
| `date` | `item/date/this.cs` | `DateOnly` | 500 | the date family, §5 |
| `time` | `item/time/this.cs` | `TimeOnly` | 450 | the date family, §5 |
| `duration` | `item/duration/this.cs` | `TimeSpan` plus a kind | 400 | the kind is the written standard |
| `choice<T>` | `item/choice/this.cs` | `T Value` | 150 | a closed set: a CLR enum, or a class that declares one |
| `path` | `item/path/this.cs` | a verbatim location string | unranked (0) | a location only, no content |

Every one of these overrides `IsLeaf` to `true`, except `path`, which reflects
its own members.

### `number`

A `number` is one boxed CLR numeric. Its `Kind` is the storage size it is held
in, and it is a `number.kind.@this` instance (§6). The pure core accepts a raw
numeric (each CLR numeric type has an implicit operator), an item that unwraps
to a number, or a string, which goes through `Parse`. Anything else declines to
`null`. A string that is not a number, such as `"abc"`, declines and is not an
error.

### `text`

`text` is the string type. It is atomic on purpose: it does not implement
`IEnumerable`, so iteration never walks characters. Its `Value` and `Output`
render `%ref%` templates when they are reached. Text also carries a kind
(`item/text/kind/`), which decides how it is walked.

### `bool`

`bool` has two singletons, `True` and `False`. Its implicit operators let a C#
`bool` lift to plang without allocation, and `operator true` and
`operator false` let `if` and `while` read a `bool` without downgrading it to
the CLR type. Its ordering is equality only: `<` and `>` throw.

### `null`

`null` is a process-wide singleton, `null.@this.Instance`. It also serves as
`item.Absent`, the value a failed door answers with. It has `Rank` 1000, so a
comparison with `null` always resolves in `null`'s favour, and `IsTruthy()` is
`false`.

### The date family

`datetime`, `date`, and `time` each wrap one .NET date type. `datetime` wraps a
`DateTimeOffset` and exposes its parts (`Year`, `Month`, `Day`, `Hour`, and so
on) as `number` values, plus compound accessors (`Date`, `Time`, `Offset`).
`date` wraps a `DateOnly`, and `time` wraps a `TimeOnly`.

### `duration`

`duration` wraps a `TimeSpan`, and it carries a kind. The kind is the written
standard the value was born in. The three kinds are the `short`, `iso`, and
`dotnet` folders under `item/duration/kind/`, and each writes the same span in
its own notation. `%d.text%` writes the value in its kind. The whole-unit
accessors (`Days`, `Hours`, `Minutes`, and so on) each return a `number`.

### `choice<T>`

`choice<T>` is a sealed generic over the set's type. Its backing is `T Value`.
The closed set is a per-`T` static `set.@this` under `item/choice/set/`. An enum
is a choice: at rung 5 of `item.Create`, a `System.Enum` becomes
`choice<TEnum>`. `choice` is equality-only, and its `Type` is
`{choice, kind: <set name>}`.

### `path`

`path` is abstract. Its backing is a verbatim location string, with no content.
The scheme is the path's *kind*: every scheme variant answers `is path`, and
`Scheme` is the one property that tells them apart. `Resolve(raw, context)`
parses the scheme (a bare path is `file`) and builds the right subclass (`file`
or `http`). An unregistered scheme throws `SchemeNotRegistered`. Live
filesystem and network state (existence, stat, the HTTP HEAD) lives on the
subclasses under `item/path/file/` and `item/path/http/`, and the file module
page covers the verbs (`../../module/file/start.md`).

---

## 6. Kind for a value: `number.kind.int`

A **kind** names how a value is held, below its type. A `number` is one type,
and its kind says which storage size it uses. The kind is a separate class, and
it is stateless behaviour: it knows how to create, write, and read a number of
its size.

The abstract base is `number/kind/this.cs`:

```csharp
public abstract class @this : global::app.type.kind.@this
{
    protected internal override string Owner => "number";
    public abstract global::app.type.item.number.@this Create(global::app.type.item.@this value);
    public abstract void Write(global::app.type.item.number.@this value, global::app.type.format.IWriter writer);
    public abstract global::app.type.item.@this Read<TReader>(ref TReader reader);
}
```

The base is shown abridged. There are fifteen concrete kinds, one folder each
under `number/kind/`:
`biginteger`, `byte`, `decimal`, `double`, `float`, `half`, `int`, `int128`,
`long`, `sbyte`, `short`, `uint`, `uint128`, `ulong`, `ushort`. The `int` kind
is a whole class:

```csharp
namespace app.type.item.number.kind.@int;

public sealed class @this : global::app.type.item.number.kind.@this
{
    public @this() : base("int") { }
    public override IReadOnlyList<string> Alias => ["integer"];
    public override global::app.type.item.number.@this Create(global::app.type.item.@this value) => value.Clr<int>();
    public override void Write(global::app.type.item.number.@this v, global::app.type.format.IWriter w) => w.Int(v.ToInt32());
    public override global::app.type.item.@this Read<TReader>(ref TReader r) => (global::app.type.item.number.@this)r.Int();
}
```

So `number.kind.int` reads as "a number stored as a 32-bit int". The kind's
`Create` lowers the value through its own `Clr` door, and `Write` emits the
kind's own token.

A value answers `is` for its kind. `%n% is int` asks the value, through
`IsKind`. A number is every kind it fits, not only the one it is held in:

```
5     is int and long      (a whole number in both ranges)
2.5   is decimal, not int  (a fractional value is not an integer kind)
```

This is the rule behind the comment on `number/this.Ladder.cs`: the ladder owns
the relation between sizes (which one an overflow climbs to), and a value owns
its own fit.

> The kind mechanism is shared with the other families. `text` has kinds by
> format (`json`, `md`), `duration` has kinds by written standard, and `path`
> has kinds by scheme. The type page (`../start.md`) covers the family and
> kind registry.

---

## 7. Rank and coercion

When two values meet in a comparison, one of them has to drive. The rule is
**higher rank drives, lower rank coerces up** through its own `Create`.

`Compare` is non-virtual, and it is two lines. The higher-ranked side calls
`Order`, and when the right operand drives, its result is inverted back into
caller order:

```csharp
public async ValueTask<Comparison> Compare(@this other, context)
    => Rank >= other.Rank
        ? await Order(other, context)
        : ComparisonExtensions.Invert(await other.Order(this, context));
```

Per-type behaviour lives in `Order`, which is `protected virtual`. The driver
coerces `other` into its own kind through the pure `Create` core, and orders
the two in caller order.

The ranks, from the overrides in the source. A type without an override is
unranked, which is `0`.

| Rank | Type | Declared in |
|---|---|---|
| 1000 | `null` | `item/null/this.cs` |
| 750 | `list` | `item/list/this.cs` |
| 700 | `dict` | `item/dict/this.cs` |
| 600 | `guid` | `item/guid/this.cs` |
| 550 | `datetime` | `item/datetime/this.cs` |
| 500 | `date` | `item/date/this.cs` |
| 450 | `time` | `item/time/this.cs` |
| 400 | `duration`, `size` | `item/duration/this.cs`, `item/size/this.cs` |
| 350 | `status` | `item/status/this.cs` |
| 300 | `number` | `item/number/this.Equality.cs` |
| 250 | `binary` | `item/binary/this.cs` |
| 200 | `bool`, `base64` | `item/bool/this.cs`, `item/base64/this.cs` |
| 150 | `choice` | `item/choice/this.cs` |
| 110 | `tag` | `item/tag/this.cs` |
| 100 | `text` | `item/text/this.cs` |
| 0 | the apex, `path` | `item/this.cs` (the default) |

`text` is the floor of the ranked types. Every type with an override outranks
it, so `"5" == 5` drives in `number`, and the text `"5"` is coerced up through
`number.Create`. `tag` sits just above `text` on purpose, so a tag-shaped text
coerces into a tag.

Two consequences follow from the table.

- `null` always drives, so `null == x` is decided by `null` and never by
  coercing `null` into `x`.
- A value with no ranked order, such as a `path` or a plugin type that does not
  declare a rank, compares by identity. Its `Order` answers `Equal` or
  `NotEqual` for the same instance, and a plugin type declares its own rank
  when it needs one.

The source comments call this the `×10` table. A new type declares its own rank
and takes its place in the same table.

---

## 8. Data vs item

`Data` (`app.data.@this`) is the **carrier**, and `item` is the **value** it
carries. A typed slot is `Data<T>` where `T : item`. The carrier holds the
name, the context, the signature, and the lazy resolution. The value holds
what the thing is.

Three rules keep the two apart.

- **A `Data` is never stored as a value.** `item.Create` throws on a bare
  `Data` (rung 3, §2). A producer that has a `Data` returns its inner value
  through that value's own factory.
- **A courier forwards `Data`. Only a leaf opens it.** The layers that pass a
  `Data` along (variable memory, the callstack, a channel) do not read its
  value. A handler with a typed `Data<T>` slot may read it.
- **A value's door is `Value(Data)`.** A holder asks the value to make itself
  ready through the door. The holder does not open the value by hand.

For the wire model (the shape of `Data`, `[Out]`, `[Store]`, signing, and the
serializer), read `../../start.md` §7 and
`../../../../Documentation/Runtime2/data-spec.md`. This page does not repeat
them.

---

## 9. Find a value type's source

| You want | Look at |
|---|---|
| the apex contract, the raw lift | `item/this.cs` (`Create`, `Value`, `Rank`, `Compare`, `Get`, `Set`, `Output`) |
| the native list | `item/list/this.cs`, and `item/list/this.Generic.cs` for `list<T>` |
| the native dict | `item/dict/this.cs` |
| a number and its kinds | `item/number/this.cs`, `item/number/kind/this.cs`, `item/number/kind/<name>/this.cs` |
| the number ladder and overflow | `item/number/this.Ladder.cs` |
| the number rank and equality | `item/number/this.Equality.cs` |
| text | `item/text/this.cs` |
| bool | `item/bool/this.cs` |
| null | `item/null/this.cs` |
| the date family | `item/datetime/this.cs`, `item/date/this.cs`, `item/time/this.cs` |
| a duration and its kinds | `item/duration/this.cs`, `item/duration/kind/<name>/this.cs` |
| a choice and its set | `item/choice/this.cs`, `item/choice/set/this.cs` |
| a path and its schemes | `item/path/this.cs`, `item/path/file/this.cs`, `item/path/http/this.cs` |
| a type's rank | `grep -rn "Rank =>" PLang/app/type/item/` |
| a type's alias | the `Alias` property on the class (`list` is `array`, `dict` is `dictionary`, `map`, `text` is `string`, `bool` is `boolean`) |

---

## The tree from here

Each page lives in the folder it describes.

| Page | Covers |
|---|---|
| `../start.md` | the type system: the type list, kinds as a family, formats (the parent) |
| `../../start.md` | the app root, the collection-node pattern, `Data` in the graph |
| `../../module/start.md` | the module system: discovery, action handlers, dispatch |
| `../../module/file/start.md` | the `file` module, end to end: the path verbs in use |
