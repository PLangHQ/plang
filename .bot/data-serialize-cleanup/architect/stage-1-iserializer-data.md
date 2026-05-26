# Stage 1: ISerializer Tightened to Data

**Goal:** Tighten `ISerializer` so it only ever sees `Data`, not arbitrary `object?`. This eliminates the polymorphic-input branches in every serializer and lets the channel stop stripping the Data wrapper before serialization.

**Scope:**
- `PLang/app/channels/serializers/serializer/this.cs` — the `ISerializer` interface itself.
- `PLang/app/channels/serializers/serializer/Json.cs` — `application/json` implementation.
- `PLang/app/channels/serializers/serializer/Text.cs` — `text/plain` implementation.
- `PLang/app/channels/serializers/serializer/plang/this.cs` — `application/plang` implementation (interim — Stage 2 will merge it with plang/Data.cs).
- `PLang/app/channels/serializers/serializer/plang/Data.cs` — `application/plang+data` implementation (interim — Stage 2 will delete).
- `PLang/app/channels/channel/stream/this.cs:53-59` — stop passing `data.Value`, pass `data`.
- `SerializeOptions` definition wherever it lives — its `Data` field becomes typed `Data`, not `object?`.

**Out of scope:**
- Merging the two plang serializers (Stage 2).
- Moving `EnsureSigned` from the serializer to the channel (Stage 2).
- Dropping the `Envelope` class (Stage 2).
- Flatten Compress/Decompress (Stage 3).

**Deliverables:**

`ISerializer` interface tightens to:

```csharp
public interface ISerializer {
    string ContentType { get; }
    string FileExtension { get; }
    Task SerializeAsync(Stream stream, Data data, CancellationToken ct = default);
    Task<Data> DeserializeAsync(Stream stream, CancellationToken ct = default);
    string Serialize(Data data);
    Data Deserialize(string s);
}
```

Notes on the change:

- `object? value` → `Data data`. No nullable, no `Type?` parameter.
- The string-overload `Serialize`/`Deserialize` keep their existence but tighten to Data.
- The generic `DeserializeAsync<T>` is dropped — every consumer wants Data; the generic was STJ-shaped, not PLang-shaped.

Each serializer's body shrinks:

- **Json** — emits `data.Value` as JSON (external clients want the value, not the wrapper). Deserialize parses JSON, wraps in `Data.Ok(parsed)`. No `if (value == null)` branch; Data at the boundary is never null.
- **Text** — emits `data.Value.ToString()` (or encoding-aware bytes). Deserialize reads text, wraps in `Data.Ok(text)` typed as string.
- **plang/this.cs** — emits the full Data shape via `app.data.Json` converter + Transport filter (unchanged from today, just stops accepting non-Data input). Stage 2 will merge this with plang/Data.cs and add `EnsureSigned`.
- **plang/Data.cs** — still has its `Envelope` class for now (Stage 2 deletes it). Stops accepting non-Data input. Stage 1 doesn't try to clean this up — that's Stage 2's job.

`Stream.WriteCore` changes from:

```csharp
await Channels!.Serializers.SerializeAsync(new SerializeOptions {
    Stream = Stream,
    Data = data.Value,        // strips the wrapper — wrong
    ContentType = Mime,
    CancellationToken = ct
});
```

to:

```csharp
await Channels!.Serializers.SerializeAsync(new SerializeOptions {
    Stream = Stream,
    Data = data,              // full Data, the serializer decides what to emit
    ContentType = Mime,
    CancellationToken = ct
});
```

`SerializeOptions.Data` typed as `Data`.

**Dependencies:** None.

## Design

The interface change is one atomic edit — touching `ISerializer` forces every implementation to compile against the new shape simultaneously. Coder cannot land this piecewise. Plan for one PR.

**Per-serializer-emits decision.** Each MIME's contract is what its body emits from the Data it receives. The choice "emit the wrapper or just the value" belongs inside the serializer, not in the channel:

- `application/json` says "I'm a JSON view of the value." Strips the wrapper.
- `text/plain` says "I'm the value as text." Strips the wrapper.
- `application/plang` (and the to-be-merged plang+data) says "I'm the Data envelope." Emits the wrapper.

The channel doesn't pre-decide on behalf of the serializer. It hands the full Data; the serializer's identity decides the shape.

**No fallthrough.** Today's serializers have an `if (value == null) return "null"` branch and a `JsonSerializer.Serialize(value, type ?? value.GetType(), ...)` polymorphic catch-all. Both die. A non-Data input to a PLang serializer is a category error; the boundary should throw a structured error, not silently emit "null" or fall through to a generic JSON view. Use `app.errors.ServiceError` with a clear code (`"InvalidSerializerInput"` or similar) so the LLM/test/log can act on it.

**Type-narrowing at the boundary, not deep.** The check happens once at `SerializeAsync` entry. Internal walking (e.g., STJ recursing on `data.Value` which is itself Data) doesn't re-check — STJ's converter resolves by runtime type.

**Compose, don't redeclare, on the JSON side.** Where it makes sense (the two plang serializers in particular), hold a reference to `Json` and delegate, instead of allocating a fresh `JsonSerializerOptions` block. Stage 2 lands this for plang+data. Stage 1 leaves the existing duplication in place — minimum disruption to land the interface change.

**Backwards compatibility — none needed.** Per Ingi: there's no case in PLang where the input to a serializer is genuinely not Data. The polymorphic shape was a System.Text.Json holdover, not a real requirement. If a caller is found doing `serializer.SerializeAsync(stream, someRawObject, ...)`, they need to wrap in `Data.Ok(value)` first — that's the discipline, surface the violation rather than tolerate it.

**Risks:**
- `SerializeOptions` is consumed in places besides `Stream.WriteCore` — coder needs to grep `SerializeOptions` and update every call site. The compiler will flag them, but the audit needs to be thorough.
- The generic `DeserializeAsync<T>` drop may have callers. Each should be replaced with `DeserializeAsync(stream).As<T>()` or similar — Data carries its own `As<T>` mechanism.
- Test fixtures that hand non-Data inputs to serializers will fail. They should be rewritten to wrap in Data first.

**What the coder verifies:**
- Every project compiles.
- Existing test suite for serializers still passes after wrapping inputs in `Data.Ok(...)`.
- A non-Data input to any serializer throws the structured error (new test).
- `Stream.WriteCore` round-trip on a plain `Data.Ok("hello")` through `text/plain` returns "hello" (proves the wrapper-strip-on-emit works per MIME).
