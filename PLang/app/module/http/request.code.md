# http.request: the body and the content type

How a request's body and its `Content-Type` go out, as the code does it today. Every hop has its file:line. The response side (`ParseResponseAsync`) is not covered here.

## The parameters

`PLang/app/module/http/request.cs`:

| line | parameter | slot | what the handler gets |
|---|---|---|---|
| 25 | `Body` | plain `data.@this?` | the step's property as a Data, **unread**: the generator copies the `.pr` row (`__Copy`, `PLang.Generators/Emission/Action/this.cs:438-440`; the plain-Data branch `PLang.Generators/Emission/Property/Data/this.cs:133-143`). A plain slot has no typed door, so nothing renders or converts it on the way in. |
| 28 | `Header` | `data<dict>?` | read through the typed door (`__View<dict>`, `Emission/Action/this.cs:442-445`), so a template dict renders its `%variables%` when its value is read. |
| 31-32 | `ContentType` | `data<text>`, `[Default("application/json")]` | one media type. |
| 35-36 | `Encoding` | `data<text>`, `[Default("utf-8")]` | the charset put on the content type. |
| 57 | `DefaultHeaders` | `data<dict>?` | merged under `Header`. |

The `.pr` value of `Body` decides what item the Data holds (`PLang/app/type/this.cs:423-447`, the one door for a row's value). A slice is made in one place, `Make` (`type/this.cs:413-415`): a `template` when the row's type is marked one, else a plain `wire`.

| the step writes | `.pr` row | item held |
|---|---|---|
| `body %request%` | `{type: {item, template: plang}, value: "%request%"}` | a `source` whose whole text is one variable (`IsVariable`, `type/item/source.cs:65`), from `type/this.cs:439-440` |
| `body {"a": 1}` | `{type: {dict}, value: {...}}` | a `wire`: the row's json slice, unread (`type/this.cs:446` → `Make`) |
| `body {"deep": %x.y%}` | `{type: {dict, template: plang}, value: {...}, variable: [...]}` | a `template` (`type/item/wire/template.cs`): the same slice, with the row's variables (`type/this.cs:446` → `Make`) |
| `body "some text"` | `{type: {text}, value: "some text"}` | a `wire` over the quoted json string (`type/this.cs:441`) |

A variable set from any of these (`variable.set` without `as`, `module/variable/set.cs:308`) holds the same item: the variable stores the Data as it is.

## The flow

```
http.request  →  Default.SendAsync                          PLang/app/module/http/code/Default.cs:39
├─ :46   contentType = ContentType's text (default application/json)
├─ :47   encoding    = Encoding's text (default utf-8)
├─ :52   the url: IAddressed.Target()                         PLang/app/module/http/IAddressed.cs:191-202
├─ :56-59 headers = MergeHeaders(Header, DefaultHeaders)      :316-335
│         a case-insensitive Dictionary<string,string>; a step header wins over a default one;
│         each value is its ToString(). Header's value comes through its typed door, so it is rendered.
├─ :66   bodyVal = await Body.Value()   (skipped when Body is absent or empty)
│         reads the body's value only to test the form case below. Side effect: a file reference
│         reached through a %var% samples its bytes here (content/this.cs:85-93), which is why a
│         `%file%` body sends the file's content (see "What the channel opens").
├─ :69-76 contentType is application/x-www-form-urlencoded and the value lowers to a dictionary
│         → FormUrlEncodedContent from each entry's ToString()
├─ :81-82 otherwise: a stream channel over a new MemoryStream, Output, owns the stream, Mime = contentType,
│         not framed (stream/this.cs:44)
├─ :83   body.Write(action.Body)  — the Body Data as the step gave it, not bodyVal
│   └─ stream channel Write                                   PLang/app/channel/type/stream/this.cs:49
│      ├─ :59  context = the channel's Context (null: this channel is in no list) ?? data.Context
│      ├─ :62  data.Peek().Open(context) — opens the item the Body Data holds (type/item/this.cs:488-489,
│      │       no-op by default; a file reference samples its bytes, content/this.cs:124-128)
│      ├─ :64  format = app.type.list.Mime(contentType)       PLang/app/type/list/this.cs:103-104
│      │       the kind whose MIME matches (parameters after `;` ignored, type/kind/this.cs:84-90);
│      │       a MIME no format answers is binary's own kind
│      └─ :65  format.Encode(Stream, data, context, encoding)
│              application/json → item's json kind            PLang/app/type/item/kind/json/this.cs:37-55
│                 :44  Utf8JsonWriter over the stream, default options
│                 :45  data.Output(json.Writer(emitsSchema: false), View.Out, context)
│              text/plain → text's Encode                     PLang/app/type/item/text/this.cs:47-58
│                 :52  the value is a text → its characters in the encoding; else the text Writer
│              other kinds → their type's IEncode, bound at registration (type/kind/this.cs:241-247)
├─ :85   ByteArrayContent over the buffer
├─ :86   httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType) { CharSet = encoding }
├─ :90-91 HttpRequestMessage(method, url) { Content }
├─ :92   ApplyHeaders(request, headers)                       :337-348
│         CR/LF stripped (:342); a content header (IsContentHeader, :350-356: Content-Type, -Length,
│         -Encoding, -Language, -Disposition, -Range) goes on request.Content.Headers, the rest on
│         request.Headers, each by TryAddWithoutValidation (:344, :346)
└─ :101  SendHttpAsync → the HttpClient for this redirect policy (:282-313)
```

The request is not signed here: request signing left this module in a13fd386a (June). `Unsigned` only decides whether an `application/plang` response is accepted (`:380-392`).

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
| native `dict` (`type/item/dict/this.cs:163-174`) | an object; each entry writes itself | entries render (a template entry resolves at `text/this.cs:237-243`) |
| `text` (`type/item/text/this.cs:228-244`) | `:232-235` plain text → `writer.String`; `:237-241` whole `%var%` → the bound value; `:243` a partial template → its rendered string | always a json string, whatever the text's kind |
| file / url reference (`type/item/content/this.cs:113-136`) | sampled → its content (`:117` json into json verbatim, `:118` text as a string, `:119` bytes); unsampled → `:115` its location | the content if sampled, else the path as a string |

## What the channel opens

`stream/this.cs:62` opens `data.Peek()`: the item the Body Data itself holds. For a body written `%var%`, that item is the reference (a `source`), whose `Open` is the default no-op. The bound value is reached later, through `source.Output` (`source.cs:296-300`), and nothing opens it there. A `%file%` body still sends its content only because `Default.cs:66` read the body's value first and the file reference kept the sampled bytes. A value nested inside the body (a file inside a dict) is never opened: it writes its location (`content/this.cs:115`).

## Where it goes wrong today

Reproduced on app-systems at 8a5130ef9 against httpbin.org/anything.

1. **A text body sent as application/json is a json string.** A text writes `writer.String` whatever its kind (`text/this.cs:234`, `:243`, `:258`). A text written in a `.pr` is a wire over its quoted slice, relayed as that string (`wire/this.cs:67`). `ui.render` returns a text with no kind (`module/ui/code/Fluid.cs:125`), so a rendered `.json` template is plain text.
2. **`content type` and a `Content-Type` header are joined.** `:86` sets the content type from the parameter; `:344` then adds the header's value to the same header with `TryAddWithoutValidation`, which appends: `text/plain; charset=utf-8, application/json`. Every content header takes the same path, so a `Content-Length` or `Content-Encoding` header would append too. The upload action (`:187`) goes through the same `ApplyHeaders`.

Stale comments in `request.cs`: `:11` says requests are signed with `X-Signature` (not since a13fd386a); `:24` says "Strings sent as-is" (not since 27c9749d7, June).
