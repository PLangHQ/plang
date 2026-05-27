# Stage 2: `Data.Normalize()` + `IWriter` Protocol + `JsonWriter`

> **Note for coder:** every code snippet, type signature, method name, and file path in this file is a **suggestion** that captures architect intent — not a contract. You own the implementation. Reshape, rename, restructure, or replace anything below as the real constraints demand. Push back on the design itself if you find it wrong.

**Goal:** The first complete pipeline. `Data.Normalize()` walks the in-memory value at serialize-time into a uniform tree of primitives + `Data` + `List<>`. An `IWriter` protocol abstracts the format encoder. A `JsonWriter` implementation produces the wire output. `path.JsonConverter` is replaced — paths now flow through the same Normalize → `[Out]`-filtered property bag pipeline as every other type.

**Scope:**
- New method `Data.Normalize()` (probably on `PLang/app/data/this.cs` or a new `this.Normalize.cs` partial).
- New interface `IWriter` (in `PLang/app/channels/serializers/` or wherever feels right alongside the existing serializer plumbing).
- New `JsonWriter` implementation of `IWriter`.
- Wiring: the existing wire serializer entry point invokes `Normalize()` once, then hands the normalized tree to the active `IWriter`.
- Removal of `PLang/app/types/path/this.JsonConverter.cs` (or its `Write` method — depends on whether the converter still serves a role on the inbound side; if not, delete the whole file).
- Cycle detection (visited-set + max-depth) inside `Normalize`.

**Dependencies:** Stage 1 (`[Out]` discipline applied, `RawSignature` deleted) is the floor.

**Out of scope:**
- Rewriting `As<T>` (Stage 3 — for now, the reverse direction can stay on STJ if path is the only type that breaks, and a temporary bridge in path's `JsonConverter.Read` is acceptable).
- A second non-reflection format adapter (deferred — `IWriter` should be shaped to accept one without changes to Normalize or any domain type, but actually shipping protobuf/MsgPack isn't part of this branch).

**Deliverables:**

1. **`Data.Normalize()` method.** Signature roughly `internal void Normalize()` (mutates `Value` in place at serialize-time) or `internal object? Normalize()` (returns the normalized tree without mutating) — coder's call on which fits the existing call sites better. Bounded: visited-set tracks reference-equal objects seen on the current walk; max-depth cap (suggested 128, matches existing `MaxRehydrationDepth` in `this.Envelope.cs:228`). Cycle or depth violation → throws a typed error (`InvalidOperationException` or a new error type — coder's call).
2. **`IWriter` interface.** Minimal surface: emit primitive (null, bool, int, long, double, string, DateTime, decimal, byte[]), open/close array, emit Data record. Shape it so a future non-reflection format (protobuf, MsgPack) could implement the same interface without format-specific concepts leaking back into Normalize — the second-format implementation isn't on this branch, but the abstraction should accept one.
3. **`JsonWriter` implementation.** Wraps `Utf8JsonWriter`. Output should match today's JSON shape *as closely as possible* — but where the new wire shape diverges (path is now a property bag, not a string), document the divergence in the PR / commit. Don't twist the design to preserve old shapes; the wire-format break is on the table for this branch.
4. **path's `JsonConverter.Write` removed.** Default reflection + `[Out]` filter takes over. Path on the wire is now `{ Scheme, Relative }` (per stage 1's `[Out]` decisions), not a bare string.
5. **The wire serializer entry point** (in `PLang/app/channels/serializers/serializer/`) calls `Data.Normalize()` once, then dispatches to the active `IWriter`. The existing serializer chain (Wrap → Compress → Encrypt, per `this.Envelope.cs`) wraps around this.
6. **Debug-mode bypass.** The serializer takes a `View` parameter — `View.Out` filters by `[Out]`, `View.Debug` walks all public properties. `[Sensitive]` is excluded in both. `[Masked]` still emits `"****"` in both. The View pick is determined by however the channel signals diagnostic mode today; coder owns the wiring.

## Design

The hand-off between Normalize and IWriter is the load-bearing interface. Get it right and a future non-reflection format slots in as a new `IWriter` implementation with zero changes to Normalize or any domain type; get it wrong and every format has to reinvent.

Suggested shape for `IWriter` (pure intent — coder owns the actual signatures):

```csharp
public interface IWriter {
    void Null();
    void Bool(bool v);
    void Int(int v);
    void Long(long v);
    void Double(double v);
    void String(string v);
    void DateTime(DateTime v);
    void Decimal(decimal v);
    void Bytes(byte[] v);
    void BeginArray(int count);  // count may be -1 if unknown — protobuf cares, JSON doesn't
    void EndArray();
    void BeginRecord(Data record);  // emits {name, type, value, signature} shape
    void EndRecord();
}
```

`Data.Normalize()` produces values whose runtime type is one of: `string`, `int`, `long`, `double`, `bool`, `DateTime`, `decimal`, `byte[]`, `null`, `Data`, or `List<>` of any of the above (homogeneous lists stay typed; heterogeneous lists become `List<Data>`). The encoder is a five-case-ish dispatch over those types — never reflects.

**The Normalize walk** for a non-primitive object T:
1. If T is null / primitive / DateTime / decimal / byte[] / Data / IList — already normalized, return.
2. Look up T's public properties tagged `[Out]` (and not `[Sensitive]`). Cache the property list per type to amortize the reflection cost (similar pattern to STJ's type-info cache).
3. For each `[Out]` property:
   - If the property is also tagged `[Masked]` — emit a child `Data { name = lowercaseName, value = "****" }` (do not call `prop.GetValue`; the whole point is the value never traverses).
   - Otherwise — emit a child `Data { name = lowercaseName, value = Normalize(prop.GetValue(T)) }`.
4. Return a `List<Data>` of the children — assigned to the parent Data's `Value`.

**On the debug toggle:** the wire serializer takes a `View` parameter (`View.Out` vs `View.Debug`). Debug bypasses the `[Out]` filter and walks all public properties — but `[Sensitive]` and `[Masked]` still apply (Sensitive excludes; Masked still emits `"****"`). How the View gets selected per call (a channel mode? a per-call argument? a thread-local from the diagnostic context?) is coder's call — pick what fits the existing channel plumbing.

**On `IBooleanResolvable`-style hooks:** if a type wants finer control over how Normalize sees it (analogous to how `IBooleanResolvable` lets a type declare its truthiness), the architect's current position is that no such hook is needed in v1 — the `[Out]`-filtered property bag is rich enough. If you hit a real case during implementation where that doesn't hold, raise it.

**Wire-format break is expected.** Path's old single-string form (`"myPath": "/foo/bar"`) becomes a property-bag form (`"myPath": { "Scheme": "file", "Relative": "/foo/bar" }`). Stored config files, persisted Data, anything serialized by the old path converter will need a one-time migration. That migration is out of scope for this stage — flag what'll need migrating in your handoff so docs / a future migration stage can land it.
