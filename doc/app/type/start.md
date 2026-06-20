# app/type

**The type instance IS the value.** `Data` carries; the type does all its own work.

`type` and `kind` are always two separate fields. `type.Name` never contains a slash — `"image/jpeg"` is a MIME string from the outside world; inside PLang it is `type=image, kind=jpg`. The slash form dies at the perimeter.

## Type is decided by where a value lives, not what it looks like

A bare string `"photo.jpg"` could be a filename, a variable reference, or a text label — the string alone doesn't tell you. The slot context in the `.pr` decides. That's why the builder stamps `type` and `kind` onto each parameter at build time. A component that only sees the raw value cannot determine the type; it has to be told by the slot.

Two cooperating `Build` calls stamp a value:

1. The **action's** `Build()` decides the return type when it's dynamic — `file.read` reads the file extension and resolves it to `image`.
2. The **type's own** `Build(value)` decides the kind — `image.Build("photo.jpg")` → `"jpg"`.

They're independent. The first answers "what type is this action producing?" The second answers "what refinement within that type?"

## I/O content starts as `binary` — it narrows lazily

Bytes arriving off I/O (file, http, stream) are not typed yet. They come in as `type=binary` with a `kind` that names what they contain:

```
GET response, Content-Type: text/csv  →  binary/csv
read config.json from disk            →  binary/json
read photo.jpg from disk              →  binary/jpg
```

`%x!type%` reads `"binary"` — the sync property, no decode. `%x.foo%` navigates into the value, which calls `Value()`, which runs the kind's reader and rebinds the holding Data to the real type (`table`, `dict`, `image`, …). The decode happens once, at the moment you first use the value.

In-memory values already have their type — they don't go through the `binary` stage. `as md` on a text value in memory refines the kind; it doesn't re-wrap in binary.

Reader lookup for a `binary` holder: kind names the inner type, then look up that type's reader. `binary/json` → kind `json` → type `dict` → the `(dict, json)` reader.

## Kinds

Kinds are not subtypes. They are refinements — they tell readers and renderers which encoding to use within a shape.

- `number` kinds: `int`, `decimal`, `double`
- `image` kinds: `jpg`, `png`, `gif`
- `code` kinds: `python`, `csharp`, `js`
- `path` kinds: `http`, `file`

The LLM only sees a type's kinds when developer-meaningful (number's precision variants). Otherwise `Build()` derives the kind silently.

## Subtopics

- [list/](list/start.md) — indexed sequence; the slot model
- [dict/](dict/start.md) — key→value map; the slot model
