# Data Serialization Cleanup

## Why

The immediate trigger was a code-review session on `PLang/app/channels/serializers/serializer/plang/Data.cs`: the `Envelope` class duplicates Data's shape just to bypass `[JsonIgnore]`; `JsonSerializer` is hard-coded inside what should be a registered, composable serializer; `if (value == null) return "null"` is dead boilerplate; the `Serialize(object? value, Type? type = null)` signature lies about what the method actually serializes (always Data in practice). The word "envelope" leaks an inverted mental model — Data IS the wire shape, not something wrapped *in* an envelope.

Pulling the thread surfaced the same smells in adjacent files: `data/this.Envelope.cs` instantiates a parallel `JsonSerializerOptions` block; `Stream.WriteCore` strips the Data wrapper before serializing; `Compress()` double-wraps `Data{archived, Data{gzip, bytes}}` when the inner gzip Data is redundant. Multiple files reinforce each other's workarounds — each on its own looks reasonable, together they encode a misconception of where Data sits in the stack.

The bigger reason to fix this now, before the next thing lands: PLang's serialization is the central boundary. Every developer ships data across it. The current shape is fragile (multiple parallel JsonSerializerOptions can drift), doesn't extend cleanly (the `Envelope` workaround would have to be re-invented for any new MIME), and structurally confuses Data's role. Cleaning it up before more serializers or channel types are added prevents the smells from multiplying. It also clears the way for the structural-normalization follow-up (`data-normalize` branch) — none of that work makes sense on top of the current tangle.

*Ingi's deeper motivation, if there's more to it than the code-review trigger — a specific deployment, a downstream consumer breaking on this, an upcoming feature that needs the clean foundation — would go here. The above is what surfaced in conversation.*

## What this is

PLang's serialization path between a Variable and the wire is tangled across four files and three concepts. This branch un-tangles it by recognizing one thing: **Data is the universal currency. Everything that crosses a channel boundary IS Data. The wire shape is Data's own shape — flat — and any nesting lives in the byte stream, not the JSON document.**

Once that's accepted, three follow-on simplifications fall out: ISerializer narrows from `object?` to `Data`, the two `application/plang` serializers collapse into one, signing moves from the serializer to the channel.

## The problem today

Four interacting smells in `PLang/app/channels/serializers/serializer/plang/Data.cs` and its neighbours:

1. **The `Envelope` class shouldn't exist.** The plang+data serializer builds a parallel `Envelope` type (`{Type, Value, Signature}`) to bypass `[JsonIgnore]` on `Data.Signature`. The parallel type is a workaround for using STJ's ignore discipline as the wire-direction gate. Data IS the envelope; there's no envelope around Data.

2. **STJ options are configured three places.** `serializer/plang/this.cs`, `serializer/plang/Data.cs`, and `data/this.Envelope.cs` each instantiate near-identical `JsonSerializerOptions`. `Json.cs` is the JSON engine custodian — the plang serializer should compose with it, not duplicate it.

3. **The serializer must always receive Data.** That's the contract — Data in, bytes out. The MIME's identity decides what to emit (wrapper or just the value); the input shape is fixed. Today nothing enforces this: `ISerializer` accepts `object?`, and `Stream.WriteCore` feeds it `data.Value` rather than `data` itself (`stream/this.cs:56`). The plang+data serializer is then forced to reconstruct the wrapper from scratch via `Envelope` because by the time it runs, the wrapper that should carry type+signature is already gone. (Note — the *return* side of ISerializer was tightened to `Data` by the `typed-action-returns` merge: errors now flow through `Data.Error` instead of throwing. The *input* side — `object? value` and the `Type? type = null` parameter — is what this branch closes.)

4. **`Compress()` double-wraps.** Current code builds `Data{archived, Data{gzip, byte[]}}`. The inner gzip Data is redundant — `archived.Value` can be a `byte[]` directly. "Data all the way down" is a property of *byte layers* (decompress reveals another serialized Data), not JSON object nesting.

There are also two MIME types (`application/plang` and `application/plang+data`) that should be one. PLang transport without Data is meaningless; the `+data` suffix is empty information.

## The shape we're moving to

For a plain `%user%` variable, the wire is four flat fields:

```json
{
  "name": "user",
  "type": "<whatever Type.Value is>",
  "value": { ... },
  "signature": { ... }
}
```

For a compressed `%user%`:

```json
{
  "name": "",
  "type": "archived",
  "value": "H4sIAAAA...",
  "signature": { ... }
}
```

The compressed bytes — when base64-decoded and gunzipped — *are themselves* a serialized Data with name "user". Nesting happens through byte-decoding layers, not JSON object nesting. Each Data on the wire is flat.

Concrete JSON examples (plain, compressed, encrypted, nested-value) live in [plan/wire-shape.md](plan/wire-shape.md).

## Cross-cutting decisions settled

**Name stays on the wire.** It's the addressability coordinate that survives transport. The HTTP example: sender names a Data "user", receiver writes the response to `%response%`, and `%response.user%` works because the structure remembered itself. Without name, that breaks.

**Signing lives at the channel.** `Channel.WriteCore` calls `data.EnsureSigned()` before invoking the serializer. The serializer never signs — it just emits whatever Signature is set. Why not sign in the serializer? Because `Compress` also serializes Data (to in-memory bytes that will be buried inside an archived wrapper). If the serializer signed, those buried bytes would carry a signature *and* the channel would sign the archived wrapper on its way out — two signatures for one logical payload. Moving the signing decision out of the serializer makes "only outermost signs" automatic by construction: channels sign because their writes are externally visible; in-pipeline transforms (Compress, Encrypt) don't sign because their output stays in-process and gets buried inside another Data. The serializer is unconscious of inner/outer — that's not its concern.

**Data is opaque to its consumers.** Every layer — Variables, action handlers, Compress, Channel, ISerializer — handles Data as an opaque unit. Only the encoder at the leaf walks `[Out]` properties. Nothing peeks at Type or Value to decide behaviour. (See [design_principles.md → Data Is Opaque to Its Consumers](../../../memory/design_principles.md).)

**ISerializer takes Data, returns Data.** The return half landed in `typed-action-returns` (every method now returns `Data` / `Data<T>`, errors travel as `Data.Error` instead of throwing). The input half is what this branch closes: `object? value` and `Type? type = null` become a single `Data data` parameter. Tightening eliminates the null branch (`if (value == null) return "null"`), the `Type? type` parameter (Data carries its own type), and the "what if it's not Data" fallthrough.

**`+` variants for encoding/algorithm differentiation.** `application/plang` defaults to JSON; `application/plang+protobuf` is the future binary variant. Same pattern for transport types: `archived+gzip`, `encryption+aes-256-gcm`. Today only the defaults exist; the variant slot is reserved, not used.

## Out of scope

- **Algorithm tagging on `archived` / `encryption`.** Ingi flagged that "archived" alone is insufficient — the decoder needs to know which compression routine to call. Same for encryption. The fix is the `+variant` pattern. **Parked**, not chasing it in this branch.
- **Protobuf or other non-JSON encoders.** The variant slot is reserved in MIME naming; no implementation yet.
- **Encrypt path.** Same shape as Compress (flat wrapper, value is ciphertext bytes), but Encrypt today is a stub awaiting a crypto service. Flattening lands when crypto lands.
- **Inspection-path serialization** (`data.ToString()` for debug logs). Different concern from wire path — fixed JSON, no signing. Not touched here.

## Stages

| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer input tightened to Data](stage-1-iserializer-data.md) | partial — return half landed via `typed-action-returns`, input half remains |
| 2 | [Merge application/plang serializers + drop Envelope + signing moves](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Vocabulary sweep — drop "envelope" + rename this.Envelope.cs](stage-4-vocabulary-sweep.md) | pending |

Stages 1 and 2 are tightly coupled — the input-tightening in Stage 1 forces all four serializer implementations to update at once, and Stage 2's merge sits naturally on top. They could be one PR or two depending on review appetite; the design is one decision. Stage 3 follows once the serializer is reliable. Stage 4 can land any time after Stage 2.

## Test surface

Test strategy and coverage live in [plan/test-strategy.md](plan/test-strategy.md) and [plan/test-coverage.md](plan/test-coverage.md) — to be written after stages are reviewed and settled.

Key behaviours to pin:
- A `Data.Ok("hello")` round-trips through `application/plang` losslessly (name, type, value, signature all preserved).
- A compressed Data round-trips: outer wrapper signed; inner bytes are themselves a serialized Data with the original name preserved.
- Inner Datas in a nested value field carry no signature on the wire; outer carries exactly one.
- ISerializer cannot be invoked with a non-Data input — boundary throws a structured error.
- `%response.user%` navigation works after deserialization (name preserved across the round trip).
