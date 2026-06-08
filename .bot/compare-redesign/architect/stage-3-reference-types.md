# Stage 3: `file` + `directory` + `url` reference types

**Goal:** The `path`-rooted reference hierarchy, with `read` producing content, references **stable** (they hold content, never become it), the two-layer `!` plane, and type-owned serialization.
**Scope:** The `file`/`directory`/`url` types and their navigation/serialization. The `.`/`!` resolver exists (Stage 2); this stage fills it in for these types. Comparison is Stage 4.
**Deliverables:**
- The hierarchy — `path` (location) with subtypes:
  ```
  path
    ├─ file       (path + lazy content + metadata)   ├─ image (file + Width/Height)  ├─ audio/video
    ├─ directory  (path + lazy list: list<path>)
    └─ url        (remote scheme: path + lazy fetched content + metadata)
  ```
  `file`/`directory`/`url` are **new**; `image` becomes a `file` specialisation. They reuse the existing `app.type.path.scheme` registry (`FilePath`/`HttpPath` are the scheme know-how).
- `read X` → a `file` (local) or `url` (remote), or a recognised specialisation (`image`); unknown local → generic `file`. **Content-kind inference**: extension → the content's kind → the deserializer (`.json` → `dict`, `.csv` → `table`/`list`, unknown → `binary`).
- **Stable, two-layer `!`**: a reference *holds* content (never replaced by it). `!path`/`!scheme`/`!host` are its **own** (intrinsic via the `path` inheritance, **no materialise**); `!size`/data forward to — and **materialise** — the content. `%url!path%` ≠ fetch; `%url!size%` fetches. No `!source`.
- `directory.list : list<path>` (its children's locations — `read` one to get content). `.` on a reference forwards into its materialised content (`%file.x%` ≡ `%file!content.x%`).
- **Type-owned serialization**: `path` → its location string; `file` → its content; `directory` → its `list` (serialise `list<path>` → location strings, a flat listing); `image` → its bytes. `text` stays **pure content** — no `.Path`.
**Dependencies:** Stage 2 (the door + resolver). Part of the 2–6 green unit.

## Design

**A reference is a stable `path` subtype that holds lazy content.** It does not transform into its content — `%file%` stays a `file`, which is why `!path` always works. Materialising produces the **content** (`item`/`image`/`text`/`binary`) as a facet reached through `!content`; `.` is sugar that forwards into it. The two `!` layers:
- **own (location), no materialise** — `!path`, `!scheme`, `!host`, `!exists` (the type's declared location surface).
- **content, materialise-on-touch** — `!size`, `!width`, the data; the reference forwards these to its materialised content. `%url!size%` fetches; `%dir.0%` materialises the listing.

The split per type is the type's call (the meta-rule): a `file` owns `!path`/`!size`(stat)/`!modified` and forwards data; a `directory` owns `!path` and exposes `!list`; a `url` owns `!path`/`!host` and forwards content props. What `!size` *means* per type (stat byte-size vs content size) is the type's decision — settle it as you build each.

**`read` → content; structures hold paths.** `read` of a file-path → `file` (content materialises on use); of a dir-path → `directory` (`.list : list<path>`); of a remote path → `url` (fetches on use). A `directory`'s `list` holds **`path`** (locations), not content-bearing files — so nothing in a listing carries content to dump. To get a child's content you `read` its path.

**Serialization is type-owned (OBP rule 9), and `list<path>` keeps it clean.** `write out %dir%` serialises its `list<path>` → each `path` → its location string → a flat listing (a recursive tree is an explicit walk). `write out %file%` → its content. `write out %path%` → its location string. Because a `path` has a **single** serialization (no content) and listings hold paths, write-out never recurses into file content and **no subject/nested context-bit is needed**. `%file!content%` is the explicit content value.

**Naming carried in (Ingi):** `directory`'s listing is `list` (not `Entries`); `url` over `uri`. `text` carries no path — the path lives on the `file`/`url`.
