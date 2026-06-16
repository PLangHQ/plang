# Mime → binary/kind, lazy narrowing (compare-redesign)

**Decided with Ingi, 2026-06-16.** Supersedes the §C "where does the declared
label ride" question — the answer is "it doesn't; undecoded content is `binary`."

## The model (one rule)

**Provenance decides.**
- **Bytes off I/O** (file / http / stream) → always `binary` + kind, **lazy**.
  `.Type` (sync) reports `binary`; the kind says *how to read*. On value access
  (`Value()` via the `.` navigation door) the kind's reader decodes and the
  holding `Data` rebinds to the real type (`dict`/`list`/`image`/`table`/`text`).
- **A value already in memory** keeps its own type; `as <kind>` only refines the
  kind (`%blog.body% as md` → `text/md`, no binary stage).

`item` / `clr` never come out of mime-typing. `clr` is host-objects-only.

`%x!type%` → Data's own `Type` property (sync) → `binary`. `%x.foo%` → `.`
navigates into the value → `Value()` parses → narrowed type.

octet-stream / unknown mime → `binary`, kind **null** (nothing to decode).

## Reader lookup: kind → inner type → that type's reader

`Readers.Of` is type-primary (`(type,kind)` → `(type,*)`), no `(*,kind)`. A
`binary/json` holder misses. The fallback: **the kind names the inner type**, then
look up that type's reader.
- `binary/json` → kind `json` → type `item` → `(item, json)` → dict/list
- `binary/jpg`  → kind `jpg`  → type `image` → `(image, *)` (wildcard) → image
- `binary/csv`  → kind `csv`  → type `table` → `(table, csv)` → table
- `binary/md`   → kind `md`   → type `text`  → `(text, *)` → text

## Edits (in order)

1. **`Format.TypeFromMime`** → uniform `binary` + canonicalised-subtype kind.
   Drop the media / tabular / item branches. octet-stream → `binary` null kind.
2. **Reader fallback** → kind → inner type → retry the lookup. (Owner + name TBD
   with Ingi — see open question.)
3. **Drop the `(object, json)` reader** (json → `item`). Full `object`-type
   removal is a separate ~37-site pass.
4. **`file read.cs:69-79`** → delete the eager-image special case (images lazy now).
5. **`channel.StampType:313`** → drop the `text/plain → text` shortcut (text off
   I/O is `binary` too).
6. **`clr` §C** → remove the `Judge` label arms + `clr._declared`/`Labeled`.
7. **Tests** → rewrite the 7 assertions to `binary/<kind>`; the strict
   `as image/gif` ones stay `image` (set validates+elevates). Redirect/delete the
   test-only `Type.FromMime`.

## Open question (for the reader fallback)
Media readers are **wildcard** (`(image, *)`), so a kind→inner-type that comes
purely from "search the registry for kind X" finds `json`/`csv` but not `jpg`.
The jpg→image step needs the family map (`KindsByFamily`). So "kind → inner type"
spans two sources: the registry's own kind entries + the family map. Where it
lives and what it's named is the thing to settle before coding edit 2.
