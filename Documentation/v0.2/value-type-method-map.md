# Value / Type / Data — method map

A reference for the three layers a value lives in. The confusion comes from names
repeating across layers (`Value`, `Peek`, `Convert`, `Create` each exist twice with
different jobs), so this is organised by **who owns the method**.

```
Data            the named slot a variable holds (carries a value, isn't the value)
  └─ item       the VALUE itself (text, number, dict, source, …) — the type instance IS the value
        ↕
     type       a LABEL ("I am number/int") — a descriptor that builds/converts/compares values
```

## Layer 1 — `Data` (`app.data.@this`) — the named slot / courier

The thing variables hold. Carries a value but isn't the value. The runtime is a courier:
it moves `Data`, reads `Type`/`Success`/`Error` to route, but doesn't open `.Value` (only leaves do).

| Member | What it does |
|---|---|
| `Value()` | **async** — "give it to me ready." Materialises: parses a source, loads a file. The main value door. |
| `Value<T>()` | same, typed as `T` |
| `Peek()` | **sync** — the value as-it-is-now, no work (a source stays a source) |
| `Type` | the value's type entity (asks the value via `Mint`) |
| `Kind` | the value's kind (`_item.Mint().Kind`) |
| `Declare(type)` | stamp a declared type after construction (build pipeline) |
| `Navigate` / `GetChild` | walk into the value (`%x.y%`) |
| `Fail` / `Ok` / `IsEmpty` | error / success / emptiness |

## Layer 2 — the VALUE (`app.type.item.@this`)

text, number, dict, image, `source`, … The type instance **IS** the value. Three doors:
**Peek** (sync, as-is), **Value** (async, ready), **Write** (async, serialise).

| Member | What it does |
|---|---|
| `Peek()` | sync — itself; for a `source`, its raw form |
| `Value(data)` | async — materialise itself (a `source` parses; most values return self) |
| **`Mint()`** | **"what type am I?"** → mints the type entity. The type-name door. Load-bearing: serializers + `Data.Kind` use it. Each type answers its own way (number → its tower kind; source → its declared `{type,kind}`). |
| **`Facet(typeName)` / `Facet<T>()`** | **"am I (or a prior) this type?"** → the matching value, or null. The typed-identity door — wraps `Mint`. |
| `Prior` | the previous value in the chain (a parsed value remembers its `source`) |
| `Narrow()` | collapse to the most specific form |
| `Write(IWriter)` | serialise itself to a channel / wire |
| `Write(key, value)` | set a child (containers) |
| `Navigate(parent, key)` | walk into itself by key |
| `Clr<T>()` | **internal** — lower to a CLR value (the .NET exit door, boundary only) |
| `IsLeaf` / `IsNull` / `IsTruthy` / `Contains` / `IsEmpty` | predicates |
| `Clone()` | protected copy |

## Layer 3 — the TYPE entity (`app.type.@this`)

Not a value — a *label* ("I am number/int") that can build, convert and compare values.

| Member | What it does |
|---|---|
| `Create(name, kind, …)` *static* | make a **type entity** from a name (`"number"`, `"int"`) |
| `Create(raw, ctx)` *static* | **lift** a raw CLR value → its plang value (general CLR→plang door). Returns a *value*, not a type. |
| `FromName` / `FromMime` *static* | type entity from a name / mime |
| **`Build(value)`** | make a **value of this type** from an input (raw → defer via `source`; built → hold or re-type). The construction door. |
| **`Convert(value, ctx)`** | re-type a value to this type via the family hook. The dispatcher (routes to the per-type hook). |
| `Convert(string raw)` | 1-arg helper — coerce a raw string to this type's CLR |
| `Judge(value)` | no-context twin of `Build` — **GOING AWAY** (born-with-context removes the no-context path) |
| `Deserialize(raw)` | dead, zero callers — **GOING AWAY** |
| `Is(other)` / `Is(typeName)` | type compatibility |
| `Rank(other)` / `Compare(a,b)` | which type drives a comparison / order-equate two values |
| `Name` / `Kind` / `Strict` / `Polymorphic` / `ClrType` | properties |

## The two traps that cause the "lost" feeling

**1. Two `Convert`s, different layers:**
- `type.Convert(value, ctx)` (Layer 3) — the **dispatcher**: "make me of this type," routes to the hook.
- per-type **static** `number.Convert(raw, kind, ctx)`, `date.Convert(…)` — the **leaves**: the ~12 hooks that actually build each family. Found at `app/type/<name>/this.Convert.cs`.

**2. Two `Create`s on the type:**
- `Create(name)` makes a **type label**.
- `Create(raw)` lifts a **value**.
Same word, opposite outputs (type vs value).

## The per-type Convert hooks (the 12 leaves)

`app/type/<name>/this.Convert.cs` for: number, text, bool, date, datetime, time, duration,
guid, path, image, list, dict. Current signature:

```csharp
public static data.@this Convert(object? value, string? kind, context)   // returns Data (Ok/Error)
```

(Reached via the dispatcher `type.Convert` and via each type's `serializer/Default.cs` whole-payload `Of` reader.)
