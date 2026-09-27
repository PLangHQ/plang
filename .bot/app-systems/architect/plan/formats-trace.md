# Formats: the three places that answer "what is this MIME" (the architect's own trace, facts only)

Written before reading the coder's trace. Ingi, 2026-09-27: `serializers` plural and `serializers.GetByType(mime)` are OBP violations; "follow obp pattern serializer.mime.list.where(..), every format should have a reader, many just fall to binary". The comparison is at the end.

## 1. `app.Format`, `format/list/this.cs` (the extension table)

- **Stores:** `_extensionToKind` (".jpg"→"image", ".pr"→"goal", ".goal"→"plang", … extension → family), `_extensionToMime` (".jpg"→"image/jpeg", ".pr"→"application/plang-goal", ".goal"→"text/plain"), `_notCompressible` (image, video, audio, archive), `_tabularMimeToKind` (text/csv→csv, the spreadsheet MIMEs → xls/xlsx/ods), and the derived `_allKinds` and `_mimeToKind`. Six maps, and no object for a format.
- **Doors and callers** (production, counted as `Format.X(`): `Mime(ext)` 3 (octet-stream on a miss), `Kind(ext)` 1, `this[ext]` 0 (throws), `Subtype(mime)` 1 (MIME → kind, the tabular special case, then `CanonicaliseKind`), `Compressible(kind)` and `Compressible(type)` 1, `FamilyOf` 0, `CanonicaliseKind` 1 (jpeg→jpg, the shortest extension of a shared MIME), `KindsByFamily()` 1 (family → extensions, for `kind.list` and teaching), `Add` and `Remove` 0.

## 2. `type.list.Mime` / `Extension`, `type/list/this.cs:86-96` (bytes as a type)

- `Mime(mime, ctx)` = `{binary, Format.Subtype(mime)}`, and `Extension(ext, ctx)` = `{binary, ext}`. **Content off I/O is binary with the format as its kind**, and it narrows on access through the kind's reader (json → dict, jpg → image, csv → table). So "every format falls to binary" is already the model for reading values. The kind is the reader key.
- Callers: `path/file/this.Operations.cs:68` (ReadText), `path/this.cs:256`, `url/this.cs:84`, `http/HttpBuildHelpers.cs:38`, and `channel/this.cs:243` (the base `Read(byte[])`).
- The kind → type answer for a name the format table doesn't know comes from `type.list.Reader.TypeOf(kind)` (`type/reader/this.cs:133`) and `Format.Kind`: two more places saying what a kind's content is (`type/kind/this.cs` `type(context)`).

## 3. The serializers, `channel/serializer/list/this.cs` (writing to the wire, and reading plang's container)

- `[PlangType("serializers")]`, plural. Stores: `_byType` (MIME → serializer), `_byExtension`, `_default` (json). **Only 3 serializers:** json, text and plang, plus aliases (`text/json`, `application/json; charset=utf-8` → json; `application/plang+json` → plang; `text/html` → json).
- **Nine selection doors**, most with verb+noun names:
  - `GetByMimeType` 0 callers (throws);
  - `this[mime]` 0 (throws);
  - `GetByType` 2 (null);
  - `GetByExtension` 0;
  - `GetOrDefault` 4 (json on a miss);
  - `Default` 0;
  - `Json` 1, `Text` 1 and `Transport` 3, the hand-named doors.
  - `Register` 0 outside the constructor.
- Per actor: each actor's channel list owns one (`actor.Channel.Serializers`).

## Where the MIME/format knowledge also lives

- **String checks of plang's MIME in callers (raw hand-off):** `http/code/Default.cs:428,641` `StartsWith("application/plang")`, `file/read.cs:59`, and `GetOrDefault("application/plang")` at `app/this.cs:455`, `build/code/Default.cs:217`, `ui/code/Fluid.cs:111`, plus `data/this.Transport.cs:57` `GetByType("application/plang")`.
- **35 literal MIME strings** in `app/` outside the format table and the serializers.
- **The renderer registry** (`type/renderer`) keys writers by type name and format. It's a fourth partial answer: how a type is written in a format.

## What the facts say

- **A "format" exists in five places, as rows in maps:** the extension table, the MIME→kind derivation, the type list's binary+kind, the serializers (wire read/write for 3 of them), and the renderers (type × format writers). No object holds `.json` + `application/json` + the dict family + its reader + its writer.
- **Reading values is already "binary + the format as kind"**, which matches Ingi's "many just fall to binary". The only reader that isn't that is plang's container (a whole Data), today reached through `Transport`.
- **Writing is where the serializers matter:** json, text and plang write Data to a channel. Everything else is written by renderers per type.

## Comparison with the coder's trace (`.bot/app-systems/coder/v8/mime-registries-trace.md`, b972f8fc6; read after mine was pushed, 0b03f55f2)

**The coder's is much better on the details. It found real bugs I didn't:**
- **Normalisation:** only the serializer list strips `; params`, so its own key `application/json; charset=utf-8` is unreachable. `Format.Subtype` doesn't strip at all, so that MIME becomes kind `json; charset=utf-8` and stays binary.
- **`CanonicaliseKind` (the shortest extension, then alphabetical) gives wrong kinds:** `text/plain` → `ini`, `text/html` → `htm`, and `video/mp4` → `m4a`, which also matches `audio/mp4`, so a video's family comes out as **audio**. A test comment already expects `{binary, null}` where the code gives `{binary, ini}`.
- **`FamilyOf` is order-dependent** (`TryAdd` over a ConcurrentDictionary): `text/plain` answers `text` or `plang`, octet-stream answers `binary` or `executable`. Not deterministic.
- **`text/html` has three different answers** (the serializers: Json; Format: `.html` → code; type.list: `{binary, htm}`).
- **`GetOrDefault` silently answers Json** for everything unregistered (csv, xml, octet-stream, plang-goal).
- **`StartsWith("application/plang")` also catches plang-goal** at `http/code/Default.cs:434,654`, not only file.read.
- **The renderer dispatch is dead in production** (`Of` has no production caller, and `json/writer`'s `_renderers` is never read). The reader and renderer registries hold no MIME keys.

**Mine adds the structural read:** a format lives in five places as rows and never as one object. Reading is already "binary + the format as kind", which is Ingi's "fall to binary". The serializers matter for writing.

**Lesson:** the coder's per-member verification turns a structural smell into concrete wrong answers. Each of these bugs exists because one fact (a format's canonical kind, family, MIME spelling) is derived separately in several places. **One format object holding its own facts fixes them by construction.** That's the strongest argument for Ingi's shape.
