# Formats as kinds — build sketch (decisions 113–115), before code

Anchored on `v8/formats-trace.md` and the current source. Five commits in order; each green (6 suites vs baseline,
`plang --test`, builder check) before the next. Open points marked **Q**.

## (a) Kinds carry their formats; one walk; `app.Format` gone

**Declaration: an attribute on the type's class, read once by the registry** (the `[PlangType]` path, `Registry.cs:229-244`
`Enlist` → `Hold`). One attribute per format; a kind is minted per declaration and held on the type's empty kind.
```csharp
[PlangType("image")]
[Format("png",  "image/png")]                         // extension defaults to "." + name
[Format("jpg",  "image/jpeg", ".jpg", ".jpeg")]       // extensions replace CanonicaliseKind's jpeg→jpg
[Format("tif",  "image/tiff", ".tif", ".tiff")]
…                                                      // Compressible = false for image/audio/video/archive formats
[PlangType("text")]
[Format("", "text/plain", ".txt")]                    // "" = the type itself: text/plain is {text}, no kind
[Format("md", "text/markdown")] [Format("ini")] [Format("goal")] [Format("llm")] [Format("template")] [Format("liquid")]
[PlangType("binary")]
[Format("", "application/octet-stream", ".bin")]
[Format("mp4", "video/mp4", Compressible = false)] [Format("m4a", "audio/mp4", Compressible = false)] [Format("pdf", "application/pdf")] …
```
Ownership per decision 115: a format sits on the type that READS it — image (its 10), text (xml, md, yaml/yml, ini, goal, llm,
template, liquid + text/plain), code (cs…sh, html/htm, bat/ps1 with their MIME), table (csv), goal (pr), json (json), and
**binary everything else** (archive formats, xls/xlsx/ods, every no-type family). `archive` declares none (no reader).
`text/calendar`, `application/geo+json`, `font/*`, `application/epub+zip`: binary's, MIME kept.

**The kind gains three facts** (base `type/kind/this.cs`): `Mime` (list), `Extension` (list), `Compressible` (bool).
Set by the attribute; code-declared kinds (number, choice, setting, path, item's dict/list/`*`) declare none.

**One walk, unchanged entry:** `type.list.Kind(key)` (`type/list/this.cs:26`) → each type's `kind[key]` → empty kind's
`this[key]` asks each held kind `k.Names(key)` — the kind answers to its name, `Alias`, any `Mime` (**media type with `; params`
dropped, here, once**, case-insensitive), any `Extension` (with or without the dot). `Names` mirrors `type.Names(key)`
(`type/this.cs:617`). The empty kind answers for its own `""` declaration (text/plain → text's empty kind).
**Uniqueness is checked at `Hold`**: two kinds answering the same MIME or extension throw at registration (a test pins it) —
this is what kills the order-dependence (`FamilyOf`'s `TryAdd`, `CanonicaliseKind`'s shortest/ordinal tie).

**The two doors:**
- `type.list.Mime(mime, ctx)` → `Kind(mime)`; unknown → `{binary}`. `type.list.Extension(ext, ctx)` → the same walk.
- `kind.type(ctx)` (`kind/this.cs:68-75`) → `Owner ?? Reader.TypeOf(Name) ?? "binary"` (the `Format.Kind` rung goes;
  Owner is now set for every declared format).
- `type.list[type, ctx]` (`:105`) drops `Format.CanonicaliseKind` — the walk finds `jpeg`/`markdown`/`text/markdown` directly.
- Empty kind's `list` (`empty/this.cs:48-53`) = its held kinds only (the `KindsByFamily` merge goes).

**Q1 — what the MIME door stamps.** Today content off I/O is `{binary, png}`, narrowing to image on touch. `text/plain`
as "text with no kind" can't be spelled `{binary, ·}`. Options:
- (i) the door answers the kind's own type — `{image, png}`, `{text}`, `{json}`, `{binary, mp4}` — the value is still a
  lazy `source` of that type (`type/this.cs:265`), same parse on touch. Visible change: `%x!type%` before touch says `image`,
  not `binary`; tests pinning `{binary, png}` (FormatRemapTests, Stage3_HttpContentTypeDispatchTests, DataTests ~11) flip.
- (ii) keep `{binary, kind}` and special-case the empty-kind match to `{text}` — two stamps for one door (a fork).
  **My pick: (i).**

**json becomes a type.** Promote `type/item/kind/json` to `type/json` (`[PlangType("json")]`, `[Format("json", "application/json", ".json")]`,
alias MIME `text/json`). Its values stay what they are (`clr(JsonElement)` / scalar via `Parse`, `kind/json/this.cs:85-99`),
navigation/enumerate/set/output/clr/read unchanged, narrowing to dict/list only by slot. No eager narrowing; dict/list
`Convert` untouched. **Q2:** the json kind is also item's clr carrier for `JsonElement` (`item.Kind(clr)`, `list/this.cs:35-39`,
`ClrForm`). Keep a `json` kind on item for that carrier role (same class, now Owner json) or move `JsonElement`'s carrier onto
the json type? I'd keep the class as the json type's kind and have item's `Kind(clr)` walk every type's carriers — say which.

**Compressible** (decision 115.5): `data/this.Transport.cs:54` asks `Type.kind.Compressible`. **Default false** for a kind with
no declaration — today `Compressible(type)` answers false for dict/list/number (no family), and text/goal/json true; the
declarations carry today's answers (true except image/video/audio/archive formats). Pinned.

**`app.Format` deleted** (`format/list/this.cs`, `app/this.cs:214`). Its 9 production callers move: `path.MimeType` →
`Kind(extension).Mime[0] ?? "application/octet-stream"` (on path, `path/this.cs:207`); image serializers' `Format.Mime("."+kind)` →
the kind's Mime; `Transport.cs:54` → kind; `type/list:87,106`, `kind:72`, `empty:50` as above. Tests: EngineTypesTests (75 calls on
its own instance), KindCanonicalisationTests, FamilyOfRenameTests move to the walk or go with the thing they tested.

**Six bugs pinned by test** (each would fail on today's code): `application/json; charset=utf-8` → json;
`text/plain` → `{text}` (not ini); `text/html` → code{html} (and `.htm` answers html); `video/mp4` → `{binary, mp4}`,
`audio/mp4` → `{binary, m4a}`; a duplicate MIME/extension throws at registration (order-dependence); an unknown MIME → `{binary}`
(the silent-Json half lands in (c)).

## (b) The Data-reading door, and the wire type

The decode door can't be `type.Create(bytes)` for plang's container (a whole Data: properties, signature). One word on the type:
```csharp
// type.@this — the one decode: bytes of this type → a Data
public Task<data.@this> Decode(byte[] raw, actor.context.@this context)   // name free; not item's internal Read(ITypeReader…), not type.Read(ref json.Reader…)
    => ClrType is IDecode … ? …                                            // see Q3
     : Task.FromResult(new data.@this(Name, Create(raw, context), context: context));   // born with the CALLER's context
```
**Q3 — how wire overrides it.** `type.@this` is one class for every type, so the override lives on the item class, the same
mechanism as `item.ILoad<T>` (static virtual on the item, called by the type — `goal.Load` precedent, 8b-2):
`item.IDecode<T> { static abstract Task<data.@this> Decode(byte[] raw, context); }`; `type.Decode` calls it when the class
implements it, else the default. The wire type (`type/item/wire`, gets `[PlangType("wire")]` internal,
`[Format("plang", "application/plang", ".plang")]` + `application/plang+json` alias) implements it with today's
`Transport.DeserializeAsync`. Then `channel.Read(byte[])` (`channel/this.cs:226-246`) is one line:
`Channels.App.type.list.Mime(Mime, ctx).Decode(raw, ctx)` — the `GetByType(Mime) == Transport` fork goes; plang is not special.
`path.ReadText`'s parallel decode (`path/file/this.Operations.cs:67-68`) calls the same door.

## (c) The serializer list gone

Each format kind names its writer; a channel/non-channel write asks its kind. **The kind's member:** `Writer(Stream, Encoding)` →
`IWriter` is verb-free but that's a factory name… one word: **`Open(Stream, Encoding) → IWriter`**. Defaults by owner: text/code/
csv → `text.Writer`; json → `json.Writer`; wire → `json.Writer` with schema (today's plang serializer write); binary → raw bytes.
Sites: stream channel (`stream/this.cs:54-64`: the raw-text bypass becomes text's writer — same bytes; line framing stays on
stream, keyed by the kind being text-owned or json instead of `StartsWith("text/")`); http body (`http/code/Default.cs:87,942,972`);
OpenAi `:112,:578`; path save (`path/file/this.Operations.cs:218`); base64 `:122`; the three plang casts (`this.cs:457`,
`build/code/Default.cs:216`, `Fluid.cs:110`) → the wire type's `Text(...)`/write; `data/this.Transport.cs:57-58,140`;
`type/this.cs:347` (`Serializers?.Transport` → the wire type); `type/item/wire` (`ITransport _reader` → itself).
Deleted: `channel/serializer/list`, `ISerializer`, `ITransport`, `Json.cs`, `Text.cs`, `UnregisteredMimeType`, `!serializers`,
the `Serializers` aliases (`GlobalUsings.cs:15`, `PLang.Tests/Directory.Build.props:63`); `types.json` rewrites (twin test).
Tests: 43 files use `Serializers` (27 `GetByMimeType`) — they move to the kind door.

**Q4 — the silent Json fallback's replacement.** Today a channel write under an unregistered MIME serializes as JSON. After:
- `text/html` (today Json by alias) is code's → text writer: an HTML channel writing a **dict** now writes the text writer's
  rendering, not JSON. Accept, or give code a json-structured fallback?
- an unknown MIME → binary: writing a non-binary value there — error (`no writer for <mime>`), or the text writer (today's
  path-save rule `?? Text`)? **My pick: error**, it's the "no silent fallback" rule; path save keeps text for extensions the walk
  doesn't know (its own today's rule), stated at the site.

## (d) Writers, readers, filters → `app.type.format.*` (mechanical, its own commit)
`channel/serializer/{IWriter, IReader, IOutput, json/*, text/*, formal/*, value/*, filter/*}` → `type/format/…`, namespaces
`app.type.format.*`. 76 namespace-only files (qualified-name change), 5 token-branching, 19 real uses (same edits, no logic),
4 unused `using app.channel.serializer;` removed. Per the hook rule every production file is an Edit (no sed); PLang.Tests may
be batched. No behavior change; suites identical.

## (e) Decision 110 + url
Five sites call the (b) door: `type/item/file/this.cs:97`, `url/this.cs:83-87` (the three branches pick a MIME: Content-Type, else
the extension's kind, else `text/plain`, then one `Decode`), `http/code/Default.cs:460`, `file/read.cs:61` (its
`StartsWith("application/plang")` goes — `.pr` is goal's kind through the walk). Result born with the caller's context (bug fixed,
pinned). **url refuses an unsigned `application/plang`** (`UnsignedPlang` 403, the http rule at `Default.cs:436`) — pinned.
`channel/type/file` and `channel/type/http` then have no users — deletion waits on Ingi (decision 114 note).

## Order / risk
(a) is the big one (every format declaration + the walk + json type); (d) is the widest but mechanical. (b) before (c) because
(c)'s wire writes need the wire type. Each commit lists its `.goal`/template consumer sweep and a builder check (the builder reads
`.pr` through the transport → (b)/(c) touch it).
