# Test Coverage Reference

Heavy reference. Test-designer reads top-to-bottom and writes one test per row. Three sections:

1. [Coverage matrix](#coverage-matrix) — per-stage behaviours, mapped to layer + sense.
2. [Failure matrix](#failure-matrix) — consolidated negative paths.
3. [New surfaces this branch introduces](#new-surfaces-this-branch-introduces) — types, methods, renames.

Cross-references from [test-strategy.md](test-strategy.md). The four integration cuts there are the green-path contract; everything beneath is here.

## Coverage matrix

### Stage 1 — ISerializer input tightening + OBP renames

| # | Behavior | Layer | Sense |
|---|---|---|---|
| 1.1 | `ISerializer.SerializeAsync(Stream, Data, ct)` accepts a `Data` argument and returns `Task<Data>` | C# | green |
| 1.2 | `ISerializer.SerializeAsync(Stream, object, …)` no longer exists — calling a method that takes `object?` fails to compile | C# | negative |
| 1.3 | `serializer.Type` returns the MIME string (e.g. "application/plang", "application/json", "text/plain") | C# | green |
| 1.4 | `serializer.Extension` returns the file extension with leading dot (e.g. ".plang") | C# | green |
| 1.5 | `Serializers.GetByType("application/plang")` returns the plang serializer; previous `GetByContentType` no longer exists | C# | green |
| 1.6 | `Serializers.Types` enumerable lists registered MIME strings; previous `ContentTypes` no longer exists | C# | green |
| 1.7 | `SerializeOptions.Type` (was `ContentType`) compiles in usage | C# | green |
| 1.8 | `SerializeOptions.Data` is typed `Data` (not `object?`) | C# | green |
| 1.9 | `channel.Write` (was `WriteCore`) is the abstract hook; base `WriteAsync` calls it after `FireBefore` and before `FireAfter` | C# | green |
| 1.10 | Each channel subclass (stream, goal, message, noop, events, session) overrides `Write`/`Read`/`Ask`; old `*Core` names no longer exist | C# | green |
| 1.11 | `Stream.Write` (renamed) passes the full `Data` to `Serializers.SerializeAsync` (not `data.Value`) | C# | green |
| 1.12 | Round-trip `Data.Ok("hello")` through `text/plain` returns "hello" — serializer's per-MIME strip-or-keep decision still works post-tightening | C# | green |
| 1.13 | Round-trip `Data.Ok("hello")` through `application/json` returns "hello" with the wrapper stripped (external client sees just the value) | C# | green |

### Stage 2 — Merge plang serializers + sign-in-converter + canonicalization fix

| # | Behavior | Layer | Sense |
|---|---|---|---|
| 2.1 | `application/plang+data` MIME is no longer registered; `Serializers.GetByType("application/plang+data")` returns null | C# | green |
| 2.2 | `application/plang` registers the single merged serializer; `.plang` file extension still resolves | C# | green |
| 2.3 | The `Envelope` class and `FromEnvelope` factory are deleted (the file `serializer/plang/Data.cs` no longer exists) | C# | green |
| 2.4 | `Json.WithConverter(JsonConverter)` returns a new `Json` instance with the converter added to options | C# | green |
| 2.5 | `Json.WithModifier(Action<JsonTypeInfo>)` returns a new `Json` instance with the modifier added to the resolver | C# | green |
| 2.6 | The wire converter, invoked on a Data with `Signature == null`, calls `EnsureSigned()` and emits the populated signature on the wire | C# | green |
| 2.7 | The wire converter, invoked on a Data with `Signature != null`, leaves the signature unchanged (sign-if-missing is idempotent — second call is no-op) | C# | green |
| 2.8 | A Data with a `List<Data>` inside `Value` round-trips with each list element carrying its own signature | C# | green |
| 2.9 | Cut 1 — plain round-trip with implicit signing | integration | green |
| 2.10 | Cut 3 — multi-actor forwarding chain | integration | green |
| 2.11 | `crypto/Default.cs:Hash` canonicalizes through the same `Transport.ForOutbound` options the wire serializer uses (test: hash of a Data ≡ wire-serializer bytes minus the outermost Signature field) | C# | green |
| 2.12 | Outer signature binds inner signature for structurally-nested Data — mutating inner `signature` in the wire JSON fails outer verification | C# | green |
| 2.13 | Channel `BeforeWrite` event handlers run before the converter signs; a handler that mutates the Data has its mutations included in the signature canonicalization | C# | green |
| 2.14 | Reading via `application/plang` populates `Data.Signature` without auto-verifying (verification is the explicit `signing.verify` step) | C# | green |

### Stage 3 — Flatten Compress / Decompress

| # | Behavior | Layer | Sense |
|---|---|---|---|
| 3.1 | `Compress(D1)` produces a Data with `type == "archived"`, `value` as `byte[]`, no nested gzip Data | C# | green |
| 3.2 | `Decompress(D2)` returns a Data equal to the pre-Compress D1 in name + value (Properties also preserved after Stage 4 lands) | C# | green |
| 3.3 | Compressed bytes, when gunzipped, deserialize to a valid `application/plang` document with a populated `signature` field | C# | green |
| 3.4 | Cut 2 — sign-then-compress preserves the inner attestation | integration | green |
| 3.5 | A non-compressible `Type` (e.g. one without `.Compressible`) returns self unchanged from `Compress()` | C# | green |
| 3.6 | The direct `JsonSerializer.SerializeToUtf8Bytes` call in `this.Envelope.cs` is gone; Compress routes through `Serializers.Get("application/plang")` instead | C# | green |
| 3.7 | `_envelopeJsonOptions` field is deleted from `this.Envelope.cs` (no duplicate STJ-options block remaining) | C# | green |
| 3.8 | `RehydrateNestedData` is gone — the wire converter handles nested-Data reconstruction natively | C# | green |
| 3.9 | A `.goal` test exercising `set %compressed% to compress %user%` and `set %restored% to decompress %compressed%` round-trips a multi-field user record losslessly | goal | green |

### Stage 4 — Properties get a wire scope

| # | Behavior | Layer | Sense |
|---|---|---|---|
| 4.1 | `Properties` is `IDictionary<string, object?>` (or a `Dictionary`-backed wrapper). Indexed by string key. | C# | green |
| 4.2 | `Properties[key] = value` for each supported primitive type (string, int, long, double, bool, DateTime, byte[]) round-trips through `application/plang` | C# | green |
| 4.3 | `Properties[key] = dict` where `dict` is `IDictionary<string, object?>` of primitives — round-trips | C# | green |
| 4.4 | `Properties[key] = list` where `list` is a list of primitives — round-trips | C# | green |
| 4.5 | Wire JSON of a Data with `Properties["cost"] = 100`: `properties: { "cost": 100 }` as a sibling of name/type/value/signature. No top-level `cost` field. | C# | green |
| 4.6 | Wire JSON of a Data with empty Properties: the `properties` field is omitted entirely | C# | green |
| 4.7 | A Property keyed `"value"`, `"name"`, `"signature"`, or any other previously-reserved word round-trips intact (keys are unconstrained inside the nested object) | C# | green |
| 4.8 | Round-trip preserves JSON-promotion semantics — `Properties["n"] = 42` (int) reads back as `long` 42 (note: same as JsonElement.GetInt64 contract) | C# | green |
| 4.9 | Cut 4 — Properties wire shape + navigation | integration | green |
| 4.10 | Variable parser: `%response!cost%` resolves to `Properties["cost"]` | goal | green |
| 4.11 | Variable parser: `%response.text%` (Value-side) and `%response!text%` (Properties-side) resolve to different stores when both populated | goal | green |
| 4.12 | Variable parser: `%response!cost.input%` reads `Properties["cost"]["input"]` when cost is a dict | goal | green |
| 4.13 | Variable parser: `%!flag%` (boolean negation prefix) still parses as negation, distinct from `%x!key%` (Properties dereference) | goal | green |
| 4.14 | `%response%` (no operator) renders Value, not the whole Data — Properties don't bleed into the variable's primary render | goal | green |
| 4.15 | Mutating any byte inside the wire `properties` object (re-encode without re-signing) fails outer signature verification | C# | green |
| 4.16 | Properties walk is skipped by the sign-if-missing converter — Properties values are primitives, not Data; no inner signatures appear under `properties` | C# | green |
| 4.17 | Unknown top-level field on read (e.g. `traceId: "..."` outside the five reserved) is silently ignored; receiver Data has the five reserved fields and an empty Properties (the unknown is dropped, not captured) | C# | green |
| 4.18 | The C# `Properties` type signals "the existing IList<Data> public surface no longer compiles" — old callers using index-by-int fail to build | C# | green |

### Stage 5 — Vocabulary sweep

| # | Behavior | Layer | Sense |
|---|---|---|---|
| 5.1 | `PLang/app/data/this.Transport.cs` exists; `this.Envelope.cs` does not | C# | green |
| 5.2 | `git grep -i envelope` in `PLang/**/*.cs` returns no matches (or only intentional historical refs flagged in scope) | C# | green |
| 5.3 | `Signature` docstring no longer says "data envelope"; reads "cryptographic signature attached to a Data" | C# | green |
| 5.4 | Local variables named `envelope` in Wrap/Compress are renamed to `outer` or context-appropriate | C# | green |
| 5.5 | All projects build clean after the rename pass | C# | green |
| 5.6 | All existing tests pass after the rename pass (pure vocabulary, no behaviour change) | C# | green |

## Failure matrix

Consolidated negative paths. Each row is a way the system *should* fail; the test asserts the failure is hard, typed, and at the right layer.

| Failure mode | Detected by | Error type / signal | Layer |
|---|---|---|---|
| Non-Data input to `ISerializer.SerializeAsync` | C# compiler | Type-system rejection (CS error) | C# |
| `SerializeOptions.Data = someRawObject` (object? assignment) | C# compiler | Type-system rejection | C# |
| Old `serializer.ContentType` / `serializer.FileExtension` access in callsite | C# compiler | CS0117 "no definition for" | C# |
| Old `channel.WriteCore` / `Serializers.GetByContentType` access | C# compiler | CS0117 / CS1061 | C# |
| `EnsureSigned()` called on a Data with no Context | `data.@this.EnsureSigned` | `InvalidOperationException` with message about Context wiring | C# |
| `Properties[key] = value` where value type is unsupported (e.g. a Data instance) | `Properties.set` | `ArgumentException` "not a wire-supported primitive" | C# |
| Outer signature verify after wire-byte tampering | `signing.verify` (`Ed25519.VerifyAsync`) | `Data<bool>.FromError(DataHashMismatch)` | C# |
| Outer signature verify after inner-signature tampering (structurally-nested case) | `signing.verify` | `DataHashMismatch` (Stage 2 canonicalization fix is what makes this fail correctly) | C# |
| Outer signature verify after `properties.{key}` tampering | `signing.verify` | `DataHashMismatch` | C# |
| Reading a wire JSON that is not a Data shape (random JSON object missing reserved fields) | wire converter Read | `JsonException` "Unterminated Data object" or default-init Data | C# |
| `Decompress()` on a Data whose Type is not "archived" | `Data.Decompress` | no-op (returns self) — *not* an error | C# |
| `Decompress()` on an archived Data whose value is not byte[] | `Data.Decompress` | `Data.FromError(DecompressError "no byte[] value")` | C# |
| `Compress()` on a non-compressible Type | `Data.Compress` | no-op (returns self) — *not* an error | C# |
| Hash with an unsupported algorithm string | `crypto/Default.Hash` | `Data<byte[]>.FromError(ActionError "UnsupportedAlgorithm")` | C# |
| Write through a channel with `Direction == Input` (read-only) | `channel.Write` override | `Data.FromError(ServiceError "ChannelReadOnly")` | C# |
| Read from a channel with `Direction == Output` (write-only) | `channel.Read` override | `Data.FromError(ServiceError "ChannelWriteOnly")` | C# |
| `Ask` on a channel with no interactive answerer (closed pipe) | `channel.Ask` override | `Data.FromError(ServiceError "ChannelEof")` | C# |
| Variable parser sees `%x!!key%` (double bang) | parser | parse error, surfaced as a goal-compile error | goal |
| Variable parser sees `%!flag%` (negation, no identifier) | parser | parses as negation, not as Properties — different AST path | goal |

## New surfaces this branch introduces

Inventory for test-designer (and coder) — types, methods, properties, renames, registrations. Path + signature where useful.

### Interfaces and types

- `PLang/app/channels/serializers/serializer/this.cs` — `interface ISerializer` (input-tightened; methods take `Data`, return `Task<Data>` / `Data<T>`).
- `PLang/app/data/Properties.cs` — `class Properties : IDictionary<string, object?>` (rewritten from `IList<Data>`).
- `PLang/app/data/WireJsonConverter.cs` (or wherever the wire converter lives) — `JsonConverter<Data>` that walks the wire shape, calls `EnsureSigned` on each Data, emits/parses the five reserved fields, ignores unknown top-level fields.

### New methods on existing types

- `Serializers.GetByType(string mime)` — replaces `GetByContentType`.
- `Json.WithConverter(JsonConverter)` — returns a new Json with the converter added.
- `Json.WithModifier(Action<JsonTypeInfo>)` — returns a new Json with the modifier added.
- `Json.ForInbound()` — `WithModifier(Transport.ForInbound)`; symmetric to existing `ForView`.

### Renamed methods / properties (OBP suffix drop)

- `ISerializer.ContentType` → `Type`
- `ISerializer.FileExtension` → `Extension`
- (Same renames on every concrete serializer: `Json.cs`, `Text.cs`, `plang/this.cs`.)
- `Serializers.GetByContentType` → `GetByType`
- `Serializers._byContentType` → `_byType`
- `Serializers.ContentTypes` → `Types`
- `SerializeOptions.ContentType` → `Type`
- `DeserializeOptions.ContentType` → `Type`
- `ResolveOptions.ContentType` → `Type`
- `channel.@this.WriteCore` (abstract) → `Write`
- `channel.@this.ReadCore` (abstract) → `Read`
- `channel.@this.AskCore` (abstract) → `Ask`
- (Override declarations updated in all 6 subclasses: stream, goal, message, noop, events, session.)
- `PLang/app/data/this.Envelope.cs` → `this.Transport.cs` (Stage 5)

### Removed surfaces

- `serializer/plang/Data.cs` — file deleted.
- `serializer/plang/Data.Envelope` and `serializer/plang/Data.FromEnvelope` — deleted with the file.
- `application/plang+data` MIME registration in `Serializers.this.cs` — removed.
- `.pdata` file-extension binding — removed.
- `_envelopeJsonOptions` field in `this.Envelope.cs` — deleted.
- `RehydrateNestedData` in `this.Envelope.cs` — deleted.

### New PLang variable syntax

- `%x!key%` — Properties dereference. Lexer addition; positionally distinct from the negation prefix `%!flag%`.
- `%x!key.path%` — chained navigation: Properties dereference, then dot-navigate into the dereferenced value.

### Existing surfaces this branch touches by reference

- `data.@this.EnsureSigned()` — called from the wire converter during walk. Behaviour unchanged from today; semantics are now "the wire path uses this, not callers."
- `data.@this.Signature` — still `[JsonIgnore] [In] [Out]`; the merged plang serializer's Transport filter re-includes it on the wire path.
- `data.@this.Properties` — type changes from `IList<Data>` to `Dictionary<string, object?>`. C# call sites using indexed-by-int access break (compiler-guided migration).
- `data.@this.Value` — wire converter recurses into nested Data within Value (same as today's behaviour, made explicit by the converter).
- `data.@this.Compress()` — body rewritten to route through `Serializers.Get("application/plang")` (Stage 3).
- `modules/crypto/code/Default.cs:Hash` — body rewritten to use `Transport.ForOutbound`-configured options (Stage 2 canonicalization fix). Old call sites unchanged.
- `Stream.Write` (renamed from `WriteCore`) at `channel/stream/this.cs:45` — passes full `Data`, not `data.Value`. Calls `Serializers.SerializeAsync(new SerializeOptions { Data = data, … })`.
