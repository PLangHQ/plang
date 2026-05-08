# Stage 18: `mime-table-split`

**Read first:**
- `plan/principles.md` — OBP discipline (smell #4, allocate-here / mutate-there).
- `plan/scope-map.md` — Formats is shared (App-level); same scope as today's MimeTypes table, just a proper home.

**Goal:** Two tables conflating two jobs need to split. `Utils/MimeTypes.cs` (66 lines, static class) does forward-lookup `ext → MIME` AND reverse-lookup `MIME → CLR type` — different concerns. `Types/this.cs:215-315` has its own (overlapping) ext→MIME and ext→Kind tables plus `Compressible`/`KindOf` methods. After this stage:

- **Forward I/O concerns** (ext→MIME, ext→Kind, Compressible, KindOf — the "what is this file format" methods) consolidate in a new `App/Formats/this.cs` (mount: `app.Formats`).
- **Reverse type-resolution** (MIME → CLR type) becomes a new overload `app.Types.Clr(mimeType)` alongside the existing `app.Types.Clr(plangName)`.
- `Utils/MimeTypes.cs` deletes (legacy static class; replaced by Formats + Types).

**Scope:**
- *Included:* create `App/Formats/this.cs`; move the MIME/Kind/Compressible block from `Types/this.cs:14, 215-315` (+ associated methods at lines 386, 398, 410, 421, 442, 446, 458, 459) into Formats; add `Clr(mimeType)` overload to `Types.@this`; delete `Utils/MimeTypes.cs`; update the 2-3 callers in `Utils/TypeMapping.cs`.
- *Excluded:* anything else. Both Types and Formats stay shared App-level.

**Deliverables:**

### New folder + file

```
App/Formats/
└── this.cs               (NEW — absorbs ext→MIME, ext→Kind, Compressible, KindOf, Add, Remove)
```

`namespace App.Formats;`

The class shape is a faithful translation of the existing instance methods on Types.@this:

```csharp
public sealed class @this
{
    private readonly ConcurrentDictionary<string, string> _extensionToKind = new(StringComparer.OrdinalIgnoreCase) { /* ... */ };
    private readonly ConcurrentDictionary<string, string> _extensionToMime = new(StringComparer.OrdinalIgnoreCase) { /* ... */ };
    private readonly HashSet<string> _allKinds;
    private readonly HashSet<string> _compressibleKinds = new(StringComparer.OrdinalIgnoreCase) { /* text, json, xml, csv, markdown, yaml */ };

    public @this()
    {
        _allKinds = new HashSet<string>(_extensionToKind.Values, StringComparer.OrdinalIgnoreCase);
        // ... (same init logic as today's Types.this.cs:328-339)
    }

    public string? Kind(string extension) { /* same logic as Types.this.cs:386 */ }
    public string Mime(string extension) { /* same as Types.this.cs:398 */ }
    public bool Compressible(string kind) { /* same as Types.this.cs:410 */ }
    public string? KindOf(string typeValue) { /* same as Types.this.cs:421 */ }
    public void Add(string ext, string kind, string mime) { /* same as Types.this.cs:442 */ }
    public bool Remove(string ext) { /* same as Types.this.cs:458 */ }
}
```

### App.this.cs — gain Formats property

```csharp
public Formats.@this Formats { get; } = new();
```

Allocated at field-init, same shape as other shared-App-level properties (Modules, Providers, Errors, etc.).

### Types/this.cs — drop the MIME block; gain Clr(mimeType) overload

Delete:
- The two `ConcurrentDictionary<string, string>` fields (`_extensionToKind` line 14, `_extensionToMime` line 215).
- `_allKinds`, `_compressibleKinds` fields.
- The methods `Kind(string)`, `Mime(string)`, `Compressible(string)`, `KindOf(string)`, plus `Add(...)` and `Remove(...)` for ext mappings.
- The init block at line 328 that builds `_allKinds`.

Add:
- `public System.Type? Clr(string mimeType)` overload — moves the logic from `MimeTypes.TryGetClrType` into Types (a method named `Clr` next to the existing `Clr(plangName)`):

```csharp
// Existing:
public System.Type? Clr(string plangName) { /* ... */ }

// New overload:
public System.Type? Clr(string mimeType)
{
    if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.Contains('/')) return null;
    if (mimeType.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase))
        return typeof(string);
    if (mimeType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase) ||
        mimeType.StartsWith("audio/", System.StringComparison.OrdinalIgnoreCase) ||
        mimeType.StartsWith("video/", System.StringComparison.OrdinalIgnoreCase))
        return typeof(byte[]);
    if (mimeType.Equals("application/json", System.StringComparison.OrdinalIgnoreCase)) return typeof(object);
    if (mimeType.Equals("application/plang-goal", System.StringComparison.OrdinalIgnoreCase))
        return typeof(App.Goals.Goal.@this);
    if (mimeType.Equals("application/octet-stream", System.StringComparison.OrdinalIgnoreCase))
        return typeof(byte[]);
    return null;
}
```

(C# overload resolution disambiguates by parameter content. If both overloads have `string` parameter, the compiler can't tell — name them differently or rename one. Suggest: keep `Clr(plangName)` and rename the new one `ClrFromMime(mimeType)` for clarity. Or use a wrapper type. Coder's call.)

### Delete `App/Utils/MimeTypes.cs`

Both methods migrated to the right places.

### Caller sweep

`PLang/App/Utils/TypeMapping.cs:135, 141-142`:

```csharp
// Today:
var mimeType = MimeTypes.TryGetClrType(typeName);
// ...
public static string GetMimeType(string extension) => MimeTypes.GetMimeType(extension);

// After:
// MimeTypes is gone. Update the call sites:
//   MimeTypes.TryGetClrType(typeName)  → context.App.Types.ClrFromMime(typeName)
//                                         (or however the overload is named)
//   MimeTypes.GetMimeType(extension)   → context.App.Formats.Mime(extension)
```

The TypeMapping `GetMimeType` static forwarder at line 142 ("Prefer MimeTypes directly" per the doc comment) — delete it; consumers use `app.Formats.Mime(ext)` directly.

Existing callers of `Types.@this.Mime/Kind/Compressible/KindOf/Add/Remove` (instance methods on Types) — sweep all to `app.Formats.X` instead. Grep `\.Mime(\|\.Kind(\|\.Compressible(\|\.KindOf(\|_extensionToMime\|_extensionToKind` across PLang/ and PLang.Tests/.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `find PLang/App/Utils/MimeTypes.cs` — gone.
- `find PLang/App/Formats/this.cs` — exists.
- `app.Formats` property exists; `app.Types.Clr(mimeType)` overload exists.
- `grep -rn "MimeTypes\." PLang/ PLang.Tests/ --include='*.cs'` — zero hits.

**Dependencies:** None on stages 17/21/22. Independent.

## Design

### The smells this closes

Two:

1. **Same logical thing stored twice across types** (smell #3). `Utils/MimeTypes.cs` has its own ext→MIME table. `Types/this.cs:215` has another. Both populated similarly; both used. Two sources of truth for the same mapping.

2. **Methods on the wrong class.** `Types.@this.Mime/Kind/Compressible/KindOf` aren't about *type identity* — they're about *file format characteristics*. Types should be plang-name ↔ CLR type identity, with one bridging method (`Clr(mimeType)`) for "given a content-type, what CLR type to deserialize to." The format-table belongs in its own home.

### Files touched

**Modified (3):** `App/Types/this.cs` (drop MIME block; add Clr overload), `App/this.cs` (add Formats property), `App/Utils/TypeMapping.cs` (caller updates).

**Created (1):** `App/Formats/this.cs`.

**Deleted (1):** `App/Utils/MimeTypes.cs`.

### Risk + dependencies

**Risk: medium.** Type system + caller sweep + overload resolution are the touchy parts.

Possible failure modes:
1. **C# overload resolution** between `Clr(string plangName)` and the new `Clr(string mimeType)` — same signature. Either rename one method or differentiate by some other means. Suggest renaming the new overload to `ClrFromMime`.
2. **Caller sweep gap on `Types.@this.Mime/Kind/Compressible/KindOf`** — the build catches if the methods are gone but a caller still references them.
3. **Existing tests for the MIME tables** — `PLang.Tests/App/Channels/Serializers/MimeRegistrationTests.cs` (or wherever) — verify they exercise `app.Formats` after the move.

**Dependencies: none.**

### Tests

**No new tests required.** Behavior preserved.

**Existing test coverage to verify:**
- MIME-related test files (search `PLang.Tests/` for mime/Mime).
- `Tests/` — full PLang suite.

### Watch for (coder eyes-on)

- **Overload disambiguation** — `Clr(string)` exists today for plang names. The new MIME overload needs a different signature or different name. Suggest `ClrFromMime(string)` or pass a wrapper type.
- **The data tables in Types/this.cs:14, 215** — substantial. Copy verbatim into Formats; preserve all entries.
- **The init block at line 328** — `_allKinds` populated from `_extensionToKind.Values`. Move to Formats' ctor.
- **Concurrent dictionary semantics** — preserved. Don't introduce locks; the existing types are thread-safe.
- **The `Compressible` HashSet contents** — preserve verbatim (text/json/xml/csv/markdown/yaml are the compressible kinds today).

### Stages that follow this one

- Stage 22 (`app-shortcuts-drop`) — same Tier 4 batch; independent.
- Stages 15 (compound-name-rename), 16 (static eviction), 19 (Provider→Code) — own sessions.

### Out of scope

- Renames inside `App/Formats/` (the class name, suffix dropping, etc.) — Rule A territory; not stage 18.
- Changes to MIME family handling (image/audio/video/application) — preserved as-is.

## Commit plan

```
runtime2-cleanup stage 18: split MIME table into Formats + Types.Clr(mimeType)

Two tables conflated two jobs. Utils/MimeTypes.cs (static class) had
both forward (ext → MIME) and reverse (MIME → CLR type) lookups.
Types/this.cs:215-315 had its own ext→MIME + ext→Kind tables and
methods (Mime, Kind, Compressible, KindOf, Add, Remove). Same data,
two homes. Plus Types methods conflate "type identity" with "file
format characteristics."

After:

  App/Formats/this.cs (NEW)  ← ext→MIME, ext→Kind, Compressible,
                               KindOf, Add, Remove (instance methods).
                               Mount: app.Formats.

  Types.@this.Clr(plangName)         (existing, plang-name → CLR)
  Types.@this.ClrFromMime(mimeType)  (NEW, MIME → CLR)

  Utils/MimeTypes.cs DELETED — legacy static class; both jobs migrated
                                to the right homes.
  Utils/TypeMapping.cs:142 forwarder DELETED.

Caller sweeps: 2-3 sites in TypeMapping.cs; existing
Types.@this.Mime/Kind/Compressible/KindOf callers sweep to
app.Formats.X instead.

Both Formats and Types stay shared App-level. No scope changes.
```
