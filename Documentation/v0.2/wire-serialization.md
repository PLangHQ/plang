# Wire & Serialization

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## [Sensitive] Attribute — Two-Mode Serialization

The `[Sensitive]` attribute (defined in `app/View.cs`) marks properties that contain secret data (e.g., `IdentityData.PrivateKey`). It controls a two-mode serialization split:

- **Output serialization** (the `application/plang` wire serializer + `Data.Transport.Compress`): `Sensitive.Strip` (composed onto the merged serializer's options chain) drops `[Sensitive]` properties. Private keys never leak through channels, API responses, or compressed payloads.
- **Storage serialization** (raw JsonSerializer via DataSource): Filter is NOT applied. Private keys persist in SQLite.
- **Code-level access**: Unaffected. `%MyIdentity.PrivateKey%` in PLang code resolves normally — the attribute only controls serialization.

The filter is always-on — `application/plang` composes it directly onto its STJ options alongside `Transport.ForOutbound` (and `Compress` routes through the same registered serializer). No opt-in required. Any new type with `[Sensitive]` properties is automatically filtered.

---

## Domain types ride the wire as property bags, not bespoke JSON converters

**Rule.** A new C# type that needs to ship through the `application/plang`
wire serializer does **not** get a custom `JsonConverter`. It gets `[Out]`
on each property that should ship, and `Normalize` does the rest — the
type is decomposed into `{name, type, value}` child Datas for each tagged
property, and `json.Writer` lays out the bytes.

**Why:** before `data-normalize` every domain type with a non-default JSON
shape had its own converter (`path` shipped as a bare string via
`path.JsonConverter`, `Identity` had a hand-rolled property list, etc.).
Two converters drifting from each other was a real failure mode. Now the
shape comes from one place — the `[Out]` set on the type — and one walker
fires for every type. If you find yourself reaching for `JsonConverterAttribute`
on a domain type, you are reaching for the smell.

**How to apply.**

1. Tag the properties that should ship: `[Out]` for the wire view, `[Store]`
   for local persistence. Use both when the property crosses both
   boundaries (e.g., `Identity.Name`).
2. Use `[Sensitive]` on properties that must never leave the process
   (e.g., `Identity.PrivateKey`), and `[Masked]` on properties whose
   *existence* is informative but whose value is secret (e.g.,
   `setting.value`).
3. If reconstruction from the property bag needs custom logic (resolving a
   string to a polymorphic subclass, validating ctor preconditions, etc.),
   add a `public static T FromNormalized(Data, Context)` method. The
   `Reconstruct<T>` dispatch picks it up before the generic property-bag
   fallback.
4. **Don't** wrap the type in a parallel "wire shape" record to bypass
   `[JsonIgnore]`. The historical `Envelope` class was the load-bearing
   example of that smell.

**Carve-out:** `path.@this` keeps a `JsonConverter` for the **inbound**
direction (bridging legacy bare-string path JSON), but its outbound path
flows through Normalize like every other type.

**See:** `Documentation/Runtime2/data-spec.md` §16a for the full Normalize
→ IWriter → bytes pipeline.

---

## TransportPropertyFilter — [In] / [Out] Attributes

`[In]` and `[Out]` are serialization view attributes (defined in `app/View.cs`) that control transport-layer property visibility. They work alongside `[JsonIgnore]` to create a three-mode serialization system:

- **Default JSON**: `[JsonIgnore]` properties are hidden (e.g., `Data.Signature`)
- **Inbound transport** (`[In]`): `TransportPropertyFilter.ForInbound` re-includes `[In]` properties during deserialization. Used when parsing `application/plang` responses — `Data.Signature` arrives on the wire and must be deserialized.
- **Outbound transport** (`[Out]`): `TransportPropertyFilter.ForOutbound` re-includes `[Out]` properties during serialization.

**Why this exists:** `Data.Signature` is `[JsonIgnore]` so it doesn't leak into normal JSON output. But for `application/plang` wire protocol, the signature must round-trip. The `[In]` attribute marks it for inbound deserialization; the filter overrides `[JsonIgnore]` selectively.

**Implementation note:** The filter removes any existing hidden entries before re-adding with fresh Get/Set delegates. Simply calling `CreateJsonPropertyInfo` + `Properties.Add` does NOT override `[JsonIgnore]` in System.Text.Json — the hidden entry must be removed first.

---

## `[Sensitive]` masking in ParamSnapshot

When a handler errors, `App.Run` stamps `ICodeGenerated.SnapshotParams()` onto `Error.Params`, which prints to logs/CI artefacts/debug output under "📥 Parameters at dispatch:". Each property contributes a `ParamSnapshot { Name, DeclaredType, PrValue, PrType, FinalValue, WasAccessed }`.

`[Sensitive]` on a `Data<T>` or legacy-scalar property (defined in `app/View.cs`, also used by `SensitivePropertyFilter` for JSON serialization) controls masking in two slots:

| Field | Non-sensitive | Sensitive |
|-------|---------------|-----------|
| `PrValue` | `__pr?.Value` (the raw `.pr` literal — often a `%var%` reference) | `"******"` when the literal is non-null, `null` when absent |
| `FinalValue` | `{set_flag} ? backing : null` | `{set_flag} ? (backing?.Value != null ? "******" : null) : null` |

The null-guard on `FinalValue` (added in v6 nit #3) distinguishes **accessed-and-null** from **accessed-and-redacted**. A sensitive property the handler read but resolved to null reports `FinalValue: null`, not `FinalValue: "******"`. There is no secret to redact in the null case; reporting `"******"` is misleading.

`[Code]` properties are not parameter-sourced — they emit no snapshot entry. Match the convention if you add a new property kind.

**Attribute matching is short-name only.** `Discovery` matches `[Sensitive]` by `AttributeClass.Name == "SensitiveAttribute"` — same convention as `[Code]` (`CodeAttribute`). A different `SensitiveAttribute` declared in another namespace would inadvertently trigger masking. Theoretical only; no current namespace collision in the codebase. If standardisation on fully-qualified attribute matching ever lands, do both at once or you create a different inconsistency.

---

## `Serializers/ISerializer` returns `Data` — no throws

Every `ISerializer` method (`Deserialize<T>`, `DeserializeAsync<T>`, `SerializeAsync`, …) returns `Data` / `Data<T>` rather than throwing. Impls (Json, Text, plang) wrap each method body in try/catch over a **closed list**:

- `System.Text.Json.JsonException`
- `System.NotSupportedException`
- `System.IO.IOException`

…and convert the exception into `Data.FromError`. Anything else (OOM, cancellation) still propagates — by design. If a new serializer impl needs an additional "expected" exception caught, add it to the closed list and surface it as `Data.FromError`; don't introduce a bare `catch (Exception)` that swallows real bugs.

Call sites read `.Success` and `.Value` / `.Error` instead of try/catch around the call. The registry methods pass `Data` through (`Registry.Deserialize<T>` returns `Data<T>`, `Registry.DeserializeAsync<T>` returns `Task<Data<T>>`, `Registry.SerializeAsync` returns `Task<Data>`).

### `Data.Load()` — async pre-materialization at the serialize chokepoint

Lazy reference fundamentals (an `image` minted from a path, with no bytes in memory yet) need their I/O run before the sync renderer wall — `JsonConverter<T>.Write` is sync by the System.Text.Json contract, so nothing below it can `await`. **The load has to happen above the STJ wall**, exactly once, at the serialize chokepoint.

```csharp
// PLang/app/data/this.Load.cs
public async Task<@this?> Load()
{
    var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
    try
    {
        await LoadValue(Value, visited, depth: 0);          // walks the value graph
        return null;                                         // success
    }
    catch (StrictKindMismatchException ex)                   // strict reference fundamental tripped
    {
        return FromError(new Error(ex.Message, "StrictKindMismatch", 400) { Exception = ex });
    }
}

// PLang/app/data/ILoadable.cs
public interface ILoadable { Task LoadAsync(); }             // image.LoadAsync => BytesAsync
```

**Walks the same shapes `Normalize` does.** `LoadValue` recurses nested `Data`, dictionaries, lists/arrays — same cycle guard (`ReferenceEqualityComparer`), same `MaxNormalizeDepth` cap. Tree-native leaves (string / `byte[]` / `ValueType` / null) carry no lazy content and short-circuit. The walk is plain — no reflection, no domain-object descent (reference fundamentals always arrive as the value itself or inside a dict/list).

**Distinct from `IStrictKindEnforcer`.** A lazy reference fundamental needs loading whether or not it is strict — an unloaded handle renders empty regardless. Strict enforcement piggybacks because the load seam (`image.BytesAsync`) is also where `StrictKindMismatchException` throws. `image.@this` implements both: `LoadAsync` is just `=> BytesAsync()`, which does the work and runs the strict check.

**Idempotent and cheap.** An already-loaded image returns from its load seam immediately; a graph with no `ILoadable` is a pure walk and allocates only the visited set.

**Call site — first line inside the `try` of each `ISerializer.SerializeAsync`** (`plang/this.cs`, `Json.cs`). `Text.cs` delegates non-primitives to the Json fallback, so all three impls are covered through one of two files. The leaf impl is the un-bypassable chokepoint: both channel output and wire egress (`ContextLessFallback` → `plang.@this`) pass through it.

**Strict mismatch surfaces before any bytes write.** The error returned from `Load()` (key `StrictKindMismatch`) becomes the serializer's `Data.FromError` return; the stream stays untouched (0 bytes written). Read failures (missing file, denied path) propagate as `IOException` to the serializer's existing catch (see the closed list above).

**Why this lives on Data, not on the serializer.** The walk is over Data's own value graph (nested Data envelopes, dictionaries, lists) and the courier rule still holds — `Load()` doesn't read `.Value` to *interpret* it, only to `await` registered `ILoadable.LoadAsync` markers and recurse. Putting it on `ISerializer` would either duplicate the graph walk per impl or invent a separate "pre-pass coordinator" type to hold it.

**Sync `.Bytes` and `Width`/`Height` are still sync by design.** They run below the load pass; by the time they read, the bytes are in memory. Off-the-serialize-wall callers (action handlers in async context that read `image.Width` directly) are a separate concern — those can `await BytesAsync()` at their own call sites if they need to. The flagged-but-not-folded follow-up is captured in `.bot/type-kind-strict/coder/v14/report.md`.

### http body dispatch through the registry

`http.request` / `http.upload` return `Task<Data<app.http.Response.@this>>`. The `Response` record is `(int Status, Dictionary<string,string> Headers, object? Body, TimeSpan Duration)`; `Body` is dispatched by Content-Type via `Serializers.GetByType` + a `TextFallback` for text-shaped misses (`text/*`, `application/xml`, `application/json`, `text/csv`). Binary content-types and missing Content-Type fall back to `byte[]`.

Legacy properties (`%response.StatusCode%`, `%response.Body%` as raw string) remain reachable via `Response.BuildProperties` so existing PLang code keeps working alongside the new `%response.Status%` / typed `%response.Body%`.

## `@schema:"data"` marker — Data self-identifies on the wire

Every `Data` written through `Wire.Write` carries `{"@schema":"data",...}` as its
first key. This is the **one canonical recognizer** — `Wire.HasDataMarker`,
`@this.IsDataMarked`, and `LiftDataIfShaped`/`LiftArrayElements` all key off
the same shape. No name+value+type shape-sniffing: a user map that happens to
have `name`/`value`/`type` keys but no marker deserializes as a plain dict,
unambiguously.

**Written by two paths:**
- `Wire.Write` (the `application/plang` serializer): `writer.WriteString(@this.WireSchema, @this.WireSchemaData)` is the first key of every Data object on the wire.
- The json.Writer's list arm: each element of a Data-typed list is self-described with the marker so a full round-trip through a list restores the element's type and signature.

**Read by three paths:**
- `LiftDataIfShaped(element)` — value-slot object recognition: an object carries the marker → lift to `Data`; no marker → plain dict.
- `LiftArrayElements(array)` — array arm: each element carrying the marker lifts to a `Data` (regaining its Signature); anything else wraps as a bare-element Data.
- `@this.IsDataMarked` — used outside the wire (e.g., `data.Normalize`) to distinguish a value that was already encoded as Data.

**Depth-capped.** `Wire.Read` stops at `MaxReadDepth = 64`; a marker-bombed
deep payload throws `JsonException` rather than stack-overflowing.

**The `name` key is excluded from signing.** `name` is a binding label (which
variable holds this value), not part of the value's identity. A Data signed as
`%x%` verifies the same when later held as `%y%`. The `@schema:data` marker
itself IS inside the signed region (it's written before the name field is
emitted) — changing the marker string would break all existing signatures.

## Wire passthrough — `RawUntouched` / `EmitRawVerbatim`

A `Data` whose `_raw` is set and whose value has never been touched serializes its raw source form back out **byte-identical**. Couriers (variable memory, callstack, channel routing, signing) cannot force a parse mid-flight — the OBP courier rule (only leaves touch `.Value`) holds by construction. See [lazy materialization](data-internals.md#lazy-materialization--_raw-materialize-forcematerialize) for the read half.

```csharp
internal bool RawUntouched => _raw != null && _value == null && _valueFactory == null;
```

**`Wire.Write` skips the inner walk when `RawUntouched`.** A relayed Data passes through as its raw form — no `Normalize` walk, no `[Out]` projection, no parse-then-reserialize. The wire emits the slot via `EmitRawVerbatim`:

```csharp
private static void EmitRawVerbatim(Utf8JsonWriter writer, @this data)
{
    var raw = data.Raw;
    if (raw is byte[] bytes) { writer.WriteBase64StringValue(bytes); return; }
    if (raw is string s)
    {
        var t = data.Type;
        bool isJson = (t.Name == "object" && string.Equals(t.Kind, "json", StringComparison.OrdinalIgnoreCase))
                      || t.Name == "number";
        if (isJson) writer.WriteRawValue(s);   // byte-identical raw JSON / number literal
        else        writer.WriteStringValue(s); // text/csv/xml/yaml → json string
    }
}
```

Raw json and number literals already *are* json, so `WriteRawValue` reproduces them exactly. Other text shapes ride as json strings; raw bytes ride as base64.

**`Wire.Read` defers shape-typed value slots.** When the type slot is present and the type is `object` or `table` with a non-empty `kind` (`IsDeferrableShape`), the value slot's raw json is captured into `_raw` (the string value when it's a json string, else the raw token text via `GetRawText()`), and the Data is built via `FromRaw`. Eager `Deserialize<object?>` stays for untyped slots — there's no `(type, kind)` to materialize toward, and an untyped primitive/list/dict re-serializes stably.

```csharp
case "value":
    if (typeRef != null && IsDeferrableShape(typeRef))
    {
        using var vdoc = JsonDocument.ParseValue(ref reader);
        var el = vdoc.RootElement;
        deferredRaw = el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? ""
            : el.GetRawText();
    }
    else if (reader.TokenType == JsonTokenType.StartObject)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        value = LiftDataIfShaped(doc.RootElement, options);  // nested-Data envelope recognition
    }
    else
    {
        value = JsonSerializer.Deserialize<object?>(ref reader, options);
    }
    break;
```

**Nested Data still rehydrates via `LiftDataIfShaped`.** A nested Data carried in a value slot has **no type slot** (Data's own PLang name is `object`, and `json.Writer` only emits a type slot when `!Type.IsNull`). The eager-untyped path recognises the canonical `{name, value, [signature]}` envelope and reconstructs via the Wire reader recursively. This is the Wire serializer's job as a leaf, not a courier reaching into `.Value`. Without it, a nested Data degrades into a dict and its inner `Signature` becomes observable as a sub-dictionary that never reaches `signing.verify`. A fully type-driven endgame (add a `data` PLang type, stamp nested Data on write, reconstruct purely via `Readers.Of("data", …)`) is captured in `Documentation/Runtime2/todos.md`.

### Signing recanonicalizes — lazy does not change it

Signing re-serializes deterministically through `Signature.ToSigningBytes` (`SigningOptions`, `Signature` excluded, ordered) — it never compares raw arrival bytes. So **verify on a signed Data materializes its value** — a legitimate touch, unchanged from pre-lazy. Do **not** rewire signing to read `_raw`; the round-trip is invariant on canonical sender output, not on byte-for-byte arrival form.

The hash round-trip is byte-identical for a relayed `RawUntouched` Data because:

1. `Wire.Write` emits keys in fixed order: `name` (suppressed in outer-hash via `MarkOuterForHash`), `type`, `value`, `properties`, `signature` (also suppressed in outer-hash).
2. The `type` entity converter (`app/type/this.json.cs`) writes a fixed key order: `name`, then `kind?`, then `strict?`.
3. `Properties` iterates a `Dictionary<string, object?>` whose .NET enumeration is insertion order — deterministic per-sender.
4. `EmitRawVerbatim` preserves bytes exactly for `object/json` and number literals; round-trips through STJ canonical string-encoding for other shapes (symmetric under canonical sender/receiver).
5. The hash carve-out (`MarkOuterForHash` ref-counted on `ReferenceEqualityComparer` keys) composes correctly under nested Hash calls.

A sender that produces non-canonical bytes (third-party, non-STJ writer, permuted keys) fails verify on the receiver — conservative-correct. A wire-attached `type.kind` cannot redirect bytes to a different parser without invalidating the signature: the `type` slot is in the signed scope.

**`Wire.Read` does not auto-verify.** A reconstructed Data carries its `Signature` populated-but-unverified. Verification is the consumer's explicit step (`signing.verify`, or a `BeforeRead` event binding). Parity with the prior eager-read behavior — not a regression. Full security audit in `.bot/lazy-deserialize/security/v1/result.md`.

## Multi-segment serializer extension matching

`Serializers.GetByExtension` walks **multi-segment** extensions before falling back to the trailing segment. `report.junit.xml` first probes `junit.xml`; if no serializer is registered there, it falls back to `xml`. This lets a future `JunitSerializer` register against the multi-segment stem without colliding with the generic XML serializer.

`path.Extension` (`PathHelper.GetExtension`) returns the extension **without** the leading dot — `"csv"` not `".csv"`. Callers that need the dot prefix it themselves; `Formats.Mime` normalises it back on when needed.
