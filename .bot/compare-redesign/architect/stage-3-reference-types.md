# Stage 3: `file` + `directory` + `url` reference types

**Goal:** The `path`-rooted reference hierarchy, with `read` producing a reference, the value **narrowing to its content type on examination** (identity accumulates `item|file|dict`, `!` resolved chain-wide), and type-owned serialization. Also the base-class **demolition** on `path` (it carries content today).
**Scope:** The `file`/`directory`/`url` types and their navigation/serialization, and the `path` rework. The `.`/`!` resolver exists (Stage 2); this stage fills it in for these types. Comparison is Stage 4.
**Deliverables:**
- The hierarchy — `path` (location) with subtypes:
  ```
  path
    ├─ file       (path + lazy content + metadata)   ├─ image (file + Width/Height)  ├─ audio/video
    ├─ directory  (path + lazy list: list<path>)
    └─ url        (remote scheme: path + lazy fetched content + metadata)
  ```
  `file`/`directory`/`url` are **new**; `image` becomes a `file` specialisation. They reuse the existing `app.type.path.scheme` registry (`FilePath`/`HttpPath` are the scheme know-how).
- **`path` rework (demolition — see below):** rename the backing `_absolutePath` → private `_location` (the as-typed string); derive `absolute`/`relative`/`extension` from it; **remove `Content` and `Source`**; `ToString` location-only.
- `read X` → a `file` (local) or `url` (remote), or a recognised specialisation (`image`); unknown local → generic `file`. **Content-kind inference**: extension → the content's kind → the deserializer (`.json` → `dict`, `.csv` → `table`/`list`, unknown → `binary`).
- **Narrow-on-examination + chain-wide `!`**: `read X` → a reference (chain `[file, path, item]`, content lazy). Examining the content (`%x.field%`, `if %x% is dict`) reads + parses and **narrows** the value to its content type — same `Data`, `.Type` mutated in place, prior type retained → `[dict, file, path, item]` (newest first). `!` walks the chain: `%x!file!path%`/`%x!file!size%` reach the file facet whether or not it narrowed; `%x!type%` = headline, `%x!type.list%` = chain. `is file` always true post-read; `is dict` forces the narrow. No `!source`; **no double-storage** (the parsed item replaces the raw).
- `directory.list : list<path>` (its children's locations — `read` one to get content). `.` navigates the **headline** content (post-narrow `%file.x%` reaches the parsed data directly — no phantom `.content`).
- **Type-owned serialization** (each type's `Write(IWriter)` reads its own private fields — no universal `value` property): `path.Write` → the as-typed `_location`; `file.Write` → its content; `directory.Write` → its `list` (flat listing of locations); `image.Write` → its bytes. `text` stays **pure content** — no path.
**Dependencies:** Stage 2 (the door + resolver). Part of the 2–6 green unit.

## The `path` rework — demolition, and the right backing

`path` carries content today and presumes "absolute," both wrong for the target:
- **Content lives on `path` today and must move.** `path.@this` has `public object? Content` (`path/this.cs:169`) and `public string? Source` (`:173`), and `ToString()` returns **`Content?.ToString()` first** (`:177-185`). So a `path` *is* the content-bearer after `file.read` today. Remove `Content`/`Source`; content becomes the `file`'s; `path.ToString` goes location-only. (Repoint the ~9 `.Content` sites.) `Source` is dropped outright — it duplicated the location and we don't keep value-history.
- **The backing is the as-typed location, not "absolute."** Rename `protected readonly string _absolutePath` → **private `_location`**, holding the string the user gave, verbatim — `"//file.txt"` (host-OS root), `"/file.txt"` (app root), `"file.txt"`/`"test/try.txt"` (relative), `"c:/my/path.txt"` (absolute), `"http://…"` (url). `absolute`/`relative`/`extension`/etc. are **derived** from it (cached), per the scheme's resolution rules.

```csharp
public abstract partial class @this : item.@this, module.IContext   // app.type.path
{
    protected readonly string _location;       // private, as-typed, verbatim
    private string? _absolute, _relative, _extension, _fileName, _directory;  // derived, cached
    public abstract string Scheme { get; }
    // Content, Source: removed. ToString: location-only.
    public override void Write(IWriter w) => w.String(_location);   // type owns its wire shape; reads its own private field
    // public ! surface (typed, derived): !relative, !absolute (GATED, unserialised), !extension (serialised)
}
```

`_location` stays **private** — `Write(IWriter)` is a method on the class and reads it directly, so no public `value`/`location` property is needed (same pattern as `text.Write` reading its private string; this is *why* no public raw accessor / no `ToRaw` leaks it). A developer reads the location via **`%path%`** (the value, rendered from `_location`); `%path!absolute%` is the *resolved* host path (gated, unserialised — it's the form that leaks the install root); `%path!extension%` is serialised.

## Design

**A reference narrows to its content type on examination; its identity accumulates.** `read config.json` → a `file` (chain `[file, path, item]`, content lazy). The file's own metadata — `!file!path`, `!file!size`(stat), `!file!scheme` — needs **no materialise** (it's the location/stat surface). Examining the content (`%config.database%`, `if %config% is dict`) reads + parses and **narrows** `%config%` to its content type (`dict`/`list`): the same `Data`, `.Type` mutated in place, `file` retained in the chain. After the narrow `%config%` `.Is(dict)` **and** `.Is(file)` — `.` navigates the dict (`%config.database%`), `%config!file!path%` still gives the location, `%config!type%` → `dict`, `%config!type.list%` → `[dict, file, item]`.

**`!` is chain-wide — that's the whole point.** `%config!file!path%` resolves whether or not `config` narrowed (file is in the chain on both branches), so `%config!file%` never crashes on the branch that didn't examine the content. Headline-only resolution was the footgun. `is file` is always true post-read; `is dict` forces the narrow (so it's deterministic). Type-logic uses `is X`. `write out %config%` is the raw bytes until a narrow, then reserialises (single-storage); a `set %config.y%` rebinds to a fresh dict (the invalidation, and the only thing that changes write-out across branches).

The split per type is the type's call (the meta-rule): `file` owns `!path`/`!size`(stat)/`!modified`; `directory` owns `!path`/`!list`; `url` owns `!path`/`!host`. What `!size` means per type (stat byte-size vs content byte-count) is the type's decision — settle it as you build each. **Terminal types** (`image`, `directory`) don't narrow — their content type is already known, so they stay the headline.

**`read` → a reference; structures hold paths.** `read` of a file-path → `file` (content narrows on examination); of a dir-path → `directory` (`.list : list<path>`, terminal); of a remote path → `url` (fetches + narrows on examination). A `directory`'s `list` holds **`path`** (locations), not content-bearing files — so nothing in a listing carries content to dump. **Bare-scalar contract (must stay green):** `%config%` used as a scalar / `write out %config%` yields the **content** (raw bytes pre-narrow, the parsed item post-narrow), never the location string — so existing `read`-then-use goals don't silently start emitting `"config.json"`. (Integration cut in `plan/test-strategy.md`.)

**Serialization is type-owned (OBP rule 9).** Each type's `Write(IWriter)` reads its own private fields and emits what it chooses — there is no universal `value` property. `path.Write` → the as-typed `_location` (relative/url form; `absolute` is gated and stays off the wire); `file.Write` → its content; `directory.Write` → its `list<path>` (flat listing of locations; a recursive tree is an explicit walk); `image.Write` → bytes. Because a `path` serialises a single string (no content) and listings hold paths, write-out never recurses into file content — no subject/nested context-bit needed.

**Naming (Ingi):** `directory`'s listing is `list` (not `Entries`); `url` over `uri`; `text` carries no path; the path backing is `_location` (not `_absolutePath`).
