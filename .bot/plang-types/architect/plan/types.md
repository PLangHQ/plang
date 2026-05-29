# Types — the vocabulary inventory

This file goes deep on what types ship on this branch, what each owns, the registration shape, and the cleanup of the existing flat `Primitives` table. The spine ([../plan.md](../plan.md)) locks the architectural decision; this locks the inventory.

## The pattern, restated

Every PLang type lives at `app/types/<name>/`:

- `this.cs` — the value class, `sealed` unless it has genuine variants (`path` is `abstract` because file vs http have different storage; `number` is `sealed` because the tagged union covers all kinds with one shape; `image` is `sealed` because mime + bytes is uniform).
- `this.cs` implements the cross-cutting interfaces: `app.data.IBooleanResolvable` (truthiness), `app.data.IWireWritable` (serialization), `app.modules.IContext` (so leaf actions can reach back into the runtime), and optionally `[PlangType]` registration attribute (see "Registration" below).
- `this.cs` exposes `public static @this Resolve(string raw, app.actor.context.@this context)` — the single string-coercion factory. `app.types.Conversion.TryConvertTo` dispatches here.
- Sub-files at the type's discretion: variant subfolders (`path/file/`, `path/http/`), surface partials (`this.Operations`, `this.Authorize`, `this.JsonConverter`), per-type modules.

The type does **not** depend on any channel. The type sees `serializer.Type` (the mime string) at `WriteTo` time and dispatches. The channel does not depend on any type — it asks `Data.Normalize`, which finds the marker.

## The three proving instances

### `number` — tagged-union numeric

**Folder:** `app/types/number/`
**Files this branch creates:**
- `this.cs` — `NumberKind` enum + storage slots (`_i`, `_d`, `_f`), `Kind` property, `static From(int|long|decimal|float|double)`, implicit-IN operators.
- `this.Parse.cs` — `static Resolve`, `static Parse`, `static TryParse`.
- `this.Operators.cs` — C# operator overloads (`+`, `-`, `*`, `/`, `%`, `==`, `!=`), policy-free (lenient default).
- `this.Arithmetic.cs` — `static Add(@this, @this, NumberPolicy)` and siblings; policy-aware overloads called by `math.*` handlers.
- `this.Equality.cs` — `Equals`, `GetHashCode` with hash canonicalization across equivalent kinds.

**Full design detail:** [storage.md](storage.md) (storage layout, parse, operators, IBooleanResolvable, equality, ToString) and [policy.md](policy.md) (NumberPolicy struct, two axes × three scopes, resolver, environment.number config).

**Leaf-action consumers:** all of `math.*` (`add`, `subtract`, `multiply`, `divide`, `modulo`, `power`, `abs`, `ceiling`, `floor`, `round`, `max`, `min`, `sqrt`, `random`) plus the numeric reducers `list.sum`, `list.avg`, `list.min`, `list.max`. Each grows optional `Overflow` / `Precision` parameters; each calls `NumberPolicy.Resolve` and dispatches to the policy-aware arithmetic.

**Wire dispatch:** uniform — every format knows numbers (see [dispatch.md](dispatch.md) `number` section).

**Why it earns its slot:** tagged-union storage that needs to pick the right writer primitive per `Kind`. Proves the dispatch-on-internal-state path of `IWireWritable`. Also closes the existing `MathHelper.ToDouble` / `MathHelper.PreserveType` scattering — those get deleted at the end.

### `image` — bytes plus mime, format-asymmetric

**Folder:** `app/types/image/`
**Files this branch creates:**
- `this.cs` — `Bytes`, `Mime`, optional `_sourcePath`, `Width`/`Height` (lazy from bytes), constructors, IBooleanResolvable (bytes.Length > 0), IWireWritable (mime-keyed dispatch).
- `this.Parse.cs` — `static Resolve(string raw, context)` interpreting path / data URL / base64; `static Resolve(byte[] raw, context)` from-bytes.

**Leaf-action consumers:** not part of this branch (image module actions are a follow-up). `file.read` ships the construction path (extension `.png`/`.jpg`/`.gif` → `Type = image`). `output.write` consumes via the dispatch.

**Wire dispatch:** asymmetric per format. See [dispatch.md](dispatch.md) `image` section for the full mime mapping.

**Why it earns its slot:** the hardest proof case for `IWireWritable` — same instance renders to four genuinely different wire shapes (bytes / base64 / `<img>` markup / path string). If the dispatch works for image, every future binary-category type (video, audio, document, archive) slots in by analogy without a single architectural change.

### `code` — text plus language tag

**Folder:** `app/types/code/`
**Files this branch creates:**
- `this.cs` — `Source`, `Language`, IBooleanResolvable (source non-empty), IWireWritable (mime-keyed: HTML wraps, others pass through as string).
- `this.Parse.cs` — `static Resolve(string raw, context)` — heuristic language detect (shebang, fenced code block header) or default `text` if none.

**Leaf-action consumers:** `code.run`, `code.validate`, `code.format` (follow-up branches). Not part of this branch's deliverable; `code` ships as a vocabulary entry first.

**Wire dispatch:** mostly-uniform string, HTML wraps for display. See [dispatch.md](dispatch.md) `code` section.

**Why it earns its slot:** text-shaped with semantic awareness for rendering — proves the third distinct dispatch pattern beyond uniform (number) and binary-asymmetric (image). Also lets the LLM start picking `code` over `string` for snippets where that's the right semantic, which sharpens the catalog vocabulary.

## The mechanical cleanups

These don't need format-aware rendering — they're rebinds of the existing flat-table entries. Confirmed with you on 2026-05-28.

| LLM-facing name | Old binding | New binding | Note |
|---|---|---|---|
| `datetime` | `System.DateTime` | `System.DateTimeOffset` | DateTime banished from production type bindings entirely. |
| `date` | `System.DateTime` | `System.DateOnly` | Calendar date. |
| `time` | `System.TimeSpan` | `System.TimeOnly` | Time-of-day. Rename `time`'s old TimeSpan meaning to `duration`. |
| `duration` | (didn't exist as name) | `System.TimeSpan` | New explicit name. `timespan` alias stays for now; deprecated. |

Each rebind moves a row in `app/types/this.cs:34`'s table. Each rebind also touches:

- `app/types/Conversion.cs` — `TryConvertTo` paths that hardcode the CLR type.
- `app/channels/serializers/TimeSpanIso8601.cs` — survives as-is; the type tag stays `duration`/`timespan`, the converter is type-keyed.
- `Wire.Read` / `Wire.Write` — DateTime cases become DateTimeOffset cases (the IWriter already has `DateTimeOffset(System.DateTimeOffset)`).
- Any test fixture or builder example that picked `datetime` against DateTime — relandable via test sweep.

These cleanups don't *require* the per-type-folder migration this branch establishes — they could ship as a smaller follow-up. But folding them in here closes the half-state Ingi specifically called out (`IsPrimitive` accepts DateTimeOffset, name table doesn't), and the new types (`number`, `image`, `code`) land alongside cleaned-up neighbors so the catalog reads coherently from day one.

**Folder shape:** `datetime` and `duration` get their own folders (`app/types/datetime/this.cs`, `app/types/duration/this.cs`) — both have parse / format complications worth owning (DateTimeOffset's tz-aware ISO-8601 round-trip; TimeSpan's `1.02:03:04` vs ISO-8601 duration formats). `date` and `time` stay as table-only entries since they're trivial CLR wrappers with no leaf-action complexity.

**On `duration` vs `timespan` as the LLM-facing name:** `duration` is the right name. PLang devs write prose ("a duration of 5 minutes") and pick types that read like the prose; `timespan` is C#-flavored. `timespan` stays as a deprecated alias for backwards compatibility on existing `.goal` files but the catalog leads with `duration` and the docs only mention it.

## Out of scope on this branch

`video`, `audio`, `document`, `archive`, `font`, `executable`, `machine-learning`, `gis-data`, `presentation`, `spreadsheet`, `ebook`, `certificate`, `calendar` — the remaining entries in the `app.formats` Kind enum.

Each is structurally identical to `image` (bytes + mime + format-asymmetric rendering). Lifting them is one-folder-per-type once the pattern is proven and the action surfaces (`video.thumbnail`, `document.extract-text`, `archive.list`) exist to consume them. Adding a type without action surfaces is just a label.

This branch's deliverable is *the dispatch pattern* plus *enough proving instances to validate it across the distinct shape categories* (uniform / binary-asymmetric / text-semantic). Once it's proven, every later type lands as a chore, not an architectural decision.

## Registration — replacing the flat `Primitives` table

Today at `app/types/this.cs:34`:

```csharp
private static readonly Dictionary<string, System.Type> Primitives = new(...)
{
    ["string"] = typeof(string),
    ["int"] = typeof(int),
    ["datetime"] = typeof(DateTime),
    // ...
};
```

Adding a new entry requires editing this dictionary, editing the `PrimitiveNames` reverse lookup directly below, editing `IsPrimitive` (`app/types/this.cs:430`), editing `Conversion.TryConvertTo`, editing any JSON converters, editing `IBooleanResolvable` if non-trivial truthiness. Six-file friction per addition.

Under this branch, each `app/types/<name>/this.cs` declares:

```csharp
[PlangType("image")]
public sealed class @this : app.data.IBooleanResolvable, app.modules.IContext
{
    public static @this Resolve(string raw, app.actor.context.@this context) { /* ... */ }
    // ...
}
```

Serialization is **not** an interface on the class — it lives in `serializer/<format>.cs` files beside it (see [dispatch.md](dispatch.md)). `number`, being a `readonly struct` value, drops `IContext` (no stored Context); class-shaped types like `image` may keep it.

`[PlangType]` takes just the PLang-facing name — the CLR type *is* the class the attribute is on, so the generator picks it up from the symbol it's scanning. No redundant `typeof(image.@this)` argument. (`[PlangType]` and the discovery scan already exist in `Registry.cs`.)

Registration is **discovery-time** (settled): the source generator scans `[PlangType]` classes + `serializer/*.cs` and emits the registration + dispatch tables. `ResolveType` / `ResolveName` / `IsPrimitive` dispatch through the registry. Adding a new type means adding a folder with an attributed class — one edit site. A DLL loaded at runtime registers via `RegisterRuntime` (the spine's "Extending the type vocabulary at runtime").

CLR primitives (`string`, `int`, `long`, …) keep their entries via a small bootstrap registration at registry construction. They're not full PLang types (no folder, no `Resolve` method needed — they ride through `Conversion`'s direct paths) but they participate in the same registry for name lookup.

`app.formats.@this` becomes the extension/MIME → PLang-name lookup that feeds the parse-in side (`file.read profile.png` → asks formats "what type for `.png`" → gets `"image"` → stamps `Data.Type = "image"`). It stops being a parallel universe; it becomes a helper to the type registry.

## Ownership matrix

The cross-cutting interfaces and surfaces each type-folder owns:

| Surface | number | image | code | path (retro) | datetime / duration (cleanups) |
|---|---|---|---|---|---|
| `static Resolve(string, context)` | ✓ | ✓ | ✓ | ✓ (exists) | ✓ if folder; else via Conversion |
| `IBooleanResolvable` | ✓ (0/NaN false) | ✓ (empty bytes false) | ✓ (empty source false) | ✓ (exists) | — |
| `serializer/` files | `Default.cs` (Kind→primitive) | `text` + `protobuf` + `Default` | `Default.cs` (+`html` later) | `Default.cs` (Relative string) | — (CLR primitives flow through IWriter directly) |
| `[PlangType]` registration | ✓ | ✓ | ✓ | retro-add | retro-add for folder cases |
| Leaf-action consumers | `math.*`, `list.*` reducers | (none this branch) | (none this branch) | `file.*`, `http.*` (exist) | `time.*` etc. (separate work) |

The five-by-five fits on a whiteboard. That's the test of whether the pattern scaled — if it doesn't fit, the cross-cutting surfaces have grown beyond what each type should own, and we should split.
