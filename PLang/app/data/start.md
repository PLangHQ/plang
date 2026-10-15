# Data — the universal carrier

Every value in Runtime2 travels as `Data`. A variable, a file's content, an
HTTP response, an action's result, an error: each one is a `Data` that holds an
`item` and the name, context, properties, and failure around it. This page is
for the C# developer who needs to know what `Data` is before touching code that
passes a value from one place to another. It covers the shape, the typed slot,
the result API, navigation, properties, and the courier rule that governs who
may open a value.

```
app.data.@this        PLang/app/data/this.cs         namespace app.data
app.data.@this<T>     PLang/app/data/this.cs         the typed slot (same file)
app.data.Properties   PLang/app/data/Properties.cs   the metadata bag
app.data.Wire         PLang/app/data/Wire.cs         the wire reader
```

> **Scope.** This page places `Data` in the graph. The value it carries is
> `item` (`../type/item/start.md`). The wire format, signing, and the full
> serializer model are in `../../../Documentation/Runtime2/data-spec.md`. The
> app root that owns the graph is `../start.md`, §7.

> **Locked core.** `Data` is part of the locked core: its public surface does
> not change without an approved decision. Read the surface below as the
> current contract.

---

## 1. One class, not an envelope

`Data` is a single class. It has no base class of its own beyond `object`, and
it is `partial`: the result API lives in `this.Result.cs`, navigation in
`this.Navigation.cs`, output in `this.Output.cs`.

```csharp
namespace app.data;

public partial class @this
```

The fields a developer meets most:

| Member | Answers |
|---|---|
| `Name` | the binding's name (`a` in `%a%`) |
| `Item` (internal) | the typed value, an `item.@this`. Never C# `null` |
| `Type` | the value's `type` entity, forwarded from `_item` |
| `Context` | the actor context the Data was born with |
| `Properties` | the metadata bag (§5) |
| `Error` | the failure, or `null` |
| `Success` | `Error == null` |
| `Signature` | who signed the Data on arrival. `null` for a Data born here |
| `IsInitialized` | `false` for a miss (no value was ever given) |
| `Parent`, `Path` | where the Data sits under its parent (`config.items[0]`) |

The value slot is never `null`. Absence is a typed citizen: `IsInitialized`
says whether a value was given, and `null.@this` holds a present null
(`../type/item/start.md` §5).

> **Data is the wire shape, not an envelope.** The serialized form of a `Data`
> is `{name, type, value, properties, signature}`, written by `Data` itself. Do
> not add a parallel wrapper type to carry something `[JsonIgnore]` keeps off
> the wire. Tag the member that should travel instead (§5).

---

## 2. `Data<T>`: the typed slot

`Data<T>` is the same carrier with a declared value type. The constraint is
`where T : item, ICreate<T>`, so a typed slot can only hold a plang value:

```csharp
public class @this<T> : @this
    where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
```

A handler declares its parameter as `Data<T>`, and the source generator reads
the declared type from that slot (see `../module/start.md`). The slot is still a
`Data`. It adds the typed door.

- **`Value()`** on the typed slot answers `T?`. It opens the door and then asks
  the value to convert itself to `T` through `ICreate<T>.Create`. A decline is
  recorded on this slot as `Error`, and the answer is `null`.
- **`Use(then)`** hands the typed value whole to a continuation, or returns the
  failed slot if it did not resolve (§4).
- **`From(source)`** retypes a base `Data` as `Data<T>` without re-building the
  value. Use it to forward a failure across a typed boundary:
  `if (!source.Success) return Data<T>.From(source);`.

> **Footgun.** When `T = object`, the implicit operator
> `@this<T>(T value)` wraps a `Data` passed as the value, so
> `Data<object>{ Value = innerData }` double-wraps. Return a typed result from
> `Data<object>` through `data.@this<object>.Ok(value)`, not `return innerData;`.
> The constructor throws when a bare `Data` is stored as a value, so the bad
> wrap fails loud.

---

## 3. The result API

A result is a `Data`. A success has no `Error`. A failure has one, and nothing
else about the Data needs to change.

| Member | Behaviour |
|---|---|
| `Ok()`, `Ok(value, type?)` | a Data holding `value` (or the null value for `Ok()`), no error |
| `FromError(error)` | a Data whose `Error` is `error`. Its `Success` is `false` |
| `Fail(error)` (instance) | records `error` on this Data. A door or `Create` that fails reports here |
| `Success` | `true` when `Error == null` |
| `Error` | the failure, or `null` |
| `implicit operator bool` | `if (data)` reads `Success` |

`Fail` stamps the error's `Context` when the error has none, so a failure says
where it happened. `FromError` does not.

### Observation is the guard

Reading `Success` or `Error` marks the failure as observed. An error that no
one reads is surfaced by the generator's post-`Run` epilogue, which answers the
action with the error. A handler that never looks at a failed parameter cannot
swallow it. The epilogue reads `HasUnobservedError` without observing.

### What `Success == false` means

A failed Data is a stop signal, not a crash. The step loop asks
`ShouldExit()`, which is true when any of these hold:

- the Data has failed and no before-event has `Handled` it
- `Returned` is set (an explicit `goal.return`, which `ReturnDepth` carries
  across goal boundaries)
- the value is an `IExitsGoal` (an `ask` that waits for an answer)

`Handled` is the before-event's override. A failed Data that is `Handled` does
not stop the loop on its failure. The step loops (`../goal/step/list/this.cs`,
`../goal/step/action/list/this.cs`) also break on `Handled` directly.

> **A program's error answers; it does not throw.** Where a result can be
> returned, the method answers `Fail` or `FromError`. A throw is for places
> that cannot return a result (a constructor, a stream callback). The throw
> carries the `Error` whole, as `AppException(error)`.

---

## 4. Navigation: how a Data reaches a child

Navigation is the path from a value to a part of it: `choices[0].message`,
`[0]`, `!type`. The Data does not parse the path itself.

```csharp
public async ValueTask<@this> Get(string path)
{
    var parser = new global::app.type.item.variable.parser.@this(path);
    if (parser.Path() is not { } code)
        return _context?.Error(parser.Error[0]) ?? NotFound(path);
    return await code.Start(Context, this);
}
```

- **`Get(path)`** parses the path with the variable parser
  (`../type/item/variable/` — see `../type/item/start.md`) and starts the
  resulting hops from this Data. It never returns `null`. A path that does not
  parse answers the parse error. A path that does not reach answers the reason.
- **`Set(key, isIndex, value, keeps)`** writes a child. It materialises a
  source-backed value first, so the write lands on the parsed value. A child the
  value refuses (`NotSupportedException`, or an `AppException` from the child)
  is answered as an error on the Data, not thrown into the caller.

### Lazy `%var%` resolution

A value may be a reference: `IsVariable` is true when the whole value is one
`%name%`. Three methods read a reference without opening its value:

| Method | Use it when |
|---|---|
| `Follow(ctx)` | you want the Data a reference names, value still shut |
| `Settle()` | you want what a reference or template resolves to, as it reads now |
| `Held()` | you want the value opened, with a reference followed |

`Value()` is the door. It asks the item to make itself ready: a file loads, a
template renders, a source parses. The Data keeps the answer only when the item
says it is `Cacheable`. Await the door once per call site.

```csharp
var answer = await data.Value();        // the door: the item decides how it loads
```

---

## 5. Properties: the metadata bag

`Properties` is a per-Data key/value bag. It carries facts about the Data that
are not the value itself: a file's size, a provider's status, a tag. It is
case-insensitive, and its keys are unconstrained.

```csharp
public sealed class Properties : IEnumerable<KeyValuePair<string, object?>>
```

- **`%x!key%` reads `Properties[key]`.** `%x.field%` reads the value's shape.
  The two namespaces are disjoint, so `%user.kind%` and `%user!kind%` can hold
  different values.
- **Writes are sync** (`this[key] = value`, `Set`, `Add`). **Reads are async**
  (`Value(key)`, `Get<T>(name)`), because a property may ride lazily. A
  `Data` stored as a property is refused: `Data` belongs in the value.
- **Values are wire primitives.** `string`, `bool`, the numeric CLR types,
  `DateTime`, `byte[]`, `Guid`, any `item`, or an `IEnumerable`/`IDictionary` of
  the same.
- **`Clone()`** copies the bag while the values inside stay shared. `Copy()`
  uses it, so `set %y!NewProp% = 1` lands on `%y%` only.

### View tags

A member says which view it travels in with a tag. The views are the `View`
enum in `PLang/app/View.cs`: `Default`, `Store`, `LlmBuilder`, `Debug`, `Out`,
`In`. The attributes are `[Out]`, `[Store]`, `[Debug]`, `[LlmBuilder]`.

| Tag | View | Meaning |
|---|---|---|
| `[Out]` | `Out` | travels on the wire when the Data leaves the system |
| `[Store]` | `Store` | persisted to `.pr` and the store |
| `[Debug]` | `Debug` | written in a debug dump |

On `Data` itself, `Error`, `Success`, `Type`, and `Properties` carry
`[Out, Store]` — they travel and persist. `Properties` *also* carries
`[JsonIgnore]` and `[LlmIgnore]`, and `Signature` and `Parent` carry
`[LlmIgnore]`: a member can stay off the default JSON and the LLM's view while
still riding the plang wire, because the tags only advertise the intended shape
— the actual wire emission is written by the Data's own `Output`
(`this.Output.cs`) and read by `Wire` (`Wire.cs`).

---

## 6. The courier rule: forward `Data`, open only at a leaf

A value's door is opened in one place: the typed leaf that needs the value.
Every other layer passes the `Data` along whole.

- **Couriers** forward `Data`: variable memory, the callstack, a channel, a
  handler that only relays a result. They do not read `data.Value` or
  `data.Item`.
- **Leaves** open the value: a handler that declares a typed `Data<T>` slot and
  calls `Value()` on it. The door is the leaf's to open.

```csharp
// courier: forward the Data whole
return await next.Start(context);

// leaf: a declared Data<T> slot opens its own value
var value = await slot.Value();          // slot is a Data<T> parameter
```

Opening a value in the middle of a courier is the **broken seal** smell in
`Documentation/v0.2/obp-smells.md`. The tell is `.Value is`, `.Value as`, or
`.Value switch` outside a file that declares a `Data<T>` parameter.

---

## 7. Wire and signing: pointers

`Data` writes itself. Its `Output` writes the `@schema` marker first
(`"@schema":"data"`), then its type, its value (delegated to the item), and
its properties. The reader is `Wire` (`Wire.cs`), which reads `Data` back by
that marker. A signed Data is signed when it is written. Owners do not sign at
egress boundaries.

The full model is in `../../../Documentation/Runtime2/data-spec.md`. The wire
boundary is in `../start.md` §7. This page does not repeat them.

---

## 8. Cheat-sheet: plang or C# → source

| You have | You want | Where |
|---|---|---|
| a `Data` parameter | what it holds | `Value()` (the door), `Peek()` (in memory now, no read) |
| a `Data<T>` slot | the typed value | `Value()` on the slot, or `Use(then)` |
| a failed result | why | `Error`; `Success` marks it observed |
| a result to return | a success or failure | `Ok(value)`, `FromError(error)` in `this.Result.cs` |
| `%x!key%` | the metadata | `Properties` in `Properties.cs` |
| `%x.field%` | the child | `Get(path)` in `this.Navigation.cs` |
| `%x%` (a reference) | the Data it names | `Follow(ctx)`, `IsVariable` |
| the wire shape | `@schema`, the fields | `this.Output.cs` (writes), `Wire.cs` (reads) |
| a `[Out]` member | where the tag is read | `View` in `PLang/app/View.cs` |

---

## The tree from here

| Page | Covers |
|---|---|
| `../start.md` | the app root and the collection-node graph; `Data` in §7 |
| `../type/item/start.md` | the value lattice: what `item` is, and each value type |
| `../module/start.md` | the module system: action handlers and `Data<T>` slots |
| `../../../Documentation/Runtime2/data-spec.md` | the wire model, signing, and the serializer |
