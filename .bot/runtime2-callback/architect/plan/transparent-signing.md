# Data signing — Data signs itself; Serializers decide what's on the wire

Two pieces, owned by different `@this`:

- **`Data.@this` owns its own signing.** `Data.Signature` is a lazy property: first access populates it via the `signing` module. Data carries a `Context` reference (it's an `@this` constructed inside an App, ambient context is cheap), so the lazy getter has everything it needs. No external coordinator says "remember to sign before writing."
- **`Serializer.@this` decides the wire shape.** One serializer per mimetype family. The serializer reads from Data's surface (`Value`, `Type`, `Signature`) and emits whatever its mimetype demands. It's the serializer's choice to read `Data.Signature` (which triggers signing) or not.

Channels orchestrate: pick the right serializer for the receiver's mimetype, hand it Data + stream. Channels never sign and never know mimetype shapes — they just route.

## Three classes, three concerns

```
Channel.@this
└── Receives Data, knows the receiver's mimetype, picks the right Serializer,
    calls Serializer.Write(data, stream, ctx). Done.

Serializer.@this — one per mimetype family
├── JsonSerializer       (text/html, application/json)         → emits data.Value only
├── PlangDataSerializer  (application/plang+data)              → emits full envelope: Type + Value + Signature
├── DbValueSerializer    (db column writes — value-only path)  → emits data.Value
├── DbDataSerializer     (db column writes — full-Data column) → emits full envelope
└── …
Each owns its mimetype's wire shape. Reading data.Signature triggers signing
on Data; not reading it skips signing entirely.

Data.@this
├── Value, Type, Signature
├── Signature is a lazy property — first access populates via signing module
└── Holds Context internally (set on construction, ambient)
```

## How signing actually happens

`Data.Signature` is a lazy property, roughly:

```csharp
public Signature.@this Signature
{
    get
    {
        if (_signature == null)
            _signature = signing.SignAsync(this, expiresInMs: ExpiresFromContext()).Result;
        return _signature;
    }
}

private int? ExpiresFromContext() =>
    Value is ICallback
        ? Context.App.Callback.Signature.ExpiresInMs   // app-level config
        : null;
```

Async-in-property is a sketch — the real implementation is `Task<Signature.@this> EnsureSignatureAsync()` exposed as an awaitable that the lazy `Signature` getter blocks on once. The point is: from any caller's view, reading `data.Signature` produces a populated Signature. Whether signing actually ran depends on whether anyone asked.

For a JSON response to a browser (`Accept: text/html`), `JsonSerializer` writes `data.Value` and never touches `data.Signature`. No signing cost. Pure win.

For a callback envelope (`application/plang+data`), `PlangDataSerializer` writes Type + Value + Signature. Reading `data.Signature` triggers signing if not yet done. The signature is part of the wire shape; that's why this serializer exists.

## Why this is OBP-clean

- **Data owns its signing.** No external `PrepareForOutput` verb, no Channel-side hook ordering, no "did someone forget to sign before writing" question. Data is responsible for its own integrity discipline.
- **Mimetype knowledge lives on Serializers, not on Data.** Each serializer is `@this` for its mimetype family's wire shape. Data doesn't know HTTP exists.
- **Channels orchestrate, don't sign.** Channel picks the right serializer based on the receiver's accept and hands off. No signing logic in Channels.
- **Lazy signing means no cost when the wire shape doesn't include the signature.** Debug logs, value-only HTTP responses, plain DB writes — none of them pay the Ed25519 cost.

## On read

Symmetric: serializers per mimetype know how to read their wire shape into a `Data.@this` with `Signature` populated (unverified). Verification is the consumer's explicit step (`signing.verify`), invoked by `callback.Run(ctx)` and by `- verify %x%` — not automatic on read. Otherwise every Data read pays a crypto cost when most readers don't care about integrity.

## `application/plang+data` MIME type

This branch introduces `application/plang+data` — the mimetype that says "I can consume the full envelope, including signature." It's the binary/full-shape sibling of `application/plang+json` (which already exists in the codebase as the JSON-shape variant).

| Channel | Default emission | Full-envelope emission |
|---|---|---|
| HTTP response | `data.Value` (when `Accept: text/html` or `*/*`) | full envelope when `Accept: application/plang+data` |
| DB insert | `data.Value` only | full envelope when the column is explicitly typed/configured for it |
| File write | `data.Value` as the file body | (out of scope this branch — see "File sidecar" below) |
| Console | `data.Value` (string render) | full envelope when console started with `accept: application/plang+data` |

The architectural rule: **Data signs lazily; Serializers decide whether to read the signature; Channels route.**

## Callback expiry: config on App

`app.Callback.Signature.ExpiresInMs` (default `null`) is config, not a property of any `ICallback` instance. The `Data.Signature` lazy getter reads it from `Context.App.Callback.Signature.ExpiresInMs` *only when the wrapped value is an `ICallback`*. Other Data writes don't pick up the callback expiry config.

```plang
- set callback timeout to 5 minutes
```

writes `app.Callback.Signature.ExpiresInMs = 300000`. The next callback materialization that reaches a Serializer will get its expiry seeded from this.

## File sidecar — out of scope

For file channels, the full-envelope shape would be: `file.jpg` carries `data.Value`, `file.jpg.signature` sibling carries the signature. Future work would extend to `file.jpg.plang` as a binary-encoded full Data envelope. Both are noted here for forward-compatibility; neither lands in this branch.

## Why default no-expiry

Error callbacks may legitimately be inspected and re-run weeks or months later. Integrity ("this is the unmutated callback we issued") is the durable guarantee; validity ("still valid at time T") is opt-in. AskCallback usually wants a short expiry (the asker should answer within minutes); ErrorCallback often wants none. Both are controlled the same way: `app.Callback.Signature.ExpiresInMs`.

## Performance

Ed25519 signing is ~50µs. For the volume of Data IO PLang does, this is in the noise — and lazy signing means it only runs when a serializer actually reads `Signature`. Most Data instances pay zero crypto cost. Don't pre-optimize.
