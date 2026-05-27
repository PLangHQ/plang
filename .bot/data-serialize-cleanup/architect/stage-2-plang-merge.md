# Stage 2: Merge plang serializers, drop Envelope, signing moves to channel

**Goal:** Collapse `application/plang` and `application/plang+data` into one serializer. Delete the `Envelope` class. Move `EnsureSigned()` from the serializer to `Channel.WriteCore`. Compose with the `Json` engine instead of declaring duplicate STJ options.

**Scope:**
- `PLang/app/channels/serializers/serializer/plang/this.cs` — becomes the single, canonical `application/plang` serializer.
- `PLang/app/channels/serializers/serializer/plang/Data.cs` — **deleted**. Its responsibilities (signing trigger, signature emission) move out: signing to `Channel.WriteCore`, signature emission via the `app.data.Json` converter already wired into `plang/this.cs`.
- `PLang/app/channels/channel/this.cs` — `WriteAsync` (or `WriteCore` callers) calls `data.EnsureSigned()` before invoking the serializer.
- `PLang/app/channels/serializers/serializer/Json.cs` — gains a `WithConverter(JsonConverter)` extension method (if not already present) so plang can compose without redeclaring options.

**Out of scope:**
- Flatten Compress/Decompress (Stage 3).
- Vocabulary sweep / file rename (Stage 4).
- Algorithm tagging on `archived` / `encryption` (parked, not chasing).

**Deliverables:**

The merged `application/plang` serializer:

```csharp
public sealed class @this : ISerializer {
    public string ContentType => "application/plang";
    public string FileExtension => ".plang";

    private readonly Json _engine;

    public @this(Json engine, actor.context.@this? context = null) {
        // app.data.Json is the converter that walks Data; Transport filter re-includes [Out].
        _engine = engine
            .WithConverter(new global::app.data.Json())
            .WithModifier(global::app.channels.serializers.filters.Transport.ForOutbound)
            .WithModifier(global::app.channels.serializers.filters.Sensitive.Strip);
    }

    public async Task<Data> SerializeAsync(Stream stream, Data data, CancellationToken ct = default) {
        // No EnsureSigned here — the channel handles it before this call.
        // Errors flow through Data.Error (the typed-action-returns shape, unchanged here).
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
        // Symmetric — Json + DataConverter + Transport.ForInbound modifier.
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

`Channel.WriteAsync` (in `channel/this.cs`) adds the signing call before WriteCore:

```csharp
public virtual async Task<Data> WriteAsync(Data data, CancellationToken ct = default) {
    var beforeAborted = await FireBefore(EventType.BeforeWrite, data);
    if (beforeAborted != null) return beforeAborted;

    data.EnsureSigned();   // moved from the serializer — channel signs because writes are externally visible
    
    Data result;
    try { result = await WriteCore(data, ct); }
    catch (...) { ... }
    
    await FireAfter(EventType.AfterWrite, result);
    return result;
}
```

The `Envelope` class and `FromEnvelope` factory in `plang/Data.cs` are deleted with the file. The duplicate `_options` block is deleted with the file.

**Dependencies:** Stage 1 (ISerializer takes Data, Stream.WriteCore stops stripping the wrapper).

## Design

**One serializer, one identity, one place for the JSON options.** `Json` owns STJ. The plang serializer composes — it adds the `DataConverter` (walks `{name, type, value, signature}`) and the two filters (Transport re-includes `[Out]` properties; Sensitive strips marked fields). No `JsonSerializer.X` calls live in `plang/this.cs` anymore — those API calls only exist inside `Json.cs` and inside the `DataConverter`.

**Why signing moves to the channel.** The earlier sketch placed `EnsureSigned()` at the serializer's entry point. That fails the Compress scenario: when Compress serializes a Data to bytes for in-process gzipping, it must NOT sign — the bytes will be buried inside another Data and signing them now would create two signatures (inner + outer). Solution: the serializer becomes pure — it emits whatever Signature is set, never decides. The CHANNEL signs (its writes are externally visible). In-pipeline transforms (Compress in Stage 3, future Encrypt) don't sign — their output stays in-process.

That makes "only outermost signs" automatic by construction. The serializer is unconscious of inner/outer. The signing decision lives one layer up, where the externally-visible-vs-internal distinction is real.

**Where the deserialized signature lives.** Channel.ReadAsync doesn't auto-verify. Verification is an explicit step — `signing.verify` action, or a channel event handler bound to `BeforeRead` / `AfterRead`. The serializer reconstructs the Data with `Signature` populated-but-unverified. This matches today's behaviour (`plang/Data.cs` comments call this out explicitly: "Read does NOT auto-verify"). Keep that contract.

**`Json.WithConverter` and `WithModifier`.** These extension methods are the composition surface. Today `Json.cs` exposes `ForView(View)` and `WithIndentation()` — same pattern. Add:

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

Cache the chained instances if perf matters (same pattern as `_viewCache` already does). Probably not — composition is one-shot at construction.

**`.plang` extension preserved.** The merged serializer keeps `FileExtension => ".plang"`. The `.pdata` extension goes away with `plang/Data.cs`. If any test fixture references `.pdata`, it migrates.

**Registry update.** Wherever `application/plang+data` is registered in `Serializers`, that registration is removed. `application/plang` is the only entry.

**Risks:**
- Callers using `Accept: application/plang+data` over HTTP fail with an unknown content-type unless we map it to `application/plang` for compatibility. Per Ingi's "no backward compat during App development" policy — fine, let them fail; rewrite to `application/plang`.
- The signing-at-channel move means anyone writing through `channel.WriteAsync` now signs. If a test fixture writes Data through a channel without a Context, `EnsureSigned()` throws `InvalidOperationException`. Tests need to wire Context — same discipline as production.
- Channel events firing around the signing call: today `FireBefore` runs before `WriteCore`; the new `EnsureSigned()` sits between `FireBefore` and `WriteCore`. A `BeforeWrite` event handler that wants to short-circuit *before signing* still works. A handler that wants to mutate the Data before signing — that's a real consideration. Recommendation: keep `EnsureSigned()` AFTER `FireBefore` so handlers can finalize the Data before it's signed.

**What the coder verifies:**
- Full project builds.
- Existing `application/plang` tests pass (the plang/this.cs surface is enriched, not removed).
- `application/plang+data` tests either pass against the merged endpoint (with the MIME rewritten in the test) or get retired.
- A round-trip test: `Data.Ok("hello")` written through a channel with `Mime = "application/plang"`, read back, signature populated, name + value preserved.
- A negative test: writing through a channel with no Context throws the documented error from `EnsureSigned`.
