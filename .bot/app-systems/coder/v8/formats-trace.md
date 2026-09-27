# Formats trace (decision 113, step 1) — facts only, no shape

Read-only source trace; nothing executed. Headline claims spot-verified against source. `P/` = `PLang/app/`.
Carries decision 114 (Ingi): families with no type are **binary's kinds** for now (`.mp4` = `{binary, mp4}`,
"not compressible" a fact on the kind); the family names disappear as categories. The 7 families that are types
today own their formats: code, image, text, archive, goal, binary, and table (for the spreadsheets).

## 0. How a format resolves today (the rules the tables use)
| Step | Where | Fact |
|---|---|---|
| ext → family | `P/format/list/this.cs:394` `Kind(ext)` | `_extensionToKind` |
| ext → MIME | `:412` `Mime(ext)` | miss → `application/octet-stream` |
| MIME → kind | `:426` `Subtype` | octet-stream/empty → null; `_tabularMimeToKind` first; else `CanonicaliseKind(subtype)`; no `; params` strip |
| canonicalise | `:487-513` | every ext whose MIME subtype matches; shortest, then ordinal |
| compressible | `:439-444` | `!_notCompressible.Contains(family)`; set = `{image, video, audio, archive}` (`:336-339`) |
| compressible(type) | `:452` | `Kind(type.kind) ?? FamilyOf(type.Name)`; only caller `P/data/this.Transport.cs:54` |
| `type.list.Mime` / `Extension` | `P/type/list/this.cs:86`, `:94` | `{binary, Subtype(mime)}` / `{binary, ext}`; indexer `:105` re-canonicalises the kind |
| kind → type | `P/type/kind/this.cs:68-75` | `Owner ?? Reader.TypeOf(Name) ?? Format.Kind(Name) ?? "binary"` |
| materialize | `P/type/item/source.cs:150-156`, `:199-206` | kind `Load`/`Parse` first; declines → `Reader.Reader(type, kind)` |
| reader narrowing | `P/type/reader/this.cs:109-122` | binary narrows to `Kind(k).type(ctx)` only if a typed reader exists; else binary's `*` reader (`binary/serializer/Reader.cs:10-20`) answers `binary{Kind=k}` — never throws. `NotSupportedException` only when a declared non-binary type lacks a reader (`{table, xlsx}`). |
| a type's kind list | `P/type/kind/empty/this.cs:48-53` | own kinds + `Format.KindsByFamily()[owner]` — so text lists txt/json/xml/csv/md/yaml/yml/ini, image its 10 exts, etc. |

Types that answer to a family name today: `text` (`type/item/text/this.cs:30`), `image` (`image/this.cs:18`),
`archive` (`archive/this.cs:17`), `code` (`type/code/this.cs:13`), `binary` (`binary/this.cs:29`), `goal`
(`goal/this.Item.cs:7`). `table` exists but is no family (spreadsheet ≠ table in `_extensionToKind`).
**No format extension exists as a kind class** except `json` (item's); every other is minted bare (`type/list/this.cs:28`).

## 1. Every row of the format table
Ext-door = `type.list.Extension`/`path.Kind`; Mime-door = `Subtype(Format.Mime(ext))` (the file/url content read).
Compr. = `Compressible(family)`.

| ext | family | MIME | Compr. | ext-door kind | mime-door kind | reads as today | owning type today |
|---|---|---|---|---|---|---|---|
| .goal | plang | text/plain | yes | goal | **ini** | text{ini} | none (`plang` no type) |
| .pr | goal | application/plang-goal | yes | pr | pr | goal | goal |
| .mp4 | video | video/mp4 | no | **m4a** | **m4a** | binary{m4a} | none |
| .webm .mkv .mov .avi .flv | video | video/webm, x-matroska, quicktime, x-msvideo, x-flv | no | = ext | = ext | binary{ext} | none |
| .mp3 .wav .flac .aac .ogg .m4a | audio | audio/mpeg, wav, flac, aac, ogg, mp4 | no | = ext | = ext (mp3) | binary{ext} | none |
| .txt | text | text/plain | yes | txt | **ini** | text{ini} | text |
| .json | text | application/json | yes | json | json | item json kind → clr(JsonElement) / scalar | item (json kind) |
| .xml | text | application/xml | yes | xml | xml | text{xml} | text |
| .csv | text | text/csv | yes | csv | csv (tabular) | **table**{csv} | table (reader), family says text |
| .md | text | text/markdown | yes | md | md | text{md} | text |
| .yaml / .yml | text | text/yaml | yes | **yml** | yml | text{yml} | text |
| .ini | text | text/plain | yes | ini | ini | text{ini} | text |
| .llm .template .liquid | — (MIME only) | text/plain | n/a | = ext | ini | text{ini} | none (no family) |
| .jpg .jpeg | image | image/jpeg | no | jpg | jpg | image | image |
| .png .gif .bmp .svg .webp .heic | image | image/png, gif, bmp, svg+xml, webp, heic | no | = ext | = ext | image | image |
| .tif .tiff | image | image/tiff | no | tif | tif | image | image |
| .zip .rar .7z .tar .gz .bz2 | archive | application/zip, vnd.rar, x-7z-compressed, x-tar, gzip, x-bzip2 | no | = ext | = ext | binary{ext} (**archive has no reader**) | archive (gzip only, built by `data/this.Transport.cs:62-74`) |
| .xls .xlsx .ods | spreadsheet | vnd.ms-excel, …sheet, …spreadsheet | yes | = ext | = ext (tabular) | binary{ext} (table has only csv reader) | none (table documents them, `type/table/this.cs:3-10`) |
| .numbers .gsheet | spreadsheet | — | yes | = ext | null | binary | none |
| .doc .docx .odt .pdf | document | msword, …document, …text, pdf | yes | = ext | = ext | binary{ext} | none |
| .pages .gdoc | document | — | yes | = ext | null | binary | none |
| .ppt .pptx .odp | presentation | vnd.ms-powerpoint, …presentation ×2 | yes | = ext | = ext | binary{ext} | none |
| .gslides | presentation | — | yes | gslides | null | binary | none |
| .cs .js .ts .py .java .cpp .h .css .go .rb .sh | code | text/x-csharp, javascript, typescript, x-python, x-java, x-c++src, x-chdr, css, x-go, x-ruby, x-shellscript | yes | = ext | = ext | code | code |
| .html .htm | code | text/html | yes | **htm** | htm | code{htm} | code |
| .bat .ps1 | code | — | yes | = ext | null | binary | code (family only) |
| .ai .eps | vector | — | yes | = ext | null | binary | none |
| .obj .fbx .stl .gltf .glb | 3d-model | — | yes | = ext | null | binary | none |
| .db .sqlite .mdb .sql .parquet .orc .avro .h5 .feather .arrow | database | — | yes | = ext | null | binary | none |
| .srt .vtt .sub | subtitle | — | yes | = ext | null | binary | none |
| .epub | ebook | application/epub+zip | yes | epub | epub | binary{epub} | none |
| .mobi .azw3 | ebook | — | yes | = ext | null | binary | none |
| .ttf .otf .woff .woff2 | font | font/ttf, otf, woff, woff2 | yes | = ext | = ext | binary{ext} | none |
| .msi .deb .rpm .pkg .dmg .nupkg | package | — | yes | = ext | null | binary | none |
| .iso .img .vhd .vmdk .qcow2 .ova | disk-image | — | yes | = ext | null | binary | none |
| .apk .aab .ipa .xapk | mobile-app | — | yes | = ext | null | binary | none |
| .crt .cer .pem .der .p12 .pfx .key | certificate | — | yes | = ext | null | binary | none |
| .conf .cfg .toml .properties .env | config | — | yes | = ext | null | binary | none |
| .log | log | — | yes | log | null | binary | none |
| .pt .pth .pb .onnx .joblib | machine-learning | — | yes | = ext | null | binary | none |
| .eml .msg | email | — | yes | = ext | null | binary | none |
| .ics | calendar | text/calendar | yes | ics | ics | binary{ics} | none |
| .geojson .kml | gis-data | application/geo+json, vnd.google-earth.kml+xml | yes | = ext | = ext | binary{ext} | none |
| .shp .gpx | gis-data | — | yes | = ext | null | binary | none |
| .sha256 .md5 .sfv | checksum | — | yes | = ext | null | binary | none |
| .exe .dll | executable | application/octet-stream | yes | = ext | null | binary | none |
| .bin | binary | application/octet-stream | yes | bin | null | binary | binary |

`_tabularMimeToKind` (`:345-351`): text/csv→csv (→ table, the only one that narrows), vnd.ms-excel→xls, …sheet→xlsx,
…spreadsheet→ods (all stay binary). Comment at `:341-344` says "stamped as the `table` type" — true only for csv
(tests agree with code: `FormatRemapTests.cs:48-53`, `TableTypeTests.cs:13-14,41`).
One-map-only: MIME only `.llm .template .liquid`; kind only `.numbers .gsheet .pages .gdoc .gslides .bat .ps1`, all
vector/3d/database/subtitle/package/disk-image/mobile-app/certificate/config/log/ml/email/checksum, `.mobi .azw3 .shp .gpx`.
Serializer `.plang` (`channel/serializer/plang/this.cs:45`) is in no Format map.

## 2. Families with no type (→ binary's kinds per decision 114) and what reads them today
All take the same path, e.g. `{binary, pdf}`: `Kind("pdf")` minted bare, `Parse` null → `Reader.Reader("binary","pdf")`
→ `Kind("pdf").type` = `Format.Kind` "document" → no such type (bare `Full`, `type/list/this.cs:130`) → no typed reader
→ **binary's `*` reader** answers `binary{Kind="pdf"}`. Never throws. A family with no MIME row reaches content as
`{binary}` with no kind (octet-stream → `Subtype` null).

video, audio, spreadsheet (no reader beyond csv), document, presentation, vector, 3d-model, database, subtitle, ebook,
font, package, disk-image, mobile-app, certificate, config, log, machine-learning, email, calendar, gis-data, checksum,
executable, plang (`.goal`: content text{ini}; ext door `{binary, goal}` → binary reader). Not-compressible today:
video, audio (plus image, archive, which are types).

Types that exist but read nothing of their family: **archive** (no `serializer/`; zip/rar/… read as binary),
**table** (only csv reader; xls/xlsx/ods read as binary; `{table, xlsx}` would throw `NotSupportedException`).

## 3. `application/plang` / wire / `text/plain` / json
- **No `wire` type.** `P/type/item/wire/this.cs:14` is an internal `source` subclass (no `[PlangType]`, name from namespace)
  holding its capturing `ITransport` (`:21-23`, `_reader.Owns(w)` `:41,:54`), minted by `P/type/this.cs:331`.
  `P/data/Wire.cs:15` is the Data-shape reader class, not an item/type.
- `application/plang` via `type.list.Mime` → `{binary, plang}` (no ext has subtype `plang`) → binary reader. Never reached on
  the real read: `P/channel/this.cs:232-237` checks `GetByType(mime) == Transport` first and deserializes a whole Data.
  Transport MIMEs: `application/plang` (`plang/this.cs:44`), `application/plang+json` alias (`serializer/list/this.cs:34`).
- `.pr` = `application/plang-goal` is **not** the transport: `{binary, pr}` → `Format.Kind("pr")` = goal → goal reader.
  `file.read`'s `StartsWith("application/plang")` (`P/module/action/file/read.cs:59`) catches it anyway.
- `text/plain` → `CanonicaliseKind("plain")` over txt/ini/llm/goal/template/liquid → **`ini`** → `{binary, ini}` →
  `Format.Kind("ini")` = text → text reader, `Kind = "ini"` (`text/serializer/Reader.cs:17-22`).
- **json today is a kind of `item`** (`P/type/item/kind/json/this.cs:13-19`, `Owner => "item"`, `ClrForm = JsonElement`),
  siblings dict/list/`*` (`Registry.cs:218-239`). `{binary, json}` materializes via `json.Parse` (`:85-99`) to
  `clr(JsonElement)` (object/array) or a native scalar — **not** a dict/list. It becomes dict/list only when a dict/list
  slot receives it (`dict/this.cs:91-92`, `list/this.cs:96`, via `type/this.cs:285-307`) or on a child write (json kind `Set`,
  `:67-77`). dict/list kinds' `Convert` (`kind/dict/this.cs:50-66`, `kind/list/this.cs:85-100`) only via `Data.Convert(kind)`
  (`data/this.cs:84`), no callers in `P/`.

## 4. Types that already declare formats/kinds
| Type | Declares | file:line |
|---|---|---|
| image | magic-byte sniff png/jpg/gif/webp/bmp (not svg/tif/heic); strict kind vs ImageSharp `FileExtensions`; mime via `Format.Mime("."+kind)` | `image/this.Parse.cs:24-39`, `image/this.cs:223-280`, `image/serializer/Reader.cs:23`, `Default.cs:23-31` |
| table | csv only (typed + static reader); doc says csv/xlsx | `table/serializer/Reader.cs:12`, `csv.cs:35-60`, `table/this.cs:3-10,40-52` |
| text | open kind from the extension, no kind classes | `text/this.cs:9-12,37-46` |
| code | kind = ext token; `DetectLanguage` uses other words (csharp/python/…) | `code/serializer/Reader.cs:20`, `code/this.Parse.cs:17-37` |
| archive | `Algo` (gzip only) | `archive/this.cs:28-37`, `data/this.Transport.cs:131` |
| binary | open `Kind` property | `binary/this.cs:32-43` |
| item | kind classes json, dict, list, `*` | `type/item/kind/*/this.cs` |
| number, setting, path, choice | own kind classes (precision, setting class, scheme, set) | `number/kind/**`, `setting/kind/this.cs`, `Registry.cs:241-287` |
| goal | via the `pr` kind (family goal) | `format/list/this.cs:19-22`, `goal/serializer/Reader.cs:16-33` |

## 5. Consumers of `app.Format`
Declared `P/this.cs:214`. Production (9 sites): `data/this.Transport.cs:54` Compressible(type); `type/kind/empty/this.cs:50`
KindsByFamily; `type/kind/this.cs:72` Kind; `type/list/this.cs:87` Subtype, `:106` CanonicaliseKind;
`path/file/this.Operations.cs:67` Mime; `path/this.cs:207` Mime (`path.MimeType`, used by `image/this.cs:165`,
`path/this.Operations.cs:114`, `file/this.cs:92`, `file/read.cs:58,95`, `channel/type/file/this.cs:31`);
`image/serializer/Default.cs:27`, `Reader.cs:23` Mime. No production callers: `FamilyOf`, `Add`, `Remove`, indexer.
Tests (11 files): EngineTypesTests (own instance, 75 calls), KindCanonicalisationTests 7, FamilyOfRenameTests 3,
DataTests 3, FileReadBuildTests 3, OtherAccessorsTests 2, GoalMimeDeserializationTests, TypeEntityShapeTests,
FileHandlerTests, ClrKindNavigationTests 1 each.

## 6. Consumers of the serializer list / `ISerializer` / `ITransport`
Aliases: `P/GlobalUsings.cs:15` `Serializers`; tests `PLang.Tests/Directory.Build.props:63`. Owner `channel/list/this.cs:47`;
built `actor/this.cs:104`, `service/this.cs:38`; `!serializers` `actor/context/this.cs:162`.
Production: `this.cs:457-459` (GetOrDefault plang → cast → `.Text`); `data/this.Transport.cs:57` (GetByType plang ??
new plang); `type/this.cs:331,347` (ITransport param, `Serializers?.Transport`); `type/item/wire/this.cs:21-54` (ITransport);
`base64/this.cs:122` (Json); `path/file/this.Operations.cs:217-218` (GetByExtension ?? Text); `http/code/Default.cs:87-88`
(GetOrDefault(contentType)), `:503,:544,:822` (Transport.Deserialize), `:922-972` (Text, GetByType json, `Body(ISerializer)`);
`build/code/Default.cs:216-217`, `ui/code/Fluid.cs:110-111` (GetOrDefault plang → cast); `llm/code/OpenAi.cs:112` (Text),
`:578` (GetByType json); `channel/this.cs:228-237`; `channel/type/stream/this.cs:64`.
Tests: `Serializers` 43 files (GetByMimeType 27, GetOrDefault 13, …); `ISerializer`/`ITransport` 7 files.

## 7. `channel/serializer/*` by namespace
Contents: `this.cs` (ISerializer, ITransport), `IWriter.cs`, `IReader.cs` (+TokenKind), `IOutput.cs`, `Json.cs`, `Text.cs`,
`UnregisteredMimeType.cs`, `list/this.cs`, `plang/this.cs` (ITransport; Text/SerializeItem(s)Async/Read/Owns),
`json/{writer,reader,converter}.cs`, `text/writer.cs`, `formal/writer.cs`, `value/reader.cs`, `filter/{Sensitive,Tagged,View}.cs`.

**IWriter (114 production files mention it; 101 by fully-qualified `global::app.channel.serializer.IWriter`, 2 per-file alias
— `permission/this.cs:2`, `signature/this.cs:3` — 2 unqualified `app.channel.serializer.IWriter`, 4 bare inside the namespace;
no global alias).** Outside the folder (105):
- comment/string only (5): `PLang.Generators/Diagnostics/Plng003.cs`, `Plng004.cs`, `type/reader/{ReadContext,this,ITypeReader}.cs`.
- **(a) namespace-only — IWriter as a parameter type, calls only interface members (76 files):** error/, goal/step/*, goal/tag,
  module/action/code/*, crypto hash, identity, module/this, snapshot, test, type/code, type/item/{archive, binary, bool, computed,
  date, datetime, directory, duration, file, guid, null, path, permission, setting, signature, tag, time, url}, image/serializer/*,
  kind/{dict,list}, number (+16 kind files), variable/code/*, type/kind, ITypeRenderer, renderer, property/list, this.Generic.
- **(a′) interface-only but branch on the writer's token (5):** `data/this.Output.cs:72,114` (`EmitsSchema`); `image/this.cs:66`
  (`switch writer.Format`); `clr/this.cs:184-197`, `dict/this.cs:166-173` (`_formats` keyed by `writer.Format`); `clr/format/text.cs`.
- **(b) real use (19):** `channel/this.cs:228-237`; `this.cs:457-459`; `goal/step/action/list/this.cs:176`, `action/this.Item.cs:50,92,108`,
  `type/property/this.cs:107,145` (`is formal.Writer`); `list/this.cs:219-226` + `list/format/formal.cs:16` (formal token/cast);
  `list/format/text.cs:18`, `dict/format/text.cs:18` (`new json.Writer`); `text/this.cs:137` (`new text.Writer`);
  `source.cs:203,252` (`new value.Reader`, formal token); `variable/this.cs:233`; `variable/serializer/Entry.cs` (`ref json.Reader`);
  `kind/json/this.cs:108,121` (`new json.Reader`); `kind/reflection/this.cs`, `type/item/this.cs:646` (`filter.Tagged`);
  `base64/this.cs:122`; `wire/this.cs`; `type/this.cs:331-352`.

Other namespaces outside the folder (production): `plang.@this` new — `this.SnapshotWire.cs:38`, `snapshot/this.Wire.cs:27`,
`data/this.Transport.cs:58,140`, `test/report/this.cs:77`; field `store/sqlite/this.cs:19`; casts above.
`json.Writer` new — `data/this.Diff.cs:42`, `action/serializer/Formal.cs:434`, `crypto/code/Default.cs:62`, list/dict `format/text.cs`.
`json.Reader` new/ref — `data/Wire.cs:116`, `data/reader/this.cs:31,36,99`, `goal/serializer/Reader.cs:31,41`,
`action/serializer/Formal.cs:363`, `action/serializer/Reader.cs:120,159`, `list/serializer/Reader.cs:53`, `kind/json/this.cs`,
`data/schema/{ISchemaReader,signature}.cs`, `type/this.cs:340`. `json.Converter` — `Diagnostics/Format.cs:31`.
`formal.Writer` new — `goal/step/this.Validate.cs:36,72`. `filter.Sensitive` — `Diagnostics/Format.cs:34`, `debug/this.cs:541`.
`IReader`/`TokenKind` — ~30 `*/serializer/Reader.cs` generic constraints + `type/item/serializer/json.cs`, `data/reader/this.cs:63`,
`bool/serializer/Reader.cs:3`, `ITypeReader.cs:47`. `IOutput` — clr/list/dict `format/*` + their `_formats` maps.
Unused `using app.channel.serializer;`: `data/this.cs:6`, `data/this.Transport.cs:6`, `http/code/Default.cs:5`, `stream/this.cs:1`.
Tests: IWriter 16 files; `plang.@this` ~40; `json.Writer` 12; `Json` class 6; `Text` class 4; `json.Converter` 5; `json.Reader` 4.

## 8. Where channels pick a serializer / writer today
- Base read `P/channel/this.cs:232-245`: Transport if `GetByType(Mime) == Transport`, else `type.list.Mime(Mime).Create(raw)` — no serializer.
- Base `Mime` default `text/plain` (`:57`); set by http channel (`channel/type/http/this.cs:33`), file channel (`path.MimeType`, `file/this.cs:31`), `channel.set` (`module/action/channel/set.cs:54`).
- **Stream** (also the memory channel, `stream/this.cs:39`): `:54-55` text/* + text value → raw `encoding.GetBytes(text+NewLine)`, no serializer; else `:64` `Serializers.GetOrDefault(Mime).SerializeAsync(...)`; `:68,82-84` line framing for `text/*` or `application/json`.
- Goal channel (`goal/this.cs:43-51,74`), test channel (`test/this.cs:32-37`, `ToString`), file (read-only), http (write unsupported), noop, session, message: no serializer.
- Inside serializers: Json → `new json.Writer` (`Json.cs:32`), Text → `new text.Writer` (`Text.cs:39`), plang → `json.Writer` (`plang/this.cs:90,118,139,159`); values then branch on `writer.Format` / `EmitsSchema` / `is formal.Writer` (§7 a′/b).
- Non-channel picks: http request body `GetOrDefault(contentType)` (`http/code/Default.cs:87`, form-urlencoded special `:71`), `Text` `:942`, json `:972`; OpenAi `Text` `:112`, json `:578`; path save `GetByExtension ?? Text` (binary → raw bytes) `path/file/this.Operations.cs:208-218`; base64 `Json` `:122`; plang casts in `this.cs:457`, build `:216`, Fluid `:110`; transport compress `data/this.Transport.cs:54-58`.

## 9. Python twins / goldens
Nothing mirrors MIME/extension/family/serializer data. `os/system/builder/llm/types.json` (namespace → `{word, alias}`,
written by `TypeCatalogTwinTests.TypesJson_IsTheTypes`, read by `tools/decider/harness.py:30` `type_word`/`type_namespace`)
lists type namespaces — including `app.channel.type.file` and the serializer list's `serializers` word — so moving/removing
a type rewrites it. `build_pr.py:29-47` `closed_set()` regex-scans `[PlangType]` classes' indexer keys; the serializer list
(`[PlangType("serializers")]`) has MIME keys, but no `choice<serializers>` slot exists, so unused. `settings.json`'s
`format: choice<format>` is the test-report format (`P/test/Format.cs:4`), unrelated. MIME text elsewhere is prose only
(`os/system/modules/variable/compress.description.md:1`, `channel/set.notes.md:6`, copied into `pick_golden.json`).
