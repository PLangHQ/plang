# Defining a PLang type in C# — and its read/write (I/O) layer

This is the guide for adding a new value type to PLang (a `number`, `path`, `choice`, `image`, …) and wiring how it crosses the wire. Three ideas run through everything:

1. **A type creates itself.** All construction lives *on the type* — the `ICreate<@this>` doors, never a static helper class beside it and never a named factory next to the doors. A `FooMeta`/`FooUtil` the type leans on, or a one-caller `FromX(...)` private static, is the same smell; dissolve it into the type's `Create`.
2. **CLR types are known to a value type in exactly two places: the `Create` doors (birth) and the `Clr` door (exit).** The backing field is private; content leaves the type only via `Write(IWriter)`, the typed ops, or the value door — a .NET edge lowers through `Clr`. No public CLR-typed property, no CLR parameter on any other member.
3. **Format never leaks into a type.** A value reads and writes through the abstract `IReader`/`IWriter` — it never names STJ, JSON, CSV, or any concrete serializer. A `[JsonConverter]`, a `Utf8JsonReader`, or `System.Text.Json` inside a value type is the alarm. The one home for format is the `channel/serializer/*` layer, which hands your type an `IReader`/`IWriter`.

---

## 0. What is `item` (`: item.@this`)?

`app.type.item.@this` (`PLang/app/type/item/this.cs`) is the abstract base of **every** PLang value — `text`, `number`, `path`, `list`, `dict`, `choice`, `image`, your new one. Writing `public sealed class @this : global::app.type.item.@this` says *"this class IS a PLang value."* By convention the class is always named `@this` and lives in `this.cs`, so consumers alias it (`global::app.type.item.number.@this` = the `number` value).

`item.@this` is the polymorphic root the whole runtime moves values around as — variable memory holds `item`s, `Data` wraps an `item`, couriers pass `item`s without looking inside. It is **storage-free**: the base carries only behavior, never a value slot; the backing (`string`/`long`/`byte[]`/…) lives on each subtype as a private field.

`Data` (`app.data.@this`) is **not** an `item` — it is the binding that *holds* one, plus name/type/properties/signature. The value lives on the `item`; `Data` is the binding.

A type is a plang value iff it implements `ICreate<@this>` — that marker is what lets the type entity's born door close its generic `Create` thunks over the class (`app/type/this.cs`, `Creatable`).

---

## 1. The type class

Every value type is a `sealed class @this : app.type.item.@this, ICreate<@this>` living at `app/type/item/<name>/this.cs` — the folder name is the PLang type name (`app/type/item/number`, `app/type/item/path`, …).

```csharp
namespace app.type.item.myvalue;

public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // THE backing — a private field, never a property at any visibility.
    // Content leaves the type only via Write(IWriter), the typed ops, or the
    // door; a .NET edge lowers through Clr. (See text/this.cs for the model.)
    private readonly <backing> _value;
    public @this(<backing> value) { _value = value; }
```

An **inbound** implicit operator (`public static implicit operator @this(<backing> v) => new(v);`) is fine — it is the entry lift. An **outbound** implicit (`@this` → backing) is banned: every site would be a silent CLR exit; a real .NET edge names `Clr` instead.

### Members you override on `item.@this`

The base (`app/type/item/this.cs`) defines the full virtual surface; a new type overrides the rows that apply:

| Member | What it does | Notes |
|---|---|---|
| `Type` | The value's own type entity — `new("myvalue", typeof(<backing>)) { Kind = … }`. | What `Data.Type` reports and what the wire writes as the declared type. **The name is your own folder name, always** — the declared name selects the reader on the way back in (see §4), so a value reporting another type's name does not round-trip as itself. |
| `Write(IWriter w)` | Renders the bare wire form — format-independent (§3). | A leaf writes one token (`w.String(...)`); the base throws, so a missing override is loud. May branch on `w.Format` when the type renders differently per format (image: base64 vs text label vs raw bytes). |
| `Output(writer, mode, ctx)` | The full async self-write. | Default routes to `Write` — only containers/references/templates override. |
| `IsLeaf` | `true` for a single-token scalar; default `false`. | Drives the serializer's leaf-vs-structure branch. |
| `Clr(Type target)` | THE CLR exit door — hand the private backing to `ClrConvert(_value, target)`. | Only a real .NET/3rd-party edge calls it. Lossy conversion throws, never a silent default. |
| `ToString()` | The display/text face. | |
| `Value(Data)` | THE value door — "I am going to use this value, make yourself ready." | A plain scalar answers itself (the default). A lazy value (path-backed image, deferred parse, template) loads/parses/renders **here** — laziness is construction state resolved at this door, never a specially-named method. Failures go `data.Fail(error)` + return `Absent`; the only blessed surface of the `Data` here is `Fail`. |
| `Cacheable` | May the holding `Data` keep the door's answer? | `false` when the answer depends on outside state (template render, computed). |
| `IsTruthy()` / `AsBooleanAsync()` | The value's boolean meaning. | Override `AsBooleanAsync` only when truthiness needs I/O (path existence). |
| `IsEmpty()` / `Contains(needle)` | Emptiness / membership, each type's own answer. | No ToString fallback — a needle never matches a serialization. |
| `Rank` / `Order(other)` | Comparison: higher rank drives; the driver coerces the other side through its own pure `Create` core. | A non-coercible other is `Incomparable`, not an error. |
| `Kinded(kind)` | A re-kinded **copy** (values are immutable, never restamped in place). | Only for types with a kind axis. |
| `Get(parent, key)` / `Set(key, isIndex, value)` | Navigation / child write. | Leaves usually keep the defaults (or fail with their own story — see text's `CantNavigateText`). |
| `Peek()` | What is in memory NOW — sync, no I/O, no parse. | |
| `IsVariable` / `Get(ctx)` | Only if the value can be a reference to a named binding. | Content answers `false`/`null`. |

### Catalog statics (LLM-facing teaching)

The type catalog folds these static properties into the type entity (`BuildTypeEntries` → `type.@this.Promote`), so the builder LLM can teach the type:

```csharp
public static string Example => "readme.md";          // canonical literal
public static string Shape => "string";               // the wire-scalar shape
public static string Description => "…";              // semantic teaching
public static IReadOnlyList<string> Kinds => […];     // advertised kind vocabulary (only if closed)
```

---

## 2. Construction — the `ICreate<@this>` doors

Construction is `app.type.item.ICreate<TSelf>` (`app/type/item/ICreate.cs`) — three static-virtual faces, all **on the type**. The target owns the conversion ("we want number, the number knows how to create it"), never the source, never a catalog above the types.

### `Create(object? raw)` — the pure core

*The ONE runtime boundary.* Per its own contract: "`object` because this method IS the crossing — a raw CLR value and an item of another type flow through the SAME switch (`int i => …` beside `text t => …`)". Context-free, no `Fail` — a decline is `null`, not a failure. This is where rule 2 of the header lives: the CLR arms exist here and nowhere else on the class.

The shape every type follows (this is `binary`'s real core):

```csharp
public static @this? Create(object? raw)
{
    if (raw is @this self) return self;                                  // pass-through
    object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;   // another item exits via ITS Clr door
    switch (value)
    {
        case byte[] b: return (@this)b;                                  // the raw backing
        case string s:                                                   // a literal — parse it
            try { return (@this)System.Convert.FromBase64String(s); }
            catch (System.FormatException) { return null; }
        default: return null;                                            // decline — not a failure
    }
}
```

Never a blind `value.ToString()` — it loses the source type and mis-handles non-strings. Switch on the real value; parse strings; decline the rest.

### `Create(object? raw, actor.context.@this? ctx)` — the context face

The default delegates to the pure core. Override **only** when the type resolves against an actor (a reference fundamental — `path`/`file`/`image`/`url` need the scheme registry). Context lives on the minority that needs it, not the scalar majority.

### `Create(object? raw, data.@this data)` — the courier

The typed ask (`Data.Value<T>()`, a `Data<T>` slot resolving). The default runs pass-through → the context face → the container self-deserialize → fails typed. Override it to land *your* decline reasons (`data.Fail(new Error(...))` — the error belongs to the binding the caller already holds) or to honor a kind override (number's `as decimal`). Implementations touch **only** `data.Fail`; everything else on the `Data` is courier state and off-limits.

**The courier has two entrances, and they hand different shapes.** `As<T>`/`Data.Value<T>()` hands the source **item** (a `text` arrives as a `text`); the entity door's leaf-retype path (`type/this.cs`, the built-leaf branch) lowers the leaf through its `Clr` **first** and hands the raw CLR form (that same `text` arrives as a `string`). Courier arms must handle both — "receives only plang types" is never true here.

### Declines vs errors

A `Create` door answers a value it cannot build in one of two ways, and the split is the *reason*, not the caller:

- **Wrong type = decline.** An `int` offered to `path`, a `dict` offered to `guid` — not this type's to build. The pure core answers `null`, silently; the default courier turns an unhandled decline into the typed `CreateItemDeclined` fail.
- **Malformed value = error, never a silent null.** A string that *claims* the type's shape but is invalid (a non-base64 payload, an unparseable number literal, a malformed data-url) is a broken value, and swallowing it as a decline loses the reason. Report through whichever channel the door has:
  - **No `data` in scope** (the pure core's parse, a shared parse helper, a wire reader) → **throw** (`FormatException` with the specific reason). On the born path the throw rides `source.Value`'s catch (`FormatException`/`JsonException`/…) into `MaterializeFailed`, named to the binding — validation-at-the-door for free.
  - **The courier** catches that throw and converts it to `data.Fail` (a keyed error on the binding).
  - **Comparison** (`Order` coercing via the pure core) catches locally and answers `Incomparable` — per the base contract, a non-coercible operand is not an error.

### Rules that hold regardless

- **No static helper class, no named factory beside the doors.** A `FromString`/`FromBytes`-style private static with one caller is `Create`'s own switch arm wearing a name — inline it. (Existing `image.FromBytes` survives only because catalog reflection can't disambiguate same-name static overloads; that is a documented exception, not a pattern.)
- **Laziness is state, not a method.** A value that defers work (encode, load, parse) holds its source in a field at construction and resolves at the `Value(data)` door — see `image` (path-backed, materializes at `Value`, caches). Never a `DoXLazily(...)` helper: a method call executes now; only a constructed value defers.
- **Output is always the born-native wrapper.** A value built by its type IS a plang value; a .NET edge unwraps with `Clr<T>()`.

---

## 3. The I/O layer — read/write, format-free

A value crosses the wire through type-owned, format-agnostic members. None of them knows what serializer is driving.

### Writing — `Write(IWriter w)`

`IWriter` (`app/channel/serializer/IWriter.cs`) is the abstract sink: `Null/Bool/Int/Long/Float/Double/Decimal/String/Bytes/DateTime/DateTimeOffset/TimeSpan/Guid/Enum`, `Raw` (verbatim passthrough), `BeginArray(count)/EndArray`, `BeginObject/Name/EndObject`, `BeginRecord/EndRecord` (the Data envelope), plus `Format` (the short token — `"json"`, `"plang"`, `"text"`, …) and `EmitsSchema`. A leaf writes one token:

```csharp
public override void Write(global::app.channel.serializer.IWriter w) => w.String(ToString());
```

The concrete writer is chosen by the channel/format layer — your type only ever sees `IWriter`. Branching on `w.Format` is legitimate format-*asymmetric* rendering (image); naming a concrete serializer is not.

### Reading — the `(type, kind)` reader registry

The registry (`app/type/reader/this.cs`) holds **two read modes side by side, both load-bearing**:

- **`Typed` — the token-stream pull** (`ITypeReader`): the type pulls its value token-by-token off a tokenized source (the `.pr` wire read, `json.Reader`), no DOM.
- **`Of` — the whole-payload content decode** (a static `Read`): the raw string/bytes is already in hand and the type decodes it whole (CSV parse, base64, image bytes).

A type may ship both — its wire value streams via `Typed`, its content form (a file's bytes materializing through `source.Value`) decodes via `Of`.

**The typed pull reader** lives at `app/type/item/<name>/serializer/Reader.cs`. It auto-registers by namespace convention: a non-abstract class in a namespace ending `.serializer`, implementing `ITypeReader`, with a parameterless ctor — the segment before `.serializer` (leading `@` stripped) is the type name. This is `bool`'s real reader:

```csharp
namespace app.type.item.@bool.serializer;

public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;   // or a concrete kind token per variant

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("bool", kind);
        return reader.Peek() switch
        {
            TokenKind.Bool => new global::app.type.item.@bool.@this(reader.Bool()),
            TokenKind.String when bool.TryParse(reader.String(), out var parsed)
                => new global::app.type.item.@bool.@this(parsed),
            _ => new global::app.type.item.@null.@this("bool", kind),
        };
    }
}
```

`IReader` (`app/channel/serializer/IReader.cs`) mirrors `IWriter`: `Peek()` (the current `TokenKind`), the leaf pulls (`Null/Bool/Int/Long/Number/Float/Double/Decimal/String/Bytes/DateTime/DateTimeOffset/TimeSpan/Guid`), `BeginArray/NextElement/EndArray`, `BeginObject/NextName/EndObject`, `RawValue()` (capture encoded bytes verbatim — the lazy passthrough), `Skip()`. The `ref TReader … allows ref struct` signature lets a stack-only reader cross with no boxing, monomorphized per format. Cursor contract: read from the first token; leave the cursor on the value's **last** token.

**The whole-payload decode** lives at `app/type/item/<name>/serializer/<Kind>.cs` — a `public static class` exposing `static object? Read(object raw, string? kind, ReadContext ctx)`. The class name maps to the kind token (`Default` → the `"*"` wildcard).

`ReadContext` carries the actor `Context`, the authored-`Template` mode, the `View`, and signature-verification flags — the decode context, threaded so signatures without re-threading every `Read`.

**The same reader serves every format** — a `.pr` JSON token, a CSV payload, a byte blob. That is the whole point: `bool`'s reader reads a bool the same way whatever the bytes were.

### Kinds

A kind is a **short tail token** (`md`, `gif`, `int`, `json`), never a slash form — `"text/markdown"` is the *authoring* form, which `type.Create` splits into name + kind on the first slash, and the LLM teaching explicitly warns the slash string off the wire. A kind token carries its content family on its own (`type.Compressible` resolves jpg→image, mp3→audio through `format.TypeOf(kind)`), so a mime never needs to ride whole.

A type that reads differently per kind (`table`: csv vs xlsx) registers one reader per kind token; a type that reads uniformly registers `AnyKind` (`"*"`) and may switch on the passed `kind` inside `Read`. Lookup precedence: runtime-exact → generated-exact → runtime-`*` → generated-`*`. The selection door is `Context.App.Type.Reader.Reader(typeName, kind, context)` — it throws loudly when the resolved type ships no reader, because **every value type owns a `serializer/Reader.cs`**.

---

## 4. The value lifecycle — how data flows through the type system

### Unprocessed data has two pure forms

Nothing is processed until it is actually used. Data enters the system as **pure text or pure bytes, undecoded** — that pair is the whole vocabulary of "raw". Two carriers hold it:

- **`item.source`** (`app/type/item/source.cs`) — the undecoded form (`string` or `byte[]`) plus the declared `{type, kind}` judgment, held whole. It IS the declared type, unparsed.
- **`item.wire`** — a still-encoded slice captured together with the serializer that sliced it (born via the entity's capture door, `Create(slice, context, ITransport)`).

While unprocessed, purity is guarded: `Peek()` answers the raw form and **never sniffs** ("is this valid UTF-8?" is not asked — a byte raw stays bytes unless the *declaration* says text); serializing an untouched source writes its raw verbatim (`source.Write` — bytes as bytes, string quoted; a wire writes its slice through its captured format). A full-match `%ref%` marked by the builder is a reference, not content — decided once at birth, resolved by name, never parsed.

### Birth — the entity door

The born-native door is the type entity's `Create` (`app/type/this.cs`): `context.App.Type[name].Create(raw, context)`. **Context-never-null** — a value is born WITH context; passing null throws with a pointer at the construction site.

```
.pr slot { name, type:{name, kind}, value }
  → the wire read hands the raw + declared type to the entity door
  → entity.Create(raw, context):
      null                → the typed null citizen
      string / byte[]     → item.source(raw, type, context)      — LAZY, unparsed
      native container    → held as-is
      a source, re-declared → source.Declared(type)              — re-birth, still unread
      a built leaf        → same name: Kinded refine · type-history Is: held ·
                            else lower via ITS Clr, re-enter through the family courier
      a raw CLR scalar    → the family lift (TSelf.Create via the entity's bound thunk)
  → …first touch (.Value())…
  → source.Value: kind-first (App.Type.Kind[kind].Load — a kind that owns its decode wins),
    else the (type, kind) reader via App.Type.Reader.Reader(name, kind, ctx)
  → your reader builds the born-native instance; parse failure → MaterializeFailed on the binding
```

So a scalar is born **lazy** (a `source` carrying the declaration whole) and materializes through *your reader* on first use. The declared `{type, kind}` picks the reader — which is why the wire type must name a registered type, and why your `Type` property must report your own name (§1).

### Graduation — and its asymmetry

First touch (`.Value()`) turns the carrier into the typed value; the holding `Data` rebinds to the answer, and the source rides the materialized value's **prior chain** (`item.list.Add(source)`) so provenance survives — a dict parsed from a file still answers `Is(file)`. But graduation means different things per family:

- **Content leaves (text, binary, image, base64): graduation is a judgment, not a transformation.** The raw string/bytes that rode the wire *become the backing* — a text IS its string, a binary IS its bytes, forever. The raw form never dies; it graduates with the value.
- **Structured values (dict, list, domain): graduation is a transformation.** The parse turns raw into structure, and the raw's life ends there (the prior chain keeps a provenance copy, frozen at parse time — never read it back as "the raw"; after a mutation it is stale).
- **Lazy references (path-backed image, file, url): the stage order inverts.** The value is *typed first, raw arrives later* — an image exists with no bytes until its `Value` door loads them through the path's auth gate. Raw is acquired post-graduation, at the door.

### The raw faces — raw is a property, not a stage

Because of that asymmetry, "unprocessed form" is not something only carriers have — it is a question any value may answer *right now*: `RawText` (`item/this.cs`, default null) is the raw string face; its byte mirror `RawBytes` lands with the base64 type. The carrier answers before the parse (`source.RawText => _value as string`); a content leaf answers always (`text.RawText => _value`); a structured value answers null — a *semantic* answer ("my text/bytes are format-relative — go through a serializer"), not a missing implementation. The engine consumes the face uniformly: the entity door reads `leaf.RawText` off a built leaf to resolve a variable name — no type-switch on where the value is in its life.

### After graduation — re-typing

A built value re-enters the entity door when a declaration re-types it: same type name → `Kinded` refine (a re-kinded *copy* — values are immutable); the type history already satisfies the ask (`leaf.Is(type)`) → held as-is, never downgraded (an image stays an image in a `path` slot); a genuinely different family → lowered through the value's own `Clr` and rebuilt through the target family's courier — the throw boundary; a family that cannot build the shape errors, never silently passing the old value through.

---

## 5. Registering the type name

The reader auto-registers, but the **type name** must resolve through the catalog (`app.Type[name]`, `app/type/list/this.cs`) so the born path can build the declared type. Built-in `app.type.item.*.@this` classes index by convention (the Registry partial scans assemblies for `[PlangType]` and the `@this` convention); a family type whose closed forms surface under a kind registers explicitly (`Register("choice", typeof(choice.@this<>))`). If `app.Type[name]` throws `No PLang type registered under name 'X'`, the name isn't in the registry — register it.

## 6. Smells — stop if you see these

- **A `[JsonConverter]` / `System.Text.Json` / `Utf8JsonReader` inside a value type.** Format has leaked in. The wire form is `Write(IWriter)` + `serializer/Reader.cs`, nothing else.
- **A CLR type visible outside the Create/Clr doors.** A public backing property, a CLR-typed parameter on an op, a foreign type reading your backing — all break rule 2. The backing is a private field; consumers go through the typed ops or the doors.
- **A static helper class or a named factory beside `Create`** (`FooMeta.Build`, `FromString`, `EncodeLazily`). The type creates itself in its `Create` arms; deferred work is construction state resolved at `Value`. Static helper classes need explicit sign-off — default to *not* having one.
- **A `Type` property reporting a name that isn't the folder name.** The declared name selects the reader on read-back (§4); a borrowed identity does not round-trip as your type.
- **A reader that routes wire-reading through `Create`.** The reader (§3) pulls the value off `IReader` and builds it directly; `Create` (§2) converts an already-in-memory value. Two concerns, two paths.
- **A blind `value.ToString()` inside `Create`.** You lose the source type and mis-handle non-strings.
- **A malformed value swallowed as a decline.** `return null` on an invalid payload of the right shape loses the reason and surfaces three hops later as a mystery. Wrong type declines; a broken value errors — throw where no `data.Fail` is in scope, `data.Fail` in the courier (§2 "Declines vs errors").
- **Content sniffing to pick semantics.** "Is this string already base64/json/a path?" guesses are forks that mis-read accidental matches. Branch only on explicit markers (a `data:` prefix, a declared kind, a scheme) — a bare value takes the one default path.
- **`.Clr` / `Clr<object>()` mid-flight.** Lowering to CLR then re-lifting is the raw-CLR-era smell. Stay in plang types; `Clr` is only for the final .NET/3rd-party edge. (The one sanctioned unwrap is the pure core's first line — that method IS the crossing.)
- **The reader/writer knowing the concrete format.** `Read`/`Write` see only `IReader`/`IWriter`; branching on the abstract `Format` token is the ceiling.

## 7. Worked examples in the tree

- **Simplest scalar:** `app/type/item/bool/serializer/Reader.cs` — one token in, `bool` out.
- **String-backed, private backing, templates:** `app/type/item/text/this.cs` — the model for backing discipline, inbound-only implicit, `Rank`/`Order` coercion via the pure core.
- **Bytes / kinded:** `app/type/item/binary`, `app/type/item/image` — bytes in, kind narrows; image is the model for lazy state resolved at the `Value` door and for format-asymmetric `Write`.
- **Container:** `app/type/item/list/serializer/Reader.cs` — `BeginArray()` / `NextElement()`.
- **Family + kind:** `app/type/item/choice/serializer/Reader.cs` — reads the option name off any reader, resolves the closed `choice<T>` from the kind, and lets the type build itself.
