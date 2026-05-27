# Test Coverage — data-normalize

> **Note for test-designer:** every row in the matrices below is a **suggestion** of what should be covered, not a contract on test names or organization. You own the suite. Merge rows, split rows, drop rows you find redundant, add rows for cases you spot — push back on the strategy itself if it looks wrong.

The strategy narrative is in [`test-strategy.md`](test-strategy.md). This file is the heavy reference.

## 1. Coverage matrix

One row per behavior. Read top-to-bottom; each row maps to one test (or one row of a parameterized test).

### Stage 1 — `[Out]` discipline + RawSignature cleanup

| Behavior | Layer | Sense |
|----------|-------|-------|
| Identity has `[Out]` on Name, PublicKey only (PrivateKey stays `[Sensitive]`, IsDefault/IsArchived/Created have no `[Out]`) | C# | green |
| path / FilePath / HttpPath: only Scheme + Relative are `[Out]`; Absolute / Raw / derived / Content / Source / GoalCall / Context not `[Out]` | C# | green |
| `list` (modules.list.types): count + value are `[Out]` | C# | green |
| Variable: Name is `[Out]`; RawValue, WasPercentWrapped not | C# | green |
| Data itself: Value, Success, Error, Type, Signature are `[Out]` | C# | green |
| Data.RawSignature property is deleted (reflection check it's gone) | C# | green |
| Signing pipeline still produces verifiable signatures via `Signature` (not RawSignature) | C# | green |
| signing.verify Ed25519 path works through `Signature` directly | C# | green |
| actor/permission code reads `Signature` not `RawSignature` (no compile-time reference to deleted prop) | C# | green |
| StatInfo: Exists, IsFile, Length, Modified are `[Out]` | C# | green |
| GoalCall: Name, Parallel, Parameters, PrPath are `[Out]`; Event / Action stay `[JsonIgnore]` | C# | green |
| permission: Actor, Path, Verb, Match are `[Out]` | C# | green |
| **setting.key is `[Out]`** | C# | green |
| **setting.value is `[Out, Masked]` — name on wire, value replaced with "****"** | C# | green |
| http.Response: Status, Headers, Body are `[Out]`; Duration is not | C# | green |
| Ask.Answer is `[Out]` | C# | green |
| Mock: no `[Out]` properties (type is test-only, never travels in production) | C# | green |
| condition.Operator: Value is `[Out]`; Evaluate (Func) is not | C# | green |

### Stage 2 — Normalize + IWriter + JsonWriter

| Behavior | Layer | Sense |
|----------|-------|-------|
| Data.Normalize() on primitive Value (int, string, bool, etc.) returns unchanged | C# | green |
| Data.Normalize() on `List<int>` keeps homogeneous primitive list | C# | green |
| Data.Normalize() on `Dictionary<string,X>` produces `List<Data>` with keys as names | C# | green |
| Data.Normalize() on a domain object (Identity, path, etc.) emits one Data child per `[Out]` property, lowercased name | C# | green |
| Data.Normalize() omits `[Sensitive]` properties | C# | green |
| Data.Normalize() omits properties without `[Out]` | C# | green |
| **Data.Normalize() emits "****" string in place of `[Masked]` property values** | C# | green |
| **Data.Normalize() emits the masked property's name (not the value)** | C# | green |
| Normalize is idempotent — calling twice on the same Data produces the same tree | C# | green |
| Property-lookup cache populates on first Normalize per type, hits on subsequent calls | C# | green |
| Cycle detection: object referencing itself directly raises typed error | C# | negative |
| Cycle detection: A → B → A indirect cycle raises typed error | C# | negative |
| Cycle detection: depth exceeding max-depth cap raises typed error | C# | negative |
| Cycle detection: legit deep-but-acyclic tree (depth < cap) succeeds | C# | green |
| IWriter.Null emits the format's null token | C# | green |
| IWriter.Bool / Int / Long / Double / String / DateTime / Decimal / Bytes each emit correct format-specific bytes | C# | green |
| IWriter.BeginArray + EndArray bracket an array correctly | C# | green |
| IWriter.BeginRecord + EndRecord bracket a Data record correctly | C# | green |
| JsonWriter output for a Data<path> is the property-bag shape (`{Scheme, Relative}`), not the old single-string shape | C# | green |
| JsonWriter output for Data<Identity> includes Name + PublicKey, excludes PrivateKey | C# | green |
| **JsonWriter output for Data<setting> shows key value but value is "****"** | C# | green |
| Wire serializer entry point calls Normalize before dispatching to IWriter | C# | green |
| path's old JsonConverter.Write is no longer invoked (the new pipeline owns the path serialization) | C# | green |
| Existing wire-serialization tests pass against JsonWriter (subset that survived the path-shape change) | C# | green |
| Round-trip: Data<path> → JsonWriter → bytes → reader → As<path> reconstructs same canonical path | integration | green (Cut 1) |
| Round-trip: Data<Identity> → JsonWriter → bytes → reader → As<Identity> reconstructs Name + PublicKey, PrivateKey is null | integration | green (Cut 1) |
| Goal-level: a .goal that writes a path, serializes, reads it back, uses the path — works end-to-end with the new shape | goal | green |
| Goal-level: signing a Data, serializing, deserializing on receive side, verifying — works after RawSignature removal | goal | green (Cut 4) |

### Stage 3 — As<T> tree-walker

| Behavior | Layer | Sense |
|----------|-------|-------|
| As<int> on Data{Value=42} returns 42 | C# | green |
| As<string> on Data{Value="hi"} returns "hi" | C# | green |
| As<List<int>> on Data{Value=List<int>{1,2,3}} returns the list | C# | green |
| As<Dictionary<string,int>> on a Data{Value=List<Data>[{name="a",value=1}]} returns {"a":1} | C# | green |
| As<Identity> reconstructs from a normalized Data: Name + PublicKey populated, PrivateKey null, IsDefault/IsArchived default | C# | green |
| As<path> reconstructs via path.Resolve(Relative, ctx) — scheme-correct subclass | C# | green |
| As<FilePath> on a Data with Scheme="http" raises typed error (scheme mismatch) | C# | negative |
| As<T> uses the property-lookup cache on second call for same T | C# | green |
| As<T> for a target type with no `[Out]` properties returns an instance with all-default values | C# | green |
| As<T> raises typed error when target type has a required property absent from the normalized tree | C# | negative |
| As<T> raises typed error when target type has a property whose runtime type doesn't match the source value's type | C# | negative |
| Round-trip: every type in wire-out-attributes.md → JsonWriter → reader → As<T> reconstructs semantically equal Data | integration | green (Cut 1) |
| Path's JsonConverter.Read either delegates to As<path> or is deleted (no other inbound JSON path for path) | C# | green |

### Stage 4 — Second format + debug bypass

| Behavior | Layer | Sense |
|----------|-------|-------|
| <Format>Writer.Null emits the format's null token | C# | green |
| <Format>Writer primitive emits match format spec | C# | green |
| <Format>Writer round-trip on Data<primitive> works | C# | green |
| <Format>Writer round-trip on Data<path> reconstructs same path | C# | green |
| <Format>Writer round-trip on Data<Identity> includes Name+PublicKey, excludes PrivateKey | C# | green |
| <Format>Reader reconstructs the same normalized tree shape that Normalize produces | C# | green |
| Cross-format: same domain value through JsonWriter and <Format>Writer round-trip to semantically equal Data | integration | green (Cut 2) |
| Channel/content-type registration: selecting the new MIME routes to the new writer | C# | green |
| Feature flag off: existing channels keep using JSON | C# | green |
| Feature flag on: channel uses the new format | C# | green |
| Debug-mode serializes every public property except `[Sensitive]` and the `settings` type | C# | green (Cut 3) |
| Debug-mode of a Data<Identity> includes IsDefault, IsArchived, Created (which are not `[Out]`) | C# | green (Cut 3) |
| Debug-mode of a Data<Identity> still excludes PrivateKey (`[Sensitive]` always honored) | C# | green (Cut 3) |
| Debug-mode of a Data<setting> still masks value (Masked always honored, even in debug) | C# | green (Cut 3) |
| Debug-mode round-trip via As<T>: works for green-path values (or documented one-way if coder chose) | C# | green |

## 2. Failure matrix

Negative paths and the layer/error they should surface at. Each row asserts the system fails *hard*, *typed*, and *at the right boundary*.

| Failure mode | Detected by | Error type | Layer |
|--------------|-------------|------------|-------|
| Reference cycle (A → A or A → B → A) during Normalize | Visited-set in Normalize walker | typed error (e.g. `CycleDetectedError`) | C# |
| Depth exceeds max-depth cap during Normalize | Depth counter in Normalize walker | typed error (e.g. `MaxDepthExceededError`) | C# |
| `[Out]` applied to a property whose getter throws | Property accessor inside Normalize | the original exception, wrapped with type+property context | C# |
| Type with no parameterless ctor and no reconstruction hook in As<T> | Reconstruction dispatch | typed error (e.g. `NoReconstructionStrategyError`) | C# |
| Scheme mismatch on As<FilePath> from an HTTP-scheme normalized tree | path.Resolve / hook | typed error from path layer | C# |
| Required property missing in normalized tree (target type has non-nullable property, tree has no child of that name) | Reconstruction loop | typed error (e.g. `MissingRequiredPropertyError`) | C# |
| Type mismatch in As<T> (tree child is a string, target property is an int and can't convert) | Property setter | typed conversion error | C# |
| Malformed format bytes (truncated, invalid header) handed to reader | Format-specific reader | format-library error, surfaced as a typed PLang error at the channel boundary | C# |
| `[Sensitive]` property accidentally tagged `[Out]` (mutex violation) | Source-gen warning or runtime assert in Normalize | compile-time warning OR typed runtime error | C# (preferred: compile-time) |
| Settings type with `[Out]` properties tries to ship raw `value` (not masked) | Normalize when encountering setting type — masked attribute enforced | typed error if value escapes unmasked | C# |
| Channel uses an unregistered MIME type | Channel/serializer registry | typed error (e.g. `UnknownContentTypeError`) | C# |

## 3. New surfaces this branch introduces

Inventory of what's NEW. Path + signature where useful. Test-designer writes tests against these without spelunking.

### Interfaces and types

- **`IWriter`** — `PLang/app/channels/serializers/IWriter.cs` (path is a suggestion). Methods per stage-2 design: Null, Bool, Int, Long, Double, String, DateTime, Decimal, Bytes, BeginArray(count), EndArray, BeginRecord(Data), EndRecord. Coder owns the exact signature shape.
- **`JsonWriter : IWriter`** — `PLang/app/channels/serializers/json/JsonWriter.cs` (suggestion). Wraps `Utf8JsonWriter`.
- **`<Format>Writer : IWriter`** — Stage 4 deliverable. Library and exact location are coder's call.
- **`<Format>Reader`** — Stage 4 deliverable. Counterpart to <Format>Writer.

### New attributes (`PLang/app/View.cs`)

- **`[Masked]`** — combines with `[Out]`. When the wire serializer encounters a `[Masked]` property, the property *name* travels but the *value* is replaced with `"****"` (or coder-chosen placeholder, but `"****"` is the canonical PLang stub). Applies in both Out and Debug views. Specifically used on `setting.value`; the architect doesn't currently see other use cases but the attribute is general-purpose.

### New methods on existing types

- **`Data.Normalize()`** — `PLang/app/data/this.cs` or `PLang/app/data/this.Normalize.cs`. Lazy (called by the serializer). Idempotent. Bounded by visited-set + max-depth.
- **`Data.As<T>()`** — already exists; rewritten in Stage 3 to walk the normalized tree instead of delegating to STJ. Optional `Context?` parameter for types that need it during reconstruction (notably path).

### Modified / deleted

- **`Data.RawSignature`** — DELETED. Four call sites migrate to `Signature` directly.
- **`path.JsonConverter.Write`** — DELETED (Stage 2). Default reflection takes over.
- **`path.JsonConverter.Read`** — DELETED or migrated to call As<path> hook (Stage 3 decision).
- **`Data.Value`** contract — narrows at *wire-tree* level (Stage 2). In-memory `Value` keeps `object?` — lazy normalize means no in-memory call site changes.

### New registrations

- A new MIME type registration for the Stage 4 format (e.g. `application/x-protobuf` or `application/msgpack`).
- A feature flag (config key or env var) selecting active wire format per channel.

### Existing surfaces this branch touches by reference

- **`PLang/app/View.cs`** — `[Out]`, `[Sensitive]`, `View.Out`, `View.Debug` already exist; this branch leans on them and adds `[Masked]`.
- **`PLang/app/data/this.cs`** — entry point for Normalize, As<T>; RawSignature lives here today.
- **`PLang/app/data/this.Envelope.cs`** — Signature property + Wrap/Compress/Encrypt pipeline; the Normalize step inserts before / replaces parts of this.
- **`PLang/app/channels/serializers/serializer/`** — the existing serializer chain; entry point gains the Normalize call.
- **`PLang/app/types/path/this.JsonConverter.cs`** — gutted (Stage 2 removes Write; Stage 3 decides Read's fate).
- **`PLang/app/modules/signing/code/Ed25519.cs`** — uses RawSignature today; migrates to Signature.
- **`PLang/app/actor/permission/this.cs`** — uses RawSignature today; migrates to Signature.
- **`PLang/app/channels/serializers/serializer/plang/Data.cs`** — uses RawSignature today; migrates to Signature.
