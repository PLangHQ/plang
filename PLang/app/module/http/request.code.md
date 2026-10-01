# http.request: the body and the content type

How a request's body and its `Content-Type` go out, as the code does it today. Every hop has its file:line. The response side (`ParseResponseAsync`) is not covered here.

## The parameters

`PLang/app/module/http/request.cs`:

| line | parameter | slot | what the handler gets |
|---|---|---|---|
| 28 | `Body` | plain `data.@this?` | the step's property as a Data, **unread**: the generator copies the `.pr` row (`__Copy`, `PLang.Generators/Emission/Action/this.cs:438-440`; the plain-Data branch `PLang.Generators/Emission/Property/Data/this.cs:133-143`). A plain slot has no typed door, so nothing renders or converts it on the way in. |
| 31 | `Header` | `data<dict>?` | read through the typed door (`__View<dict>`, `Emission/Action/this.cs:442-445`), so a template dict renders its `%variables%` when its value is read. |
| 35-36 | `ContentType` | `data<text>`, `[Default("application/json")]` | the content type; a `Content-Type` header replaces it (see the flow, `Default.cs:62`). |
| 39-40 | `Encoding` | `data<text>`, `[Default("utf-8")]` | the charset put on the content type, unless the content type names one. |
| 62 | `DefaultHeaders` | `data<dict>?` | merged under `Header`. |

The `.pr` value of `Body` decides what item the Data holds (`PLang/app/type/this.cs:423-447`, the one door for a row's value). A slice is made in one place, `Make` (`type/this.cs:413-415`): a `template` when the row's type is marked one, else a plain `wire`.

| the step writes | `.pr` row | item held |
|---|---|---|
| `body %request%` | `{type: {item, template: plang}, value: "%request%"}` | a `source` whose whole text is one variable (`IsVariable`, `type/item/source.cs:65`), from `type/this.cs:439-440` |
| `body {"a": 1}` | `{type: {dict}, value: {...}}` | a `wire`: the row's json slice, unread (`type/this.cs:446` → `Make`) |
| `body {"deep": %x.y%}` | `{type: {dict, template: plang}, value: {...}, variable: [...]}` | a `template` (`type/item/wire/template.cs`): the same slice, with the row's variables (`type/this.cs:446` → `Make`) |
| `body "some text"` | `{type: {text}, value: "some text"}` | a `wire` over the quoted json string (`type/this.cs:441`) |

A variable set from any of these (`variable.set` without `as`, `module/variable/set.cs:308`) holds the same item: the variable stores the Data as it is. `ui.render` of a template file answers a text of the file's kind (a `.json` template: `{text, json}`), an inline template a plain text (`module/ui/code/Fluid.cs:50`, `:129`).

## The flow

```
http.request  →  Default.SendAsync                          PLang/app/module/http/code/Default.cs:39
├─ :46   contentType = ContentType's text (default application/json)
├─ :47   encoding    = Encoding's text (default utf-8)
├─ :52   the url: IAddressed.Target()                         PLang/app/module/http/IAddressed.cs:191-202
├─ :56-59 headers = MergeHeaders(Header, DefaultHeaders)      :322-341
│         a case-insensitive Dictionary<string,string>; a step header wins over a default one;
│         each value is its ToString(). Header's value comes through its typed door, so it is rendered.
├─ :62   one Content-Type: a Content-Type header is taken out of the headers and is the content type,
│         in place of the parameter — it names the format the body is written in, and is sent once
├─ :69   bodyVal = await Body.Value()   (skipped when Body is absent or empty)
│         reads the body's value only to test the form case below. Side effect: a file reference
│         reached through a %var% samples its bytes here (content/this.cs:85-93), which is why a
│         `%file%` body sends the file's content (see "What the channel opens").
├─ :73-74 mediaType = MediaTypeHeaderValue.Parse(contentType); its charset, else the encoding
├─ :75-82 the media type is application/x-www-form-urlencoded and the value lowers to a dictionary
│         → FormUrlEncodedContent from each entry's ToString()
├─ :87-88 otherwise: a stream channel over a new MemoryStream, Output, owns the stream, Mime = contentType,
│         not framed (stream/this.cs:44)
├─ :89   body.Write(action.Body)  — the Body Data as the step gave it, not bodyVal
│   └─ stream channel Write                                   PLang/app/channel/type/stream/this.cs:49
│      ├─ :59  context = the channel's Context (null: this channel is in no list) ?? data.Context
│      ├─ :62  data.Peek().Open(context) — opens the item the Body Data holds (type/item/this.cs:488-489,
│      │       no-op by default; a file reference samples its bytes, content/this.cs:124-128)
│      ├─ :64  format = app.type.list.Mime(contentType)       PLang/app/type/list/this.cs:110-111
│      │       the kind whose MIME matches (parameters after `;` ignored, type/kind/this.cs:84-90);
│      │       a MIME no format answers is binary's own kind
│      └─ :65  format.Encode(Stream, data, context, encoding)
│              application/json → item's json kind            PLang/app/type/item/kind/json/this.cs:37-55
│                 :44  Utf8JsonWriter over the stream, default options
│                 :45  data.Output(json.Writer(emitsSchema: false), View.Out, context)
│              text/plain → text's Encode                     PLang/app/type/item/text/this.cs:47-58
│                 :52  the value is a text → its characters in the encoding; else the text Writer
│              other kinds → their type's IEncode, bound at registration (type/kind/this.cs:241-247)
├─ :91   ByteArrayContent over the buffer
├─ :92   httpContent.Headers.ContentType = mediaType
├─ :96-97 HttpRequestMessage(method, url) { Content }
├─ :98   ApplyHeaders(request, headers)                       :343-358
│         CR/LF stripped (:348); a content header (IsContentHeader, :360-366: Content-Type, -Length,
│         -Encoding, -Language, -Disposition, -Range) replaces what the content already carries
│         (:352-353, never joined); the rest go on request.Headers (:356)
└─ :107  SendHttpAsync → the HttpClient for this redirect policy (:288-318)
```

The upload action goes through the same `ApplyHeaders` (`:193`): a `Content-Type` header replaces the content's own.

The request is not signed here: request signing left this module in a13fd386a (June). `Unsigned` only decides whether an `application/plang` response is accepted (`:390-401`).

## How a value writes itself into the body

`data.Output` (`PLang/app/data/this.Output.cs:39-145`) writes what the Data holds:
- `:50-67` a Data whose item is a `variable` item resolves it (Out view) and writes the bound Data.
- `:70` otherwise the held item (or its error), and `:116` `held.Output(writer, mode, context)`: the item writes itself.
- The bare json writer has `EmitsSchema` false, so no `{type, value}` envelope (`:75`, `:118`).

What each item held in a body does in `Output`:

| item | Output | into a json writer |
|---|---|---|
| `source`, whole `%var%` (`type/item/source.cs:287-304`) | `:296-300` resolves the variable and writes the bound Data | whatever the bound value writes |
| `source`, no template (`source.cs:260-279`, via `:291-294`) | `:264-268` its declared kind owns the writer (`kind.Owns`) → the raw verbatim; else `:278` a string | `{item, json}` content relays as its bytes |
| `wire` (`type/item/wire/this.cs:63-71`) | `:67` the wire's reader (plang's own format, `wire/kind/plang/this.cs:129`) owns json and plang writers → the `.pr` slice verbatim; any other writer decodes the slice (`:70`) and the value writes itself | the slice as written in the `.pr`, whitespace included |
| `template` (`type/item/wire/template.cs:21-35`) | `:27-31` in the Store view (a `.pr`) its slice as authored, as a wire; in any other view `:34` the slice is decoded and its parts write themselves, each rendering its variable | the rendered value: `{"deep":"m-1"}` |
| native `dict` (`type/item/dict/this.cs:163-174`) | an object; each entry writes itself | entries render (a template entry resolves at `text/this.cs:243-249`) |
| `text` (`type/item/text/this.cs:234-250`) | `:240` plain content and `:249` a rendered template hand the writer the characters with their kind (`IWriter.Content`, `type/format/IWriter.cs:71-75`); `:243-248` a whole `%var%` writes the bound value | a text of kind json as it is (`{"deep": "m-1"}`); any other text a json string |
| file / url reference (`type/item/content/this.cs:113-136`) | sampled → its content (`:117` json into json verbatim, `:118` text as a string, `:119` bytes); unsampled → `:115` its location | the content if sampled, else the path as a string |

## A text of a format's kind

A text's kind is found by asking the types with the text's own type (`type.list.Kind(type, context)` → `Kind(name, kind)`, `type/list/this.cs:37-52`; text's `Format`, `text/this.cs:254-255`; the text holds its type, made once, `:92-95`): one of text's own kinds (md, xml, …), or one text coins for a text format another type holds (json is item's, csv table's, html code's) — `type/item/text/kind/this.cs` coins it, `type/item/text/kind/format/this.cs` is it. The coined kind:
- owns its format's writer only where that writer's envelope doesn't carry the value's type (`format/this.cs`, `Owns`): the plain json writer takes the characters as they are; plang's envelope, which says `{text, json}`, takes the string, and it reads back as that text;
- declines `Parse`/`Load`, so a read keeps it text;
- answers `Open` (the json parse), which only navigation asks: `%x.a%` and `foreach %x%` open the characters at the first navigation and keep what they opened (`text/this.cs:274-285`, `:287-317`). The text stays text and writes its characters. Characters that don't read as json answer `MaterializeFailed` (json's `Open`). A text whose kind opens nothing answers `CantNavigateText`.

## What the channel opens

`stream/this.cs:62` opens `data.Peek()`: the item the Body Data itself holds. For a body written `%var%`, that item is the reference (a `source`), whose `Open` is the default no-op. The bound value is reached later, through `source.Output` (`source.cs:296-300`), and nothing opens it there. A `%file%` body still sends its content only because `Default.cs:69` read the body's value first and the file reference kept the sampled bytes. A value nested inside the body (a file inside a dict) is never opened: it writes its location (`content/this.cs:115`).
