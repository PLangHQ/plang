# MIME / format registries — facts trace (no shape)

Facts only, from read-only source reading; no code executed. Headline claims spot-verified against source
(serializer aliases + `;` strip, `CanonicaliseKind` shortest/ordinal tie, `text/plain`→`ini`, `video/mp4`→`m4a`,
`_mimeToKind` via `TryAdd`, renderer `_renderers` never read). `P/` = `PLang/`, `T/` = `PLang.Tests/`.

## Headlines
- **No shared normalisation.** Only the serializer list strips `; params` (`GetByType`, `serializer/list/this.cs:74-76`,
  only when `;` index > 0). Its alias key `"application/json; charset=utf-8"` (:33) is therefore unreachable — the strip
  runs before lookup. `Format.Subtype` does not strip: `application/json; charset=utf-8` → kind `"json; charset=utf-8"`
  → `{binary, "json; charset=utf-8"}` → stays binary.
- **`CanonicaliseKind` = shortest extension, then ordinal** (`format/list/this.cs:487-513`), over every extension whose
  MIME subtype matches: `text/plain`→`ini` (txt/ini/llm tie; `ini` < `llm` < `txt`), `text/html`→`htm`,
  `video/mp4`→`m4a` (matches `audio/mp4`'s subtype too → family **audio**), `image/jpeg`→`jpg`, `yaml`→`yml`.
  `type.list.Extension` canonicalises through it too (`.html`→`htm`, `.mp4`→`m4a`, `.jpeg`→`jpg`).
- **`text/html` gets three answers:** serializers → **Json** (:36); Format `.html`/`.htm` → `text/html`, kind `code`;
  `type.list.Mime` → `{binary, htm}` → narrows to `code`.
- **Silent Json fallback:** `GetOrDefault` answers Json for any unregistered MIME — `application/plang-goal`,
  `application/octet-stream`, `text/csv`, `application/xml`, …
- **`StartsWith("application/plang")` also catches `application/plang-goal`** (`.pr`): `file/read.cs:59`,
  `http/code/Default.cs:434`, `:654`. The channel boundary instead asks "which serializer owns this MIME"
  (`channel/this.cs:232`), which separates them.
- **`Format.FamilyOf(mime)` is order-dependent:** `_mimeToKind` is filled with `TryAdd` over a ConcurrentDictionary
  (:364, :527), so `text/plain` → `text` or `plang` (`.goal`), `application/octet-stream` → `binary` or `executable`.
- **Renderer dispatch is dead in production:** `renderer.Of` has no production caller; `json/writer.cs` stores
  `_renderers` (:19, :28) and never reads it.
- **Two parallel decode paths:** the channel boundary (`channel/this.cs:226-246`, Transport check then `type.list.Mime`)
  and `path.ReadText` (`path/file/this.Operations.cs:67-68`, `Format.Mime` → `type.list.Mime`, no Transport check).
- Test comment drift: `Stage3_HttpContentTypeDispatchTests.cs:110` says a missing Content-Type gives `{binary, null}`;
  the http channel substitutes `text/plain` → `{binary, ini}` (test asserts only the name `binary`).
- Doc drift: `type/reader/this.cs:103` says `plang-goal→goal`; the actual kind is `pr` (→ `Format.Kind("pr")="goal"`).

## A. The registries

### A1. Serializer list — `P/app/channel/serializer/list/this.cs` (`[PlangType("serializers")]`, one per actor)
Created `actor/this.cs:104`, `service/this.cs:38`; held `channel/list/this.cs:47`; exposed as `!serializers`
(`actor/context/this.cs:162`).
Data: `_byType`, `_byExtension` (OrdinalIgnoreCase), `_default`. Registered: Json (`application/json`, `.json`),
Text (`text/plain`, `.txt`), plang Transport (`application/plang`, `.plang`). Aliases: `text/json`→Json,
`application/json; charset=utf-8`→Json (unreachable), `application/plang+json`→plang, `text/html`→Json. Default Json.

| Member | Line | Miss |
|---|---|---|
| `Register(ISerializer)` | :44 | overwrites type + ext keys |
| `GetByMimeType(string)` / `this[string]` | :56 / :66 | throws `UnregisteredMimeType` |
| `GetByType(string)` | :71 | null; strips `;…` |
| `GetByExtension(string)` | :87 | null; tries `.a.b.c`, `.b.c`, `.c` |
| `GetOrDefault(string?)` | :105 | Json |
| `Default {get;set;}`, `Json`, `Text`, `Transport`, `Types`, `Extensions` | :116–148 | — |

### A2. `app.Format` — `P/app/format/list/this.cs` (`app.this.cs:214`, one per app)
Data: `_extensionToKind` (150+ exts → family), `_extensionToMime` (subset), `_notCompressible`,
static `_tabularMimeToKind` (csv/xls/xlsx/ods), derived `_allKinds`, `_mimeToKind` (first-wins).
Notable: `.goal`→`text/plain` kind `plang`; `.pr`→`application/plang-goal` kind `goal`; `.llm .template .liquid`→
`text/plain` with **no kind**; `.html .htm`→`text/html` kind `code`; `.bin .exe .dll`→octet-stream. No `.plang` entry.

| Member | Line | Miss |
|---|---|---|
| `KindsByFamily()` | :375 | — |
| `Kind(ext)` | :394 | null |
| `this[ext]` | :405 | throws |
| `Mime(ext)` | :412 | `application/octet-stream` |
| `Subtype(mime)` | :426 | null for blank/octet-stream; tabular map, else `CanonicaliseKind(after '/')`; no param strip |
| `Compressible(kind)` / `Compressible(type)` | :439 / :452 | false |
| `FamilyOf(string)` | :465 | null |
| `CanonicaliseKind(string?)` | :487 | lowercased input |
| `Add` / `Remove` | :519 / :536 | — |

### A3. `type.list` — `P/app/type/list/this.cs`
- `Mime(mime, ctx)` :86 = `this[new type("binary", Format.Subtype(mime)), ctx]`
- `Extension(ext, ctx)` :94 = `Null` for empty, else `this[new type("binary", ext.TrimStart('.')), ctx]`
- `this[type, ctx]` :105 re-canonicalises the kind (`Format.CanonicaliseKind(k) ?? k`) → `Full(...)`
- `Kind(name)` :26 — first matching kind, else a new unregistered `kind.@this(name)`
- A kind decides its type (`type/kind/this.cs:68-75`):
  `Owner ?? Reader.TypeOf(Name) ?? Format.Kind(Name) ?? "binary"`.

### A4. Reader registry — `P/app/type/reader/this.cs`
Keys `(type name, kind token)` — **not MIME**. Filled lazily from `*.serializer` namespaces. Non-`*` kinds:
`("table","csv")` (typed + static), `("choice", <set>)` (runtime, `Registry.cs:258`). Members: `Of` :71, `Typed` :89,
`Reader` :109 (binary+kind narrows to `Kind(kind).type(ctx)` when a typed reader exists; throws `NotSupportedException`
when none), `TypeOf` :133 (untyped keys only — in practice only `csv`→`table`), `Register` :149/:158.

### A5. Renderer registry — `P/app/type/renderer/this.cs`
Keys `(type, writer format token)` — `json`/`plang`/`text`/`formal`, **not MIME**. Production: `Register`/`Has` only
(`Registry.cs:184,187`); `Of` has no production caller; the table is passed to `json/writer.cs` and never read.

## B. Production callers
- **Serializer list:** `GetByType` — `data/this.Transport.cs:57`, `http/code/Default.cs:972`, `llm/code/OpenAi.cs:578`,
  `channel/this.cs:232`. `GetByExtension` — `path/file/this.Operations.cs:218` (`?? serializers.Text`).
  `GetOrDefault` — `app/this.cs:458`, `build/code/Default.cs:217`, `ui/code/Fluid.cs:111` (all `application/plang`),
  `http/code/Default.cs:88` (request ContentType, default `application/json`), `channel/type/stream/this.cs:64`.
  `Transport` — `type/this.cs:347`, `http/code/Default.cs:503,544,822`, `channel/this.cs:232,237`.
  `Json` — `base64/this.cs:122`. `Text` — `path/file/this.Operations.cs:218`, `http/code/Default.cs:942`,
  `OpenAi.cs:112`. No production callers: `GetByMimeType`, indexer, `Register`, `Default`, `Types`, `Extensions`.
- **Format:** `Mime` — `path/this.cs:207` (`path.MimeType`), `path/file/this.Operations.cs:67`,
  `image/serializer/Default.cs:27`, `image/serializer/Reader.cs:23`. `Subtype` — `type/list/this.cs:87`.
  `CanonicaliseKind` — `type/list/this.cs:106`. `Kind` — `type/kind/this.cs:72`. `KindsByFamily` —
  `type/kind/empty/this.cs:50`. `Compressible(type)` — `data/this.Transport.cs:54`. No production callers: `FamilyOf`,
  `Add`, `Remove`, indexer. `path.MimeType` callers: `image/this.cs:165`, `path/this.Operations.cs:114`,
  `file/this.cs:92`, `file/read.cs:58,95`, `channel/type/file/this.cs:31`.
- **type.list:** `Mime` — `path/file/this.Operations.cs:68`, `channel/this.cs:243`. `Extension` — `path/this.cs:256`,
  `url/this.cs:84`, `http/HttpBuildHelpers.cs:38`.
- **Reader:** `Reader(...)` — `source.cs:202`, `serializer/plang/this.cs:246`, `data/reader/this.cs:69`,
  `step/action/serializer/Reader.cs:137`, `setting/serializer/Reader.cs:34`. `Typed` — `type/this.cs:344`,
  `type/item/this.cs:191`, `list/serializer/Reader.cs:33`. `TypeOf` — `kind/this.cs:71`.
- **Tests:** serializer list `GetByMimeType` 27 (18 files), `GetByType` 15, `GetByExtension` 13, `GetOrDefault` 17;
  Format `FamilyOf` 30, `Kind` 22, `Mime` 14, `Compressible` 13, `Add` 9, `CanonicaliseKind` 6, `Remove` 4;
  `type.list.Mime` 15, `Extension` 2; renderer `Of` 9.

## C. What each answers (my trace of the code, not executed)

| MIME | Serializer | Format ext | `type.list.Mime` | narrows to |
|---|---|---|---|---|
| `application/json` | Json | `.json` | `{binary,json}` | item (json) |
| `text/json` | Json (alias) | — | `{binary,json}` | item |
| `application/json; charset=utf-8` | Json (strip) | — | `{binary,"json; charset=utf-8"}` | binary |
| `application/plang` | Transport | — | `{binary,plang}` | binary |
| `application/plang+json` | Transport (alias) | — | `{binary,plang+json}` | binary |
| `application/plang-goal` | null → Json default | `.pr` | `{binary,pr}` | goal |
| `text/plain` | Text | `.txt .ini .llm .template .liquid .goal` | `{binary,ini}` | text |
| `text/html` | Json (alias) | `.html .htm` (code) | `{binary,htm}` | code |
| `application/octet-stream` | null → Json | `.bin .exe .dll` + every unknown ext | `{binary}` | binary |
| `text/csv` | null | `.csv` | `{binary,csv}` | table |
| `application/xml` | null | `.xml` | `{binary,xml}` | text |
| `image/jpeg` | null | `.jpg .jpeg` | `{binary,jpg}` | image |
| `video/mp4` | null | `.mp4` | `{binary,m4a}` | binary (family audio, no reader) |
| `application/pdf` | null | `.pdf` | `{binary,pdf}` | binary (document, no reader) |
| xls/xlsx/ods | null | yes | `{binary,xls/xlsx/ods}` | binary (spreadsheet, no reader) |

Extension-only: serializer `.plang` unknown to Format; serializer list has only `.json .txt .plang`, so file save
falls back to Text for `.xml .html .csv` (`path/file/this.Operations.cs:218`). Kind-but-no-MIME: office/cloud docs,
`.bat .ps1`, vector/3d/database/… (Format.Mime → octet-stream). MIME-but-no-kind: `.llm .template .liquid`.
`.goal`: kind `plang`, MIME `text/plain`; `type.list.Extension(".goal")` → `{binary,goal}`.

## D. Case
Serializers + Format dictionaries OrdinalIgnoreCase; `CanonicaliseKind` lowercases; reader/renderer keys are
case-sensitive tuples (only `TypeOf` ignores case). Upstream param stripping: http uses `ContentType?.MediaType`
(`http/code/Default.cs:410,651`, `path/http/this.cs:367` → `url/this.cs:73`); `channel.set` / `request.ContentType`
are not stripped. Default MIMEs `text/plain`: `channel/this.cs:57`, `channel/type/http/this.cs:33`,
`channel/set.cs:54`, `url/this.cs:87`.

## E. MIME string checks in production
- Prefix: `file/this.cs:93-95` (`text/` || contains `json` || contains `xml`); `file/read.cs:59`, `http/code/Default.cs:434`,
  `:654` (`application/plang`); `channel/type/stream/this.cs:54` (`text/` → raw text write), `:83-84`
  (`text/` || `application/json` → line-delimited).
- Equality: `http/code/Default.cs:71` (form-urlencoded), `:719` (`text/event-stream`); `file/read.cs:95`
  (`== "application/octet-stream"`, case-sensitive); `base64/this.cs:57-60` (data-url, `octet-stream` → null kind).
- Literals/fallbacks: `application/octet-stream` at `path/this.cs:207`, `path/this.Operations.cs:115`,
  `image/this.cs:56,151`, `http/code/Default.cs:990,998,1035`; `application/json` at `http/code/Default.cs:974`;
  magic-byte sniff `image/this.Parse.cs:28-37`.

## F. Channel read boundary — `P/app/channel/this.cs:226-246`
Transport branch = MIMEs the plang serializer owns after `;` strip (`application/plang`, `application/plang+json`,
`application/plang; …`). Value branch = `type.list.Mime` (no strip), `{binary}` without actor/app. Result born with
`Actor?.Context`. Reached by: file channel (`channel/type/file/this.cs:46,55`; built at `file/this.cs:97`,
`url/this.cs:85`, `file/read.cs:61`), http channel (`channel/type/http/this.cs:42`; built at
`http/code/Default.cs:460`, `url/this.cs:83,87`), stream channel (`channel/type/stream/this.cs:98`).
Parallel non-channel decode: `path/file/this.Operations.cs:67-68` (`ReadText`).
