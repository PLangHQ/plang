# Formats as kinds of their types (decision 113): the architect's own trace, facts only

Written before reading the coder's trace. The comparison is at the end.

## The format table's families (`format/list/this.cs`, `_extensionToKind`, counted 2026-09-27)

| family | extensions | is it a type today? (declared words from `types.json`) |
|---|---|---|
| code | 15 | **yes** (`code`) |
| image | 10 | **yes** (`image`) |
| database | 10 | no |
| text | 8 | **yes** (`text`) |
| certificate | 7 | no |
| video | 6 | no |
| package | 6 | no |
| document | 6 | no |
| audio | 6 | no |
| archive | 6 | **yes** (`archive`) |
| spreadsheet | 5 | no, but the tabular MIMEs already stamp as **`table`** (`_tabularMimeToKind`: csv/xls/xlsx/ods) |
| config | 5 | no |
| presentation | 4 | no |
| font | 4 | no |
| subtitle, ebook, checksum | 3 each | no |
| vector, executable, email | 2 each | no |
| plang (`.goal`), log, calendar | 1 each | no (`.goal`'s MIME is `text/plain`) |
| goal (`.pr`) | 1 | **yes** (`goal`; MIME `application/plang-goal`, kind `pr` per the reader) |
| binary | 1 | **yes** (`binary`) |

So 7 families have a type: code, image, text, archive, goal, binary, and table (for spreadsheets). **About 18 families have no type**, covering about 75 extensions.

## What a family is used for today (why it exists at all)

- `Format.Kind(ext)` → family, the input to `kind.type(ctx)`'s fallback (`Owner ?? Reader.TypeOf ?? Format.Kind ?? "binary"`).
- `KindsByFamily()` → family → extensions, read by the empty kind's `list(context)` (`type/kind/empty/this.cs:48-53`) to list "the formats that are text".
- `Compressible(family)`: `_notCompressible` = image, video, audio, archive.
- `FamilyOf(mime)` (order-dependent). No other production use.

So for most of the ~18 type-less families, the family only answers compressible and teaching questions. Nothing reads their content differently: it stays binary with the extension as kind.

## What the shape needs to decide (for Ingi)

**Families with no type:** where do their ~75 extensions live? Three options:
1. **binary owns them:** `{binary, mp4}`, and compressible is a fact on the kind. Simplest, and it matches "many just fall to binary". The family names (video, audio, …) disappear as categories.
2. **Types for the families that mean something** (video and audio are not compressible, and playable; document, font, …), with binary for the rest.
3. **Keep family as a fact** on each kind (`mp4`: family video). That's a second grouping beside types, which is a concept named twice, so I'd rule it out.

My lean is (1), plus (2) only when a family needs behaviour of its own (a video type would earn its place when something plays or inspects it).

## The special cases to check

- `.goal` → family `plang`, MIME `text/plain`: it's source text. It becomes text's `goal` kind with extension `.goal` (it reads as text; `goal.Parse` is a separate step).
- `.pr` → the goal type's `pr` kind, MIME `application/plang-goal`.
- `application/plang` (a whole Data, no extension) → the `wire` type's kind.
- `text/plain` → text itself, with no kind. `application/octet-stream` → binary itself.
- `CanonicaliseKind` (jpeg→jpg, markdown→md) → the kind's own aliases (`jpg` with alias `jpeg`, `md` with alias `markdown`). Its shortest-extension guessing goes.

## Comparison with the coder's trace

(to fill in after reading it)
