# Stage 2: Merge plang serializers, drop Envelope, sign-in-converter, canonicalization fix

**Goal:** Collapse `application/plang` and `application/plang+data` into one serializer. Delete the `Envelope` class. Move the signing trigger from explicit callers into the `app.data` JSON converter's walk (sign-if-missing, idempotent). Make signing canonicalization match wire-direction serialization so the outer signature binds the inner signatures it walks past.

**Scope:**
- `PLang/app/channels/serializers/serializer/plang/this.cs` — becomes the single, canonical `application/plang` serializer composed over `Json` + the Data converter + Transport/Sensitive modifiers.
- `PLang/app/channels/serializers/serializer/plang/Data.cs` — **deleted**. Its `Envelope` class and `FromEnvelope` factory go away; signature emission moves to whatever `app.data` JSON converter writes the Data wire shape (with the `Transport.ForOutbound` modifier re-including `[Out]` properties).
- `PLang/app/data/` — the `app.data` JSON converter (the one that emits the `{name, type, value, signature}` wire shape) calls `EnsureSigned()` on each Data it visits before emitting. Idempotent: skips if `Signature` is already populated.
- `PLang/app/modules/crypto/code/Default.cs:17-33` — `Hash` switches from default-options `JsonSerializer.Serialize(value)` to the same `Transport.ForOutbound`-configured options the wire serializer uses. After this, hashed-shape and wire-shape are identical, and the outer signature binds the inner signatures present on the wire.
- `PLang/app/channels/serializers/serializer/Json.cs` — gains `WithConverter(JsonConverter)` and `WithModifier(Action<JsonTypeInfo>)` extension methods so the plang serializer can compose without redeclaring options.
- `PLang/app/channels/channel/this.cs` — **no signing call added.** The converter handles signing during the write's serialization walk. `Channel.WriteAsync` stays as-is on that axis.

**Out of scope:**
- Flatten Compress/Decompress (Stage 3).
- Properties flatten to the wire (Stage 4).
- Vocabulary sweep / file rename (Stage 5).
- Algorithm tagging on `archived` / `encryption` (parked, not chasing).

**Deliverables:**

The merged `application/plang` serializer:

```csharp
public sealed class @this : ISerializer {
    public string ContentType => "application/plang";
    public string FileExtension => ".plang";

    private readonly Json _engine;

    public @this(Json engine, actor.context.@this? context = null) {
        // The Data converter walks {name, type, value, signature} and calls
        // EnsureSigned on each Data it visits with no signature. Transport
        // re-includes [Out] properties. Sensitive strips [Sensitive] entries.
        _engine = engine
            .WithConverter(new global::app.data.WireJsonConverter(context))
            .WithModifier(global::app.channels.serializers.filters.Transport.ForOutbound)
            .WithModifier(global::app.channels.serializers.filters.Sensitive.Strip);
    }

    public async Task<Data> SerializeAsync(Stream stream, Data data, CancellationToken ct = default) {
        // No signing call here — the converter signs each Data during its walk.
        try {
            await _engine.SerializeAsync(stream, data, ct);
            return Data.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException) {
            return Data.FromError(new ServiceError(
                $"plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<Data> DeserializeAsync(Stream stream, CancellationToken ct = default) {
        try {
            var result = await _engine.ForInbound().DeserializeAsync<Data>(stream, ct);
            return Data.Ok(result);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException) {
            return Data.FromError(new ServiceError(
                $"plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public Data<string> Serialize(Data data) { ... }
    public Data Deserialize(string s) { ... }
    public Data<T> Deserialize<T>(string s) { ... }
}
```

The Data wire converter — where signing actually fires:

```csharp
// PLang/app/data/WireJsonConverter.cs (or wherever the wire Data converter ends up)
public override void Write(Utf8JsonWriter writer, Data data, JsonSerializerOptions options) {
    if (data.Signature == null) data.EnsureSigned();   // sign-if-missing, idempotent
    writer.WriteStartObject();
    writer.WriteString("name", data.Name);
    writer.WritePropertyName("type"); JsonSerializer.Serialize(writer, data.Type, options);
    writer.WritePropertyName("value"); JsonSerializer.Serialize(writer, data.Value, options);
    writer.WritePropertyName("signature"); JsonSerializer.Serialize(writer, data.Signature, options);
    writer.WriteEndObject();
}
```

Nested Datas in `Value` are reached via STJ's recursion on `data.Value` — STJ resolves the inner Data's converter (this same one) by runtime type, and the inner emit hits the same `EnsureSigned`-if-missing rule. List<Data> elements: same.

Canonicalization fix in `modules/crypto/code/Default.cs`:

```csharp
public data.@this<byte[]> Hash(Hash action) {
    var value = action.Data.Value;
    var bytes = value is byte[] raw
        ? raw
        : JsonSerializer.SerializeToUtf8Bytes(action.Data, WireOptions);   // ← wire-direction options, not default
    var algorithm = action.Algorithm.Value!.ToLowerInvariant();
    // ... unchanged
}

// WireOptions is the same composed options the merged plang serializer uses:
// Json base + WireJsonConverter + Transport.ForOutbound + Sensitive.Strip.
// Pulled from the Serializers registry so canonicalization can't drift from wire shape.
```

Note the shift from `value` to `action.Data`. Today's code hashes `action.Data.Value` — the *inner* JSON of the user fields. The wire shape that gets signed is the full `{name, type, value, signature-cleared}` outer envelope. Hashing the outer envelope means tampering with name or type also invalidates the signature (today they don't). This is a property-strengthening change, not a regression — but it does mean signatures produced before this branch don't verify after, so any persisted signed Data needs to be re-signed once. (Real concern for the deferred-callback / signed-storage cases; flag for the coder.)

The `Envelope` class and `FromEnvelope` factory in `plang/Data.cs` are deleted with the file. The duplicate `_options` block is deleted with the file.

**Dependencies:** Stage 1 (ISerializer takes Data, Stream.WriteCore stops stripping the wrapper).

## Design

**One serializer, one identity, one place for the JSON options.** `Json` owns STJ. The plang serializer composes — it adds the wire Data converter (walks `{name, type, value, signature}`, signs-if-missing) and the two filters (Transport re-includes `[Out]` properties; Sensitive strips marked fields). No `JsonSerializer.X` calls live in `plang/this.cs` anymore — those API calls only exist inside `Json.cs` and inside the wire converter.

**Why signing lives in the converter, not the channel.** Earlier sketches considered putting `EnsureSigned()` at the channel boundary ("only outermost signs"). The shift to converter-driven sign-if-missing makes three properties fall out for free:

1. *Forwarding preserves provenance.* Bob receives Alice's signed D1, wraps it in `D3 { value: D1 }`, writes through his channel. The converter walks D3 → signs (Bob's signature, D3 was unsigned) → walks into Value → reaches D1 → already signed → skips. Charlie sees the chain. No explicit "preserve the inner" choreography.
2. *Compress is automatic.* `Compress(D1)` serializes D1 through the same converter; D1 gets signed during that walk; bytes encode a signed D1. Compress wraps in D2; D2 hits the channel; converter signs D2. Two signatures attesting different things (the user data, the compressed package).
3. *List-of-Data signs per element.* Walking a `List<Data>` inside a Value visits each element through the same converter path; each element gets sign-if-missing. The unit of attestation is the Data node, regardless of tree position.

**Why canonicalize through the wire-direction options.** `crypto/Default.cs:20` currently calls `JsonSerializer.Serialize(value)` with default STJ options. Default options respect `[JsonIgnore]` on `Signature`, so the signing hash *strips* the inner Datas' signatures even though they get emitted on the wire (via Transport.ForOutbound). Result: someone can swap an inner Data's signature without invalidating the outer — the wire-shape and hash-shape diverge. Fix: hash through the same options the wire writer uses. Hashed-bytes ≡ wire-bytes (minus the outermost Signature field, which is excluded from its own hash by the existing `Signature.SigningOptions` resolver). After this change, modifying anything that crosses the wire — name, type, value, inner signatures — fails verification.

**Where the deserialized signature lives.** Channel.ReadAsync doesn't auto-verify. Verification is an explicit step — `signing.verify` action, or a channel event handler bound to `BeforeRead`/`AfterRead`. The wire converter reconstructs each Data with `Signature` populated-but-unverified. Match today's behaviour: "Read does NOT auto-verify."

**`Json.WithConverter` and `WithModifier`.** Composition surface for `Json`:

```csharp
public Json WithConverter(JsonConverter converter) {
    var newOptions = new JsonSerializerOptions(_options);
    newOptions.Converters.Add(converter);
    return new Json(newOptions);
}

public Json WithModifier(Action<JsonTypeInfo> modifier) {
    var newOptions = new JsonSerializerOptions(_options);
    var existing = newOptions.TypeInfoResolver as DefaultJsonTypeInfoResolver
                   ?? new DefaultJsonTypeInfoResolver();
    var resolver = new DefaultJsonTypeInfoResolver();
    foreach (var m in existing.Modifiers) resolver.Modifiers.Add(m);
    resolver.Modifiers.Add(modifier);
    newOptions.TypeInfoResolver = resolver;
    return new Json(newOptions);
}

public Json ForInbound() => WithModifier(Transport.ForInbound);
```

Same shape as `ForView(View)` / `WithIndentation()` already use. Cache chained instances if perf matters — probably not, composition is one-shot at construction.

**`.plang` extension preserved.** The merged serializer keeps `FileExtension => ".plang"`. The `.pdata` extension goes away with `plang/Data.cs`. If any test fixture references `.pdata`, it migrates.

**Registry update.** Wherever `application/plang+data` is registered in `Serializers`, that registration is removed. `application/plang` is the only entry.

**Risks:**
- Callers using `Accept: application/plang+data` over HTTP fail with an unknown content-type. Per "no backward compat during App development" — let them fail; callers rewrite to `application/plang`.
- Every Data on the wire now carries a signature, including ones deep in a Value tree. Previously only the outer carried one. Test fixtures that asserted "nested Data has no signature" need updating. Test fixtures that wrote Data through a channel without a Context will fail at `EnsureSigned`'s context guard — same discipline as production.
- Canonicalization change invalidates pre-existing signatures. Any signed Data persisted before this branch (e.g. deferred-callback rows in sqlite, signed snapshots) won't verify after — needs a one-time re-sign or a migration path. Flag for coder.
- Channel events: `FireBefore` runs before `WriteCore`. The converter's sign-if-missing now sits *inside* `WriteCore` (during the actual serialize). A `BeforeWrite` handler that wants to mutate the Data still works — its mutations land before the converter signs. A handler that wants to short-circuit *signing specifically* doesn't have a hook — but there's no use case for that today.

**What the coder verifies:**
- Full project builds.
- Existing `application/plang` tests pass with the new converter-driven signing (asserting Signature is populated on read, not asserting nesting behaviour).
- `application/plang+data` tests retired or rewritten against the merged endpoint.
- A round-trip test: `Data.Ok("hello")` written through a channel with `Mime = "application/plang"`, read back, signature populated, name + value preserved.
- A forwarding test: a Data with a nested Data round-trips with BOTH signatures intact. Modifying the inner signature post-round-trip fails outer verification.
- A canonicalization test: a signed Data verifies iff its wire bytes are unchanged. Mutating name, type, or any nested-Data's signature in the wire JSON invalidates verification.
- A negative test: writing through a channel with no Context fails with the documented error from `EnsureSigned`.
