# app.channel

A channel is **named I/O for an actor**. Every read, write, and prompt at
runtime flows through one. Channels are *redirectable*: point `output` at a
file, back `logger` with a goal, swap a channel's serializer — without the code
that writes ever knowing.

```csharp
channel(Data<text> name, Data<config> config) : IChannel
```

A channel holds its **config** and forwards to a **stream** (the byte boundary).
`Write` forwards to the stream and stops there — the channel never serializes,
never sees bytes.

The stream is **not** handed in. It comes from implementing `IChannel`, because
the stream is the one thing that differs between channels — *what you write to*:

```csharp
interface IChannel { stream stream { get; } }   // file → file stream, http → response body
```

Implement `IChannel`, supply the stream, and you get `Write` / `Read` for free.

> **No `Async` in the plang layer.** Methods are `Write` / `Read`, never
> `WriteAsync`. Everything is async when it needs to be — that's the `Task`
> return, not the name. The only `WriteAsync` in sight is the underlying .NET
> `Stream.WriteAsync`, called at the byte floor inside `WriteRaw`.

[[app/channel/start.json]]

## what a channel does

```
channel.Write(data)              channel owns the stream — forwards
  └─ stream.Write(data)          stream owns the format + the bytes
       ├─ format.serialize(data) Data → bytes, at the boundary
       └─ WriteRaw(bytes)        the actual byte write
```

Three owners, each doing one thing:

- **channel** — holds config, forwards to the stream, gates on direction.
- **stream** — the only place Data becomes bytes. Serializes with its format,
  then writes raw. Each concrete stream (file, console, memory, http) implements
  only `WriteRaw` / `ReadRaw`.
- **format** — the serializer for a mime. `serialize(Data) → bytes`,
  `deserialize(bytes) → Data`.

## config

Config flows into the channel from `channel.set`, so it rides as `Data<config>`
like every other value. `config` is one thing — a single value carrying all the
knobs — not seven loose parameters:

| knob | type | what it does |
|------|------|--------------|
| `direction` | `direction` | may it write, read, or both |
| `mime` | `mime` | the content type — **selects the serializer and the read-type** |
| `encoding` | `text` | `utf-8` |
| `buffer` | `number` | byte buffer size |
| `timeout` | `duration` | I/O timeout |
| `signing` | `text?` | signing provider; `"auto"` → system identity at write |
| `encryption` | `text?` | encryption provider; none by default |

`mime` is the keystone — the other knobs tune I/O, but `mime` decides *what the
bytes mean*. The two sections below are both really about `mime`.

## the three defaults: output, error, input

Every actor boots with three channels it cannot remove. They differ only in
direction — the console pair is split on purpose:

| name | direction | mime | role |
|------|-----------|------|------|
| `output` | write-only | `text/plain` | user-facing program output |
| `error` | write-only | `text/plain` | user-visible errors |
| `input` | read-only | `text/plain` | stdin / interactive answers |

Split direction means you cannot read `output` or write `input` — a channel
**refuses** the wrong direction with a typed error, it does not silently no-op:

```csharp
public Task<data.@this> Write(data.@this data) =>
    config.direction.canWrite ? stream.Write(data) : config.direction.refuseWrite(name);
```

`write out %x%` resolves `output`; an error write resolves `error`; a read
resolves `input`. A bare `write` with no channel falls back to `output`.

## changing the serializer

You don't set a serializer. **You set the mime, and the serializer follows.**

```plang
- set channel "audit" mime "application/json"
```

The stream resolves its format from the mime, fresh each I/O — never stored:

```csharp
public format format => format.@for(config.mime);   // mime changed → format changed
```

The format registry answers by asking each format if it owns the mime:

```csharp
public format @for(mime mime) => formats.first(f => f.handles(mime));
```

So there is one knob, not two — no way to set a json mime with a text serializer
and have them disagree. To add a *new* serializer, register a `format` in the
registry; no channel or stream changes.

## getting the type (mimetype)

The same `mime` that picks the serializer also gives you the **plang type** —
the type stamped on every value read from the channel:

```csharp
public type.@this type => app.type.from(this);
```

| mime | type |
|------|------|
| `text/plain` | `text` |
| `application/json` | `{object, json}` |
| `text/csv` | `{table, csv}` |
| `image/png` | `{image, png}` |

So a read off a channel arrives already typed — `deserialize` stamps the mime's
type onto the `Data`, and navigation (`%x.field%`, `%x as table%`) works without
anyone sniffing the content. One knob, two answers: the mime maps the wire to a
serializer *and* to a type.

## OBP rules

- **The channel owns the stream; the stream owns the bytes.** `channel.Write`
  forwards; serialization lives at the byte boundary, never in the channel or a
  registry.
- **Config is settled state, not Data.** Only the Write/Read payload flows as
  `Data`. Config is the channel's own typed properties.
- **One knob, not two.** `mime` selects both the serializer and the type — they
  can't drift out of agreement.
- **A channel refuses the wrong direction.** A typed error, never a silent no-op.
- **The list owns lookup; the channel answers its name.** `channels.find(name)`
  → `channel.is(name)`, never the list reaching into a channel's fields.

## source

`app/channel/start.cs`

[[app/channel/start.cs]]
