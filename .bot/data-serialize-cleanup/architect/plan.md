# Data Serialization Cleanup

## Why

The immediate trigger was a code-review session on `PLang/app/channels/serializers/serializer/plang/Data.cs`: the `Envelope` class duplicates Data's shape just to bypass `[JsonIgnore]`; `JsonSerializer` is hard-coded inside what should be a registered, composable serializer; `if (value == null) return "null"` is dead boilerplate; the `Serialize(object? value, Type? type = null)` signature lies about what the method actually serializes (always Data in practice). The word "envelope" leaks an inverted mental model — Data IS the wire shape, not something wrapped *in* an envelope.

Pulling the thread surfaced the same smells in adjacent files: `data/this.Envelope.cs` instantiates a parallel `JsonSerializerOptions` block; `Stream.WriteCore` strips the Data wrapper before serializing; `Compress()` double-wraps `Data{archived, Data{gzip, bytes}}` when the inner gzip Data is redundant. Multiple files reinforce each other's workarounds — each on its own looks reasonable, together they encode a misconception of where Data sits in the stack.

The bigger reason to fix this now, before the next thing lands: PLang's serialization is the central boundary. Every developer ships data across it. The current shape is fragile (multiple parallel JsonSerializerOptions can drift), doesn't extend cleanly (the `Envelope` workaround would have to be re-invented for any new MIME), and structurally confuses Data's role. Cleaning it up before more serializers or channel types are added prevents the smells from multiplying. It also clears the way for the structural-normalization follow-up (`data-normalize` branch) — none of that work makes sense on top of the current tangle.

*Ingi's deeper motivation, if there's more to it than the code-review trigger — a specific deployment, a downstream consumer breaking on this, an upcoming feature that needs the clean foundation — would go here. The above is what surfaced in conversation.*

## What this is

PLang's serialization path between a Variable and the wire is tangled across four files and three concepts. This branch un-tangles it by recognizing one thing: **Data is the universal currency. Everything that crosses a channel boundary IS Data. The wire shape is Data's own shape — flat — and any nesting lives in the byte stream, not the JSON document.**

Once that's accepted, four follow-on simplifications fall out: ISerializer narrows from `object?` to `Data`, the two `application/plang` serializers collapse into one, signing happens implicitly during the serializer's converter walk (sign-if-missing — per Data, not per wire crossing), and Properties get their own scope on the wire as a nested `properties` object next to `name`/`type`/`value`/`signature`.

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

The compressed bytes — when decoded per the wire format's convention (base64 in JSON because JSON has no binary literal; raw bytes in protobuf/CBOR) and gunzipped — *are themselves* a serialized Data with name "user". The base64 isn't intrinsic to the shape; it's the JSON serializer's choice for representing `byte[]`. Nesting happens through byte-decoding layers, not JSON object nesting. Each Data on the wire is flat.

Concrete JSON examples (plain, compressed, encrypted, nested-value) live in [plan/wire-shape.md](plan/wire-shape.md).

## Cross-cutting decisions settled

**Name stays on the wire.** It's the addressability coordinate that survives transport. The HTTP example: sender names a Data "user", receiver writes the response to `%response%`, and `%response.user%` works because the structure remembered itself. Without name, that breaks.

**Signing happens during the serializer walk — sign-if-missing, per Data, not per wire crossing.** The `app.data` JSON converter is the single signing point. Each Data the converter visits during serialization: if `Signature` is null, call `EnsureSigned` before emitting; if it's already populated, leave it alone. `EnsureSigned` is idempotent — a no-op when a signature exists. The channel doesn't sign explicitly; the converter does it during the walk that the channel's write triggers.

Why this and not "outermost only at the channel": the unit of attestation is the *Data node*, not the *wire boundary*. Three behaviours fall out for free:

- **Forwarding preserves provenance.** Alice's Data D1 (unsigned) goes through her channel; the converter signs D1 with Alice. Bob receives D1 (signed by Alice), wraps it as `D3 { value: D1 }`, writes to his channel; the converter sees D3 unsigned → signs with Bob, sees D1 signed → leaves it. Charlie sees D3 signed by Bob, opens it, sees D1 still signed by Alice. No explicit "preserve the inner signature" choreography; it's built in.
- **Compress is automatic.** `Compress(D1)` serializes D1 to bytes through the same converter; the converter signs D1 during the walk; the bytes encode a signed D1. `Compress` wraps in `D2 { type=archived, value=byte[] }`; D2 hits the channel; converter signs D2. Two signatures, two different attestations: D1 signs the user data; D2 signs the compressed package. Both rest on the same `EnsureSigned` rule fired at different walk depths.
- **List of Data — each element signs independently.** When the converter walks a `List<Data>` inside a Value, each list element is its own Data and gets sign-if-missing. The unit-of-attestation rule is uniform regardless of how the Data sits in the tree.

Canonicalization rule: signing must hash the same bytes the wire writes. Today `modules/crypto/code/Default.cs:20` calls `JsonSerializer.Serialize(value)` with no options — default STJ respects `[JsonIgnore]` on `Signature` and strips it. The fix is to canonicalize through the same `Transport.ForOutbound` options the wire-direction serializer uses, so hashed-shape and wire-shape are identical. This also closes the gap where today's outer signature doesn't bind inner-Datas'-signatures (because the inner `Signature` field gets `[JsonIgnore]`-stripped from the hash, but emitted on the wire). After the canonicalization fix, the outer signature transitively binds every inner signature exposed on the wire.

Sign-if-missing walks **Value-graph Datas only**, never Properties. Properties are metadata about the Data, not part of the signed body — a Data with cost/timing/debug Properties shouldn't grow per-Property signatures. The outer Data's signature covers the canonicalized wire shape (which includes the flat Property key-values), so tampering with a Property still invalidates the outer signature, but no nested attestations are conjured.

**Data is opaque to its consumers.** Every layer — Variables, action handlers, Compress, Channel, ISerializer — handles Data as an opaque unit. Only the encoder at the leaf walks `[Out]` properties. Nothing peeks at Type or Value to decide behaviour. A consumer that needs to branch on what's inside has stopped being a consumer and started being an encoder — push the work down to the leaf.

**ISerializer takes Data, returns Data.** The return half landed in `typed-action-returns` (every method now returns `Data` / `Data<T>`, errors travel as `Data.Error` instead of throwing). The input half is what this branch closes: `object? value` and `Type? type = null` become a single `Data data` parameter. Tightening eliminates the null branch (`if (value == null) return "null"`), the `Type? type` parameter (Data carries its own type), and the "what if it's not Data" fallthrough.

**Properties get their own scope on the wire.** Today `Properties` is `IList<Data>` (see `PLang/app/data/Properties.cs`) and is `[JsonIgnore]` — it never crosses the wire. The new design changes both:

- C# shape: `Properties` becomes `Dictionary<string, object?>` with primitives only (string, int, bool, DateTime, byte[], `Dictionary<string,object?>`, `List<object?>` of primitives). No Data instances inside Properties.
- Wire shape: Properties emit as a single nested `properties` object — a fifth top-level field next to `name`/`type`/`value`/`signature`. An LLM response is `{ name: "response", type: "string", value: "...", properties: { cost: 100, model: "claude" }, signature: {...} }`.
- Property keys are unconstrained: any string can be a Property key. Because Properties live in their own object, there's no collision risk with the reserved top-level fields. A Property named `"value"` or `"signature"` is fine — it lives at `properties.value`, not at the root.
- Receiver-side rule: on deserialize, the five reserved top-level fields (`name`, `type`, `value`, `properties`, `signature`) bind to their Data slots; any other top-level field is silently ignored (default STJ behaviour). The `properties` object is parsed verbatim into the Properties dictionary.
- Access syntax: `%x.field%` reads `value`'s structural shape (object property, dict key); `%x!key%` reads `Properties[key]`. The two namespaces are disjoint: `%user.kind%` and `%user!kind%` can coexist with different values.
- Sign discipline: the sign-if-missing converter walks Value-graph Datas only and does not visit Properties. The outer Data's signature covers the `properties` object via canonicalization — tampering with any Property fails verification, but Properties don't grow nested signatures.

The typed path (`Data<T>` where `T` declares a property like `Cost`) keeps its structural shape — `Cost` lives inside `T`, serializes inside `value`. Properties is the *untyped* metadata channel; `Data<T>`'s named properties are the *typed* path. Either gets navigated via the same `%x.field%`/`%x!key%` discipline, just routed to different storage.

Out of scope for this branch (worth a follow-up): public/private split on Properties (debug-only entries that shouldn't cross the wire), `[Sensitive]` on individual Property keys, structured (Data-typed) Property values. For this branch: all Properties cross, primitives only.

**`+` variants for encoding/algorithm differentiation.** `application/plang` defaults to JSON; `application/plang+protobuf` is the future binary variant. Same pattern for transport types: `archived+gzip`, `encryption+aes-256-gcm`. Today only the defaults exist; the variant slot is reserved, not used.

**Transforms are a pattern, not a registry.** Compress is one instance of a general shape: take a Data, produce a Data whose `Value` is bytes (the transformed payload) and whose `Type` names the transformation. The full family:

| Verb | Input | Output |
|------|-------|--------|
| `data.Compress()` | Data | Data { type=archived, value=byte[] (gzip-of-serialized-Data) } |
| `data.Encrypt()` | Data | Data { type=encrypted, value=byte[] (cipher-of-serialized-Data) } |
| `data.Sign()` / `EnsureSigned()` | Data | same Data with Signature populated (in-place; no wrapping) |
| Future: `data.Brotli()`, `data.Zstd()` | Data | Data { type=archived+brotli, value=byte[] } |

No separate registry. The verb is a method on Data; the wire marker is a `Type` value; algorithm differentiation rides on the `+variant` slot. Decoding mirrors: `Decompress()` peels `archived`, `Decrypt()` peels `encrypted`, with the variant naming the routine. Type's own `.Compressible` / `.Encryptable` properties (when added) gate whether a transform applies — same pattern Compress already uses today.

The serializer is unaware of any of this. It serializes Data; what's *inside* Data — primitives, bytes, nested Data — is opaque. Transforms produce a new Data; the serializer renders it. The fact that "byte[] inside archived" used to be "Data inside Data" was the smell — flat wrapping (Stage 3) makes the pattern uniform across all transforms.

## Out of scope

- **Algorithm tagging on `archived` / `encryption`.** Ingi flagged that "archived" alone is insufficient — the decoder needs to know which compression routine to call. Same for encryption. The fix is the `+variant` pattern. **Parked**, not chasing it in this branch.
- **Protobuf or other non-JSON encoders.** The variant slot is reserved in MIME naming; no implementation yet.
- **Encrypt path.** Same shape as Compress (flat wrapper, value is ciphertext bytes), but Encrypt today is a stub awaiting a crypto service. Flattening lands when crypto lands.
- **Inspection-path serialization** (`data.ToString()` for debug logs). Different concern from wire path — fixed JSON, no signing. Not touched here.

## Stages

| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer input tightened to Data](stage-1-iserializer-data.md) | partial — return half landed via `typed-action-returns`, input half remains |
| 2 | [Merge application/plang serializers + sign-in-converter + canonicalization fix](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Properties flatten to the wire](stage-4-properties-on-wire.md) | pending |
| 5 | [Vocabulary sweep — drop "envelope" + rename this.Envelope.cs](stage-5-vocabulary-sweep.md) | pending |

Stages 1 and 2 are tightly coupled — the input-tightening in Stage 1 forces all four serializer implementations to update at once, and Stage 2's merge (plus the sign-in-converter rewire and the crypto canonicalization fix) sits naturally on top. They could be one PR or two depending on review appetite; the design is one decision. Stage 3 follows once the serializer is reliable. Stage 4 (Properties) is independent of Stage 3 but shares Stage 2's converter rework, so it should land after Stage 2. Stage 5 can land any time after Stage 2.

## Test surface

Test strategy and coverage live in [plan/test-strategy.md](plan/test-strategy.md) (the narrative — scope, layer mapping, four integration cuts) and [plan/test-coverage.md](plan/test-coverage.md) (the heavy reference — coverage matrix per stage, consolidated failure matrix, new-surfaces inventory). Test-designer reads strategy top-to-bottom for the cuts, then walks the coverage matrix row-by-row writing one test per row.

Key behaviours to pin (already encoded in the matrix; here for quick scan):
- A `Data.Ok("hello")` round-trips through `application/plang` losslessly (name, type, value, signature, properties all preserved).
- A compressed Data round-trips: outer wrapper signed; inner bytes decode to a serialized Data with its own signature (sign-then-compress chain preserved).
- Inner Datas in a nested value field each carry their own signature on the wire (sign-if-missing rule applied during the converter walk).
- Signing canonicalization matches wire-serialization shape — modifying an inner signature in the JSON invalidates the outer signature.
- Properties on the wire live inside a nested `properties` object; the C# Dictionary round-trips through it unchanged.
- `%response.user%` navigates into Value; `%response!cost%` navigates into Properties; the two are disjoint.
- ISerializer cannot be invoked with a non-Data input — the type system forbids it after Stage 1.
