# Stage 2: Wire-View Filter + `Data.Normalize()` + `IWriter` Protocol + `JsonWriter`

> **Note for coder:** every code snippet, type signature, method name, and file path in this file is a **suggestion** that captures architect intent — not a contract. You own the implementation. Reshape, rename, restructure, or replace anything below as the real constraints demand. Push back on the design itself if you find it wrong.

**Goal:** Three pieces land together to make the wire format-agnostic:

1. **Wire-view filter** — `[Out]` becomes the positive whitelist. Properties without `[Out]` are excluded from the wire. Stage 1 tagged the properties; this stage builds the filter that enforces the meaning.
2. **`Data.Normalize()`** — walks the in-memory value at serialize-time into a uniform tree of primitives + `Data` + `List<>`. Consumes the wire-view filter to decide which properties on each domain type become children.
3. **`IWriter` + `JsonWriter`** — `IWriter` is the format encoder protocol; `JsonWriter` is the first implementation. `WireJsonConverter` stays as the entry point and feeds `JsonWriter` the normalized value. A future `ProtobufWriter` ships as a sibling without touching Normalize or any domain type.

`path.JsonConverter.Write` goes away — paths now flow through the Normalize → `[Out]`-filtered property bag pipeline like every other type.

**Scope:**
- New wire-view filter in `PLang/app/channels/serializers/filters/` (probably `Wire.cs` next to `View.cs` and `Transport.cs`). Whitelist semantics: only `[Out]` ships, `[Sensitive]` always excluded, `[Masked]` emits `"****"`.
- New `Data.Normalize()` method (probably a new `this.Normalize.cs` partial under `PLang/app/data/`).
- New `IWriter` interface (alongside the existing serializer plumbing in `PLang/app/channels/serializers/`).
- New `JsonWriter` implementation of `IWriter`.
- Wiring: `WireJsonConverter.Write` invokes `Normalize()` on `data.Value`, then walks the normalized tree through the active `IWriter` to emit bytes. The outer `{name, type, value, properties, signature}` shape `WireJsonConverter` already emits stays the same — only how `data.Value` becomes bytes changes.
- Removal of `path.JsonConverter.Write` (the inbound `Read` may stay if Stage 3 still needs it as a bridge — coder decides).
- Cycle detection (visited-set + max-depth) inside `Normalize`.

**Dependencies:** Stage 1 (`[Out]` tags applied, `RawSignature` deleted) is the floor.

**Out of scope:**
- Rewriting `As<T>` (Stage 3 — for now the reverse direction can stay on STJ; if path's old `Read` is needed as a bridge that's fine, Stage 3 cleans it up).
- A second non-reflection format adapter. `IWriter` is shaped so protobuf/MsgPack can ship later as a sibling implementation without changes to Normalize or any domain type — but the second format itself is a future branch.

**Deliverables:**

1. **Wire-view filter.** New filter in `PLang/app/channels/serializers/filters/` (suggested name `Wire.cs`). Whitelist semantics: a property is included only if it has `[Out]`. `[Sensitive]` always excludes (wins over `[Out]`). `[Masked]` includes the property but Normalize substitutes `"****"` for the value. Debug-mode bypass: when serializing in debug mode, the filter walks every public property except those with `[Sensitive]`; `[Masked]` still emits `"****"` (debug never unmasks). Cache the per-type filtered property list to amortize reflection.
2. **`Data.Normalize()` method.** Probably a new partial at `PLang/app/data/this.Normalize.cs`. Signature: coder picks between `internal void Normalize()` (mutates `Value` in place at serialize-time) or `internal object? Normalize()` (returns a normalized tree). Bounded: visited-set tracks reference-equal objects seen on the current walk; max-depth cap suggested 128 (matches `MaxRehydrationDepth` in `this.Transport.cs`). Cycle or depth violation throws a typed error. Reads the wire-view filter from #1 to decide which properties on each domain type become children.
3. **`IWriter` interface.** Minimal surface: emit primitive (null, bool, int, long, double, string, DateTime, decimal, byte[]), open/close array, emit Data record. Shape it so a future protobuf/MsgPack writer implements the same interface — the second-format implementation isn't on this branch, but the abstraction is the seam that lets it ship later without touching Normalize or any domain type.
4. **`JsonWriter` implementation.** Wraps `Utf8JsonWriter`. Replaces the `JsonSerializer.Serialize(writer, data.Value, options)` call in `WireJsonConverter.Write` (line ~290). Output for primitives matches today; for objects (now property-bagged) the wire shape changes from "type-reflected JSON object" to "list of named Data children" — same as how nested Data already renders.
5. **`WireJsonConverter` wiring.** The outer `{name, type, value, properties, signature}` shape stays. Inside the `value` slot, `WireJsonConverter` calls `Data.Normalize()` once on `data.Value`, then walks the normalized tree through the active `IWriter`. Sign-if-missing, hash-outer carve-out, `MaxReadDepth` — all stay where they are; this stage doesn't touch them.
6. **`path.JsonConverter.Write` removed.** Default reflection + the new wire-view filter take over. Path on the wire is now `{ Scheme, Relative }` (per Stage 1's `[Out]` decisions), not a bare string. The `Read` side may stay as a bridge until Stage 3 lands; coder decides.
7. **Debug-mode wiring.** The wire-view filter takes a mode flag. The pick (per call, per channel, thread-local) is whatever fits the existing channel plumbing — coder owns the wiring.

## Design

The hand-off between Normalize and `IWriter` is the interface that lets a second format ship later without redoing Normalize or touching any domain type. Get it right and protobuf is a new `IWriter` implementation — nothing else changes.

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
2. Ask the wire-view filter for T's properties (whitelist semantics: only `[Out]`, minus `[Sensitive]`; in debug mode all public properties minus `[Sensitive]`). The filter caches per-type.
3. For each included property:
   - If `[Masked]` — emit a child `Data { name = lowercaseName, value = "****" }` (do not call `prop.GetValue`; the whole point is the value never traverses).
   - Otherwise — emit a child `Data { name = lowercaseName, value = Normalize(prop.GetValue(T)) }`.
4. Return a `List<Data>` of the children — assigned to the parent Data's `Value`.

**On the debug toggle:** the wire-view filter takes a mode flag. Default mode: only `[Out]` ships. Debug mode: walks all public properties — but `[Sensitive]` still excludes and `[Masked]` still emits `"****"` (debug never unmasks). How the mode gets selected per call (a channel mode? a per-call argument? a thread-local from the diagnostic context?) is coder's call — pick what fits the existing channel plumbing.

**On `IBooleanResolvable`-style hooks:** if a type wants finer control over how Normalize sees it (analogous to how `IBooleanResolvable` lets a type declare its truthiness), the architect's current position is that no such hook is needed in v1 — the `[Out]`-filtered property bag is rich enough. If you hit a real case during implementation where that doesn't hold, raise it.

**Wire-format break is expected.** Path's old single-string form (`"myPath": "/foo/bar"`) becomes a property-bag form (`"myPath": { "Scheme": "file", "Relative": "/foo/bar" }`). Stored config files, persisted Data, anything serialized by the old path converter will need a one-time migration. That migration is out of scope for this stage — flag what'll need migrating in your handoff so docs / a future migration stage can land it.
