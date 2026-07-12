# Format is a birth fact (architect → coder)

Answers `coder/rawformat-is-the-format-a-caller-fact.md`. Settled with Ingi in session 2026-07-12. This subsumes the "minimal unblock" — don't land the stopgap, land this; the 5 byte-backed reds green under it (byte raw → `application/octet-stream` → registered value dispatch → the type reader pulls `Bytes()`).

> **You own this.** Every code block below is a suggestion — the design decisions (what owns what, what dies) are settled; the exact bodies, private plumbing names, and mechanics are yours. Flag back anything that doesn't survive contact with the code.

## Why

A source's format has one job: select the serializer that decodes its raw. Today that fact is guessed in three places — `type.RawFormat` guesses by Name (a type-switch on the generic entity), and the file/channel capture sites coerce "I don't know this mime" into "it's text/plain" via `?? Serializers.Text`, stamping a text encoding onto gif bytes. The unregistered-mime guess (`Mime("." + Kind)`) then produces formats like `image/gif` that no serializer is registered for → `UnregisteredMimeType` at materialization. The fix is structural: capture sites pass only real knowledge (or null), and silence falls to exactly one derivation at the one birth site, owned by the family, not switched on a name.

## The ruling (three questions, three answers)

1. **Can `RawFormat` die?** The method dies; the fact doesn't become fully caller-supplied. Capture sites that know the encoding (file, channel, wire reader) pass it — that stays the rule. The bare-literal path (`data/this.cs:250`) has no earlier step holding a mime: a construction site's string is by definition the type's canonical literal form, so the residual default is a definition, not a guess. What dies is the guess-by-Name switch.
2. **One shared derivation for read and write?** Dissolved. Read asks *"what format is this literal captured in"* — the new `ICreate.Format` fact. The write fallback (`ResolveForWrite`, `channel/serializer/list/this.cs:166-167`) asks a different question — *"what wire shape does an untyped write get"* — and deliberately answers plang for a number so the type survives the wire (its doc at `:152-160` says so). Two facts that partially coincide, not one fact stored twice. **`ResolveForWrite` stays untouched.** (Residual note, no action: its `data.Peek() is string` shape-sniff could someday become an instance-level fact on the value; nothing depends on it now.)
3. **Byte content = `application/octet-stream`?** Yes. A per-kind mime in the format slot is *stored twice* (the kind already carries `gif`) and bytes have no encoding to select — the type reader decodes them (`image/serializer/Reader.cs` calls `reader.Bytes()`; `value/reader.cs:48` hands the blob through). One constant, one registered serializer, no per-mime lookup.

**Naming ruling (Ingi):** the fact is called **`Format`** everywhere — `Encoding` was a synonym for the same concept and synonyms never. `source._format`, `ICreate.Format`, `type.Format` are one fact.

## The flow

```
CAPTURE SITES — pass only what they actually know (null = "I have no encoding fact")
  file.ReadText        format = Serializers.GetByType(extensionMime)?.Type       // null for .gif, .pr
  channel.StampValue   format = Serializers.GetByType(contentType)?.Type         // null for image/png blob
  wire reader (.pr)    format = token is string ? value.Text : "application/plang"   (unchanged)
          │
          ▼
type.Create(raw, ctx, format?) ──(string | byte[])──► new item.source(raw, type, ctx, format)
          │
          │  BIRTH DERIVATION — one site, ctor-fixed, immutable, only when format == null:
          │    raw is byte[]  →  value.Binary (application/octet-stream)
          │    else           →  type.Format        // the family's literal fact
          ▼
source.Read()  →  Serializers[_format].Read(source, ctx)        (source.cs:179-188, unchanged)
      text/plain ──┐
      octet-stream ┴─► value.@this.Read → value.Reader(raw) → type reader pulls itself
      application/plang ─► plang parser (container literals — value.Reader is scalar-only, structural pulls throw)
      application/json  ─► json parser
```

Why `GetByType` returning null is correct: the registry holds *parsers* (json, plang, text/plain). `image/gif` is a content family, not an encoding — no parser exists or should; the image type reader decodes the bytes. Null is the true answer, and the birth derivation turns it into `octet-stream`.

Why dict/list say `application/plang`: a container's literal form is JSON text, and `value.Reader` is deliberately not a parser (`value/reader.cs:14-17` — structural pulls throw). Which parser reads a family's literal is the family's choice — serializer capability can't pick it (plang can also parse `"42"`; value can hold `"[1,2,3]"` as one dumb token), and a name→serializer catalog above the types would be the deleted switch relocated.

Why the wire reader keeps its fork (`data/reader/this.cs:90-91`): it just looked at the token, so it knows what it captured — a string token's raw is bare content (value format); an object/array/number token's raw is JSON text (plang). Capture-site knowledge, not a guess.

## Code changes, file by file

**1. `PLang/app/type/item/ICreate.cs`** — the family fact (NEW):

```csharp
/// <summary>
/// The format a bare literal of this type is captured in — which serializer parses a
/// raw string declared as this type when no capture site supplied one. A scalar's
/// literal is its own text token (the value serializer); a container overrides — its
/// literal is plang wire (the value reader is scalar-only and cannot parse structure).
/// </summary>
static virtual string Format => global::app.channel.serializer.value.@this.Text;
```

**2. `PLang/app/type/item/dict/this.cs` + `PLang/app/type/item/list/this.cs`** — both are `ICreate<@this>` (`dict/this.cs:25`, `list/this.cs:21`); each gets (NEW):

```csharp
/// <summary>A container literal is plang wire — parsed by the plang serializer, never the scalar value reader.</summary>
public static string Format => global::app.channel.serializer.plang.@this.Mime;
```

**2b. `PLang/app/channel/serializer/plang/this.cs`** — the serializer owns its mime name, same shape as `value.@this.Text`/`Binary`. Current `public string Type => "application/plang";` (`:46`) →

```csharp
public const string Mime = "application/plang";
public string Type => Mime;
```

No naked `"application/plang"` strings in type files — the type names *the serializer* (compiler-checked, navigable), never a floating string that happens to match one. Sweep the other literals to the const where they mean the plang serializer: `data/reader/this.cs:91`, the registry ctor's `Register` rows. (`"application/plang+json"` in the alias row is a different mime — stays literal or gets its own const, your call.)

**3. `PLang/app/type/this.cs`** — `RawFormat` (`:185-197`) deleted. In its place, the entity door closing the family's static virtual, mirroring the `_openByContext` bind pattern already in this file (`:384-391`). Runs once per entity, cached — the alternative to this one reflection close is a name-switch, which is the obpv being deleted. Suggestion (private plumbing names + where the close lives are yours; note C# forbids a property and method both named `Format` in one class, hence the shape below):

```csharp
/// <summary>The format a bare literal of this type is captured in — the FAMILY's own
/// answer (dict/list: plang wire; every scalar: its own text token). Read once at
/// source birth when no capture site supplied a format. A primitive/host entity
/// (no ICreate family) answers the scalar default.</summary>
internal string Format()
    => _format ??= Creatable is { } clr
        ? (string)_openFormat.MakeGenericMethod(clr).Invoke(null, null)!
        : global::app.channel.serializer.value.@this.Text;
private string? _format;

private static string Format<T>() where T : item.@this, global::app.type.item.ICreate<T>
    => T.Format;

private static readonly System.Reflection.MethodInfo _openFormat
    = typeof(@this).GetMethod("Format",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
```

**4. `PLang/app/type/item/source.cs:51`** — the one derivation site, fixed at construction. Current `_format = format ?? type.RawFormat(value, context);` →

```csharp
_format = format ?? (value is byte[]
    ? global::app.channel.serializer.value.@this.Binary   // bytes carry no encoding — the type reader decodes them
    : type.Format());
```

The byte/string fork lives here because source already owns that dichotomy (`Peek` `:87`, `Write` `:225`, `RawText` `:71`). Also `:227`: `global::app.channel.serializer.Text.Mime` → `value.@this.Text`.

**5. `PLang/app/channel/serializer/Text.cs` → `PLang/app/channel/serializer/value/this.cs`** — class `app.channel.serializer.value.@this`, beside its own `reader.cs`, matching the `plang/this.cs` shape. The codebase already calls this the value reader (`path/file/this.Operations.cs:71`, `channel/this.cs:299-300`); the class name `Text` was the lie. Its identity: the no-envelope serializer — json/plang encode a structure around the value, this one emits/reads the value's own face, which is why it honestly owns two mimes:

```csharp
public const string Text   = "text/plain";                 // the value's own text face
public const string Binary = "application/octet-stream";   // the value's own byte face
public string Type => Text;
```

`Read` body unchanged (`Text.cs:84-90` already is the raw-value dispatch). Dies with the move: `_jsonFallback` field + `jsonFallback` ctor param (assigned `:31`, never read) and the stale "Falls back to JSON for complex types" class doc.

**6. `PLang/app/channel/serializer/list/this.cs`** (the serializer *registry* — the `X.list` collection, not a serializer for lists) — ctor `:24` constructs the renamed class; register the second mime as an alias row like `text/json → json`:

```csharp
var value = new global::app.channel.serializer.value.@this(context);
...
Register(value);
_byType[global::app.channel.serializer.value.@this.Binary] = value;
```

The `public ISerializer Text => _byType["text/plain"];` property (`:130`) loses its last production callers after 7-8 — delete it, or keep a `Value` door if tests want one; your call.

**7. `PLang/app/type/item/path/file/this.Operations.cs:73-75`** — pass only real knowledge. Current three lines (`var serializers = ...; var serializer = serializers?.GetByType(mime) ?? serializers?.Text; var format = serializer?.Type ?? "application/plang";`) →

```csharp
// A registered mime is a real capture fact — pass it. An unregistered one (.pr, .gif)
// is no knowledge: pass null and let the source's birth derivation answer.
var format = Context.Actor?.Channel.Serializers?.GetByType(mime)?.Type;
```

Behavior check: `.json` → `application/json` as before; `.pr` bytes → null → octet-stream → value dispatch → goal reader (today text/plain → the same dispatch — same reader path); the build-snapshot arm (`:86`, string raw) → null → goal's default text/plain — same as today; `.gif` → null → octet-stream → image reader (today: red).

**8. `PLang/app/channel/this.cs:294-303` `StampValue`** — same pattern. Current `var serializer = Channels?.Serializers.GetByType(Mime ?? "") ?? Channels?.Serializers.Text;` + `serializer?.Type ?? "application/plang"` →

```csharp
var format = Channels?.Serializers.GetByType(Mime ?? "")?.Type;
return new global::app.data.@this(Name, StampType(context).Create(raw, context, format), context: context);
```

After this the body is "stamp `{type,kind}` from the mime, Create with real-or-null format" — whether `StampValue` survives as a named seam is your shape call. (`StampReadAsync`'s plang-container branch `:278-285` untouched.)

**9. `PLang/app/data/reader/this.cs:91`** — const ref only: `global::app.channel.serializer.Text.Mime` → `value.@this.Text`. The logic stays — this capture site genuinely knows its slot's encoding.

## Demolition list

| What | Where | Dies when |
|---|---|---|
| `type.@this.RawFormat` (the Name-switch) | `type/this.cs:185-197` | change 3 |
| class `Text` + file `Text.cs` | `channel/serializer/Text.cs` | change 5 (move+rename) |
| `_jsonFallback` field + `jsonFallback` ctor param + stale class doc | `Text.cs:21,27-31,5-9` | change 5 |
| registry `Text` property (or → `Value`) | `serializer/list/this.cs:130` | change 6, after 7-8 remove its callers |
| `?? serializers?.Text` coercion | `path/file/this.Operations.cs:74` | change 7 |
| `?? "application/plang"` literal | `path/file/this.Operations.cs:75` | change 7 |
| `?? Channels?.Serializers.Text` coercion + `?? "application/plang"` | `channel/this.cs:301-303` | change 8 |
| `global::app.channel.serializer.Text.Mime` refs | `source.cs:227`, `data/reader/this.cs:91` | changes 4, 9 |

**Stays:** `source.Read` dispatch (`source.cs:179-188`); `source.Write`'s format fork (`:223-231` — reads the honest `_format`); the wire reader's deferredFormat fork (`data/reader/this.cs:90-91`); `ResolveForWrite` whole (`serializer/list/this.cs:162-168` — different fact, see ruling 2); plang/json serializers; per-type wire renderers (`image/serializer/*`).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `ICreate.Format`, `type.Format()`, `source._format` | one concept, one name; no synonym | ✓ (Encoding rejected as synonym) |
| `value.@this` | single word, names the actual distinction (no-envelope, the value's own face) | ✓ |
| consts `Text`, `Binary`, `plang.@this.Mime` | nouns; every mime string owned by its serializer, referenced never repeated | ✓ |
| birth derivation in source ctor | determined on creation, immutable; fork is source's own raw dichotomy (`Peek`/`Write`/`RawText` already fork on it) | ✓ |
| family fact via `static virtual` | no name-switch, no catalog above the types; one reflection close cached per entity, same price as the existing `Create` binds | ✓ |
| deleted: `RawFormat` Name-switch, `?? Text` coercions | fork + guess removed at the root | ✓ |
