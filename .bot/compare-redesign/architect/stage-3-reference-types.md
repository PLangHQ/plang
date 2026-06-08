# Stage 3: `file` + `directory` + `url` reference types

**Goal:** The `path`-rooted reference hierarchy, with `read` producing content, references **stable** (they hold content, never become it), the two-layer `!` plane, and type-owned serialization. Also the base-class **demolition** on `path` (it carries content today).
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
- **Stable, two-layer `!`**: a reference *holds* content (never replaced by it). `!path`/`!scheme`/`!host` are its **own** (intrinsic, **no materialise**); content props (`!size`, the data) forward to — and **materialise** — the content. `%url!path%` ≠ fetch; `%url!size%` fetches. No `!source`.
- `directory.list : list<path>` (its children's locations — `read` one to get content). `.` on a reference forwards into its materialised content (`%file.x%` ≡ `%file!content.x%`).
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

**A reference is a stable `path` subtype that holds lazy content.** It doesn't transform into its content — `%file%` stays a `file`, which is why `!path` always works. Its `!` has two layers (Stage 2's split):
- **own (location), no materialise** — `!path`, `!scheme`, `!host`, the location projections; intrinsic via the `path` inheritance.
- **content, materialise-on-touch** — `!size`, the data; forwarded to the materialised content. `%url!size%` fetches; `%dir.0%` materialises the listing.

The split per type is the type's call (the meta-rule): `file` owns `!path`/`!size`(stat)/`!modified`; `directory` owns `!path`/`!list`; `url` owns `!path`/`!host`. What `!size` means per type (stat byte-size vs content size) is the type's decision — settle it as you build each.

**`read` → content; structures hold paths.** `read` of a file-path → `file` (content materialises on use); of a dir-path → `directory` (`.list : list<path>`); of a remote path → `url` (fetches on use). A `directory`'s `list` holds **`path`** (locations), not content-bearing files — so nothing in a listing carries content to dump. *(This `read`-returns-a-reference shape — vs today's `read`-returns-content — and its bare-scalar contract are coder finding 4, settled in the next round; the integration cut is in `plan/test-strategy.md`.)*

**Serialization is type-owned (OBP rule 9).** Each type's `Write(IWriter)` reads its own private fields and emits what it chooses — there is no universal `value` property. `path.Write` → the as-typed `_location` (relative/url form; `absolute` is gated and stays off the wire); `file.Write` → its content; `directory.Write` → its `list<path>` (flat listing of locations; a recursive tree is an explicit walk); `image.Write` → bytes. Because a `path` serialises a single string (no content) and listings hold paths, write-out never recurses into file content — no subject/nested context-bit needed.

**Naming (Ingi):** `directory`'s listing is `list` (not `Entries`); `url` over `uri`; `text` carries no path; the path backing is `_location` (not `_absolutePath`).
