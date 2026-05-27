# Stage 1: ISerializer Input Tightened to Data

**Goal:** Tighten `ISerializer`'s *input* so it only ever sees `Data`, not arbitrary `object?` + nullable `Type?`. This eliminates the polymorphic-input branches in every serializer and lets the channel stop stripping the Data wrapper before serialization.

## What's already done

The `typed-action-returns` merge (`5b1b894c4 coder: Serializers/ISerializer return Data instead of bare T?`) landed the *return* half of this stage:

```csharp
// today's interface, post-typed-action-returns:
Task<data.@this> SerializeAsync(Stream stream, object? value, Type? type = null, ...);
Task<data.@this> DeserializeAsync(Stream stream, Type type, ...);
Task<data.@this<T>> DeserializeAsync<T>(Stream stream, ...);
data.@this<string> Serialize(object? value, Type? type = null);
data.@this Deserialize(string data, Type type);
data.@this<T> Deserialize<T>(string data);
```

Returns are now `Data` / `Data<T>`; parse and serialize failures travel as `Data.Error` (`Success=false`) instead of throwing. Every implementation already wraps its body in `try/catch` over `JsonException`/`NotSupportedException`/`IOException` and returns `Data.FromError(...)`. Call sites (`channels/this.cs ReadChannelAsync`, `channel/stream/this.cs WriteCore`, `types/path/file/this.Operations.cs Save`, `goals/this.cs MergePr`, `modules/settings/Sqlite.cs`) already branch on `.Success` / read `.Value` / propagate `.Error`. **Do not re-do that work.**

What's *not* done — and what this stage now covers — is the input side.

## What's left

**Scope:**
- `PLang/app/channels/serializers/serializer/this.cs` — drop `object? value` + `Type? type = null` from the interface; both become a single `Data data` parameter. Also rename `ContentType` → `Type` and `FileExtension` → `Extension` (OBP: the owner is `serializer`, the qualifier suffixes are redundant).
- `PLang/app/channels/serializers/serializer/Json.cs` — `application/json` implementation.
- `PLang/app/channels/serializers/serializer/Text.cs` — `text/plain` implementation.
- `PLang/app/channels/serializers/serializer/plang/this.cs` — `application/plang` implementation (interim — Stage 2 will merge it with plang/Data.cs).
- `PLang/app/channels/serializers/serializer/plang/Data.cs` — `application/plang+data` implementation (interim — Stage 2 will delete).
- `PLang/app/channels/serializers/this.cs` — the registry. `_byContentType` → `_byType`, `GetByContentType` → `GetByType`, `ContentTypes` enumerable → `Types`, `SerializeOptions.ContentType` → `Type`, same for `DeserializeOptions` / `ResolveOptions`. `SerializeOptions.Data` becomes typed `Data` (not `object?`).
- `PLang/app/channels/this.cs:193` and other call sites populating `options.ContentType` rename accordingly.
- `PLang/app/channels/channel/this.cs:96-110` — rename the abstract hooks: `WriteCore` → `Write`, `ReadCore` → `Read`, `AskCore` → `Ask`. The public orchestrators `WriteAsync` / `ReadAsync` / `AskAsync` keep their names — they wrap the abstract hook with `Before*` / `After*` event firing.
- All channel subclasses (`channel/stream/this.cs`, `channel/goal/this.cs`, `channel/message/this.cs`, `channel/noop/this.cs`, `channel/events/this.cs`, `channel/session/this.cs`) — update the `override` declarations to match the renamed abstract hooks.
- `PLang/app/channels/channel/stream/this.cs:53-59` — in the renamed `Write`, stop passing `data.Value`, pass `data`.

**Out of scope:**
- Merging the two plang serializers (Stage 2).
- Moving `EnsureSigned` from the serializer to the channel (Stage 2).
- Dropping the `Envelope` class (Stage 2).
- Flatten Compress/Decompress (Stage 3).

**Deliverables:**

`ISerializer` interface tightens to:

```csharp
public interface ISerializer {
    string Type { get; }       // MIME type — e.g. "application/plang", "application/json"
    string Extension { get; }  // file extension — e.g. ".plang", ".json"
    Task<Data> SerializeAsync(Stream stream, Data data, CancellationToken ct = default);
    Task<Data> DeserializeAsync(Stream stream, CancellationToken ct = default);
    Task<Data<T>> DeserializeAsync<T>(Stream stream, CancellationToken ct = default);
    Data<string> Serialize(Data data);
    Data Deserialize(string s);
    Data<T> Deserialize<T>(string s);
}
```

OBP naming rationale: a property called `serializer.ContentType` restates context — the owner is `serializer`, of course it's the content type for *something*; the qualifier carries no information. Same for `FileExtension`. PLang already has a `Type` concept on Data (`data.Type` = PLang type-name like "user"/"object"); on a serializer, `Type` reads unambiguously as the MIME type. The two `Type` properties are namespaced by their owner. Same logic applies to the channel side: `WriteCore` / `ReadCore` / `AskCore` lose their `Core` suffix and become `Write` / `Read` / `Ask` — the public orchestrators keep their `Async` suffix to mark them as the entry point with events.

Changes from today's interface (input-side only — return shapes are kept as-merged):

- `object? value` → `Data data`. No nullable, no `Type?` parameter.
- `Deserialize(string, Type)` drops the `Type` parameter. Single-arg `Deserialize(string)` returns `Data`; callers that need a typed view use the generic `Deserialize<T>` or call `.As<T>()` on the result.
- The generic `DeserializeAsync<T>` / `Deserialize<T>` **stay** — `typed-action-returns` ships them as the contracted shape, and they are PLang-shaped now (`Data<T>` not bare `T`). The original v1 plan called for dropping them; the merge superseded that.

Each serializer's body shrinks:

- **Json** — emits `data.Value` as JSON (external clients want the value, not the wrapper). Deserialize parses JSON, wraps in `Data.Ok(parsed)`. No `if (value == null)` branch; Data at the boundary is never null.
- **Text** — emits `data.Value.ToString()` (or encoding-aware bytes). Deserialize reads text, wraps in `Data.Ok(text)` typed as string.
- **plang/this.cs** — emits the full Data shape via `app.data.Json` converter + Transport filter (unchanged from today, just stops accepting non-Data input). Stage 2 will merge this with plang/Data.cs and add `EnsureSigned`.
- **plang/Data.cs** — still has its `Envelope` class for now (Stage 2 deletes it). Stops accepting non-Data input. Stage 1 doesn't try to clean this up — that's Stage 2's job.

`Stream.Write` (renamed from `WriteCore`) changes from:

```csharp
await Channels!.Serializers.SerializeAsync(new SerializeOptions {
    Stream = Stream,
    Data = data.Value,        // strips the wrapper — wrong
    Type = Mime,
    CancellationToken = ct
});
```

to:

```csharp
await Channels!.Serializers.SerializeAsync(new SerializeOptions {
    Stream = Stream,
    Data = data,              // full Data, the serializer decides what to emit
    Type = Mime,
    CancellationToken = ct
});
```

`SerializeOptions.Data` typed as `Data`. `SerializeOptions.ContentType` renamed to `Type` in the same pass.

**Dependencies:** None. (`typed-action-returns` was a prerequisite for the return-side work and is already merged in.)

## Design

The interface change is one atomic edit — touching `ISerializer` forces every implementation to compile against the new shape simultaneously. Coder cannot land this piecewise. Plan for one PR.

**Per-serializer-emits decision.** Each MIME's contract is what its body emits from the Data it receives. The choice "emit the wrapper or just the value" belongs inside the serializer, not in the channel:

- `application/json` says "I'm a JSON view of the value." Strips the wrapper.
- `text/plain` says "I'm the value as text." Strips the wrapper.
- `application/plang` (and the to-be-merged plang+data) says "I'm the Data envelope." Emits the wrapper.

The channel doesn't pre-decide on behalf of the serializer. It hands the full Data; the serializer's identity decides the shape.

**No fallthrough.** Today's serializers have an `if (value == null) return "null"` branch and a `JsonSerializer.Serialize(value, type ?? value.GetType(), ...)` polymorphic catch-all. Both die with the input tightening. A non-Data input is no longer expressible — the compiler stops it. The `null` branch is dead because `Data` is never null at the boundary (callers that have nothing wrap `Data.Ok()` with a null inner value, which the serializer emits naturally).

**Type-narrowing at the boundary, not deep.** The check happens at `SerializeAsync` entry by virtue of the parameter type. Internal walking (e.g., STJ recursing on `data.Value` which is itself Data) doesn't re-check — STJ's converter resolves by runtime type.

**Compose, don't redeclare, on the JSON side.** Where it makes sense (the two plang serializers in particular), hold a reference to `Json` and delegate, instead of allocating a fresh `JsonSerializerOptions` block. Stage 2 lands this for plang+data. Stage 1 leaves the existing duplication in place — minimum disruption to land the interface change.

**Backwards compatibility — none needed.** Per Ingi: there's no case in PLang where the input to a serializer is genuinely not Data. The polymorphic shape was a System.Text.Json holdover, not a real requirement. If a caller is found doing `serializer.SerializeAsync(stream, someRawObject, ...)`, they need to wrap in `Data.Ok(value)` first — that's the discipline, surface the violation rather than tolerate it.

**Risks:**
- `SerializeOptions` is consumed in places besides `Stream.WriteCore` — coder needs to grep `SerializeOptions` and update every call site. The compiler will flag them, but the audit needs to be thorough.
- Test fixtures that hand non-Data inputs to serializers will fail. They should be rewritten to wrap in Data first. Stage 0-4 of `typed-action-returns` already updated ~30 test assertions to read `.Value`; the input-side rewrite is a fresh audit pass.
- The `Deserialize(string, Type)` drop has callers — `modules/settings/Sqlite.cs` was updated to read returns but still passes `Type`. Each call site needs to switch to `Deserialize<T>(string)` or rely on `Deserialize(string).As<T>()`.

**What the coder verifies:**
- Every project compiles.
- Existing test suite for serializers still passes after wrapping inputs in `Data.Ok(...)`.
- The post-`typed-action-returns` failure-path tests (parse error → `Data.Fail`) still pass — the input tightening must not regress error flow.
- `Stream.WriteCore` round-trip on a plain `Data.Ok("hello")` through `text/plain` returns "hello" (proves the wrapper-strip-on-emit works per MIME).
- A non-Data argument at any serializer call site no longer compiles (the contract is enforced by the type system, not by a runtime check).
