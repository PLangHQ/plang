# Types — the vocabulary inventory

This file goes deep on what types ship on this branch, what each owns, the registration shape, and the cleanup of the existing flat `Primitives` table. The spine ([../plan.md](../plan.md)) locks the architectural decision; this locks the inventory.

## The pattern, restated

Every PLang type lives at `app/types/<name>/`:

- `this.cs` — the value. A `sealed class` for most types (`number`, `image`, `code`), `abstract` for variant families (`path` — file vs http have different storage and dispatch). `number` is a class for codebase consistency though it's a *value* semantically (immutable, value equality). Carries `[PlangType("name")]` and implements `app.data.IBooleanResolvable` (truthiness); types that need the runtime may also implement `app.modules.IContext` (`number`, a value, does not).
- **`static Resolve(value, context) → @this`** — the runtime construction factory (`Resolve(string,…)`, plus `Resolve(byte[],…)` for binary types). `app.types.Conversion.TryConvertTo` dispatches here.
- **`static Build(value) → kind`** — the *build-time* sibling of `Resolve`: reads the value's refinement (`kind`) for the `.pr` without constructing the value. `number.Build(3.5)→"decimal"`, `image.Build("a.jpg")→"jpg"`, `path.Build("https://…")→"http"`. The `kind` is a separate `.pr` field, never a `type:kind` string (no runtime split). Each type owns its kind determination; the LLM is shown a type's kinds only when developer-meaningful (number's precision), otherwise `Build()` derives it silently.
- `serializer/<format>.cs` — one file per (type, format) rendering; `Default.cs` is the uniform fallback. Serialization is **not** an interface on the value; it's these files (see [dispatch.md](dispatch.md)).
- **Properties carry their own types**, so the LLM navigates: an `image` exposes `Path(path)` (nullable), and the type catalog reads `image(path) => Exif, Width, Height, Path(path)`. `%photo.Path.Exists%` resolves because the catalog says `Path` is a `path` and `path` has `Exists`. Composition, not a `path|image` union.
- Sub-files at the type's discretion: variant subfolders (`path/file/`, `path/http/`), surface partials (`this.Operations`, `this.Authorize`).

The type does **not** depend on any channel, and no channel depends on a type. The bridge is the writer's `Format` token: `Normalize` tags a registered value as `TypedValueNode`, the writer looks up `(Data.Type, Format)` and calls the type's serializer file.

## The three proving instances

### `number` — tagged-union numeric

**Folder:** `app/types/number/`
**Files this branch creates:**
- `this.cs` — `sealed class @this` (immutable), `NumberKind` enum + storage slots (`_i`, `_d`, `_f`), `Kind`, `static From(int|long|decimal|float|double)`, implicit-IN operators.
- `this.Parse.cs` — `static Resolve` / `Parse` / `TryParse` (runtime construction).
- `this.Build.cs` — `static Build(value)→kind`: the build-time kind determiner (decimal point → decimal, `e` → double, else int/long). Distinct from the action `IClass.Build()` hook.
- `this.Operators.cs` — operator overloads `+ - * / %` (lenient default).
- `this.Arithmetic.cs` — `static Add(@this, @this, NumberPolicy)` and siblings; policy-aware, `Data`-returning; called by `math.*` handlers.
- `this.Equality.cs` — lenient `Equals` + `ExactEquals` + canonical `GetHashCode`.
- `Config.cs` / `NumberPolicy.cs` — the policy axes and resolved struct.
- `serializer/Default.cs` — `(number, *)` → the matching `IWriter` numeric primitive per `Kind`.

**Full design detail:** [storage.md](storage.md) (storage, parse, operators, equality, IBooleanResolvable, ToString) and [policy.md](policy.md) (NumberPolicy, the two axes, the `app.config` resolver).

**Leaf-action consumers:** all of `math.*` (`add`, `subtract`, `multiply`, `divide`, `modulo`, `power`, `abs`, `ceiling`, `floor`, `round`, `max`, `min`, `sqrt`, `random`, `intdiv`) plus the numeric reducers `list.sum`, `list.avg`, `list.min`, `list.max`. Each grows optional `Overflow` / `Precision` parameters and resolves policy via `app.config.For<number.Config>`.

**Wire dispatch:** uniform — one `Default.cs` picks the writer primitive by `Kind`.

**Why it earns its slot:** the value-with-internal-variants proof. Also closes the existing `MathHelper.ToDouble` / `MathHelper.PreserveType` scattering — deleted at the end of the math retype.

### `image` — bytes plus mime, format-asymmetric

**Folder:** `app/types/image/`
**Files this branch creates:**
- `this.cs` — `Bytes`, `Mime`, `Path` (type `path`, nullable — null for a base64-decoded image), `Width`/`Height` (lazy from bytes), constructors, `IBooleanResolvable` (bytes.Length > 0).
- `this.Parse.cs` — `static Resolve(string raw, context)` interpreting path / data URL / base64; `static Resolve(byte[] raw, context)` from-bytes.
- `this.Build.cs` — `static Build("a.jpg") → kind "jpg"` from the extension.
- `serializer/text.cs` (path placeholder), `serializer/protobuf.cs` (raw bytes, when that writer ships), `serializer/Default.cs` (base64 — covers json + plang).

**Leaf-action consumers:** not part of this branch (image module actions are a follow-up). `file.read` ships the construction path (extension `.png`/`.jpg`/`.gif` → `Type = image`). `output.write` consumes via the dispatch.

**Why it earns its slot:** the hardest proof — same instance renders to genuinely different wire shapes (raw bytes / base64 / path placeholder). If the per-format files work for image, every future binary-category type (video, audio, document, archive) slots in by analogy without an architectural change.

### `code` — text plus language tag

**Folder:** `app/types/code/`
**Files this branch creates:**
- `this.cs` — `Source`, `Language`, `IBooleanResolvable` (source non-empty).
- `this.Parse.cs` — `static Resolve(string raw, context)`.
- `this.Build.cs` — `static Build(src) → kind` (the language: `csharp`/`python`/… by heuristic — shebang, fenced-block header — or `text`).
- `serializer/Default.cs` — `writer.String(Source)`. (An `html.cs` that wraps in `<pre><code>` lands once an HTML writer exists.)

**Leaf-action consumers:** `code.run`, `code.validate`, `code.format` (follow-up branches). Not part of this branch's deliverable; `code` ships as a vocabulary entry first.

**Why it earns its slot:** text-shaped with semantic awareness for rendering — the third distinct pattern beyond uniform (number) and binary-asymmetric (image). Also lets the LLM pick `code` over `string` for snippets where that's the right semantic, sharpening the catalog vocabulary.

## The mechanical cleanups

These don't need format-aware rendering — they're rebinds of existing flat-table entries.

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

These cleanups don't *require* the per-type-folder migration — they could ship as a smaller follow-up. But folding them in here closes the existing half-state (`IsPrimitive` accepts `DateTimeOffset`, the name table doesn't), and the new types land alongside cleaned-up neighbors so the catalog reads coherently from day one.

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

Serialization is **not** an interface on the class — it lives in `serializer/<format>.cs` files beside it (see [dispatch.md](dispatch.md)). `number` drops `IContext` (it's a value — no stored Context); types that genuinely need the runtime may keep it.

`[PlangType]` takes just the PLang-facing name — the CLR type *is* the class the attribute is on, so the generator picks it up from the symbol it's scanning. No redundant `typeof(image.@this)` argument. (`[PlangType]` and the discovery scan already exist in `Registry.cs`.)

Registration is **discovery-time** (settled): the source generator scans `[PlangType]` classes + `serializer/*.cs` and emits the registration + dispatch tables. `ResolveType` / `ResolveName` / `IsPrimitive` dispatch through the registry. Adding a new type means adding a folder with an attributed class — one edit site. A DLL loaded at runtime registers via `RegisterRuntime` (the spine's "Extending the type vocabulary at runtime").

CLR primitives (`string`, `int`, `long`, …) keep their entries via a small bootstrap registration at registry construction. They're not full PLang types (no folder, no `Resolve` method needed — they ride through `Conversion`'s direct paths) but they participate in the same registry for name lookup.

`app.formats.@this` becomes the extension/MIME → PLang-name lookup that feeds the parse-in side (`file.read profile.png` → asks formats "what type for `.png`" → gets `"image"` → stamps `Data.Type = "image"`). It stops being a parallel universe; it becomes a helper to the type registry.

## Ownership matrix

The cross-cutting interfaces and surfaces each type-folder owns:

| Surface | number | image | code | path (retro) | datetime / duration (cleanups) |
|---|---|---|---|---|---|
| `static Resolve(string, context)` | ✓ | ✓ | ✓ | ✓ (exists) | ✓ if folder; else via Conversion |
| `static Build(value)→kind` | ✓ (decimal/int/…) | ✓ (jpg/png/…) | ✓ (csharp/…) | ✓ (file/http) | — (no kind) |
| LLM shown the kinds? | ✓ (precision) | — (derived) | — (derived) | — (derived) | — |
| `IBooleanResolvable` | ✓ (0/NaN false) | ✓ (empty bytes false) | ✓ (empty source false) | ✓ (exists) | — |
| `serializer/` files | `Default.cs` (Kind→primitive) | `text` + `protobuf` + `Default` | `Default.cs` (+`html` later) | `Default.cs` (Relative string) | — (CLR primitives flow through IWriter directly) |
| `[PlangType]` registration | ✓ | ✓ | ✓ | retro-add | retro-add for folder cases |
| Leaf-action consumers | `math.*`, `list.*` reducers | (none this branch) | (none this branch) | `file.*`, `http.*` (exist) | `time.*` etc. (separate work) |

The five-by-five fits on a whiteboard. That's the test of whether the pattern scaled — if it doesn't fit, the cross-cutting surfaces have grown beyond what each type should own, and we should split.
