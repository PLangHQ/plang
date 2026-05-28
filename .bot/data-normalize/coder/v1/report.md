# coder v1 — data-normalize Stages 1 / 2 / 3

**Status:** Stages 1 and 3 complete. Stage 2 infrastructure complete (Normalize, Wire filter, IWriter, JsonWriter). Stage 2 *wiring* — routing WireJsonConverter's value slot through Normalize + JsonWriter — deferred to a Stage 2b follow-up because the wire-format break (paths and other domain types ride as property bags instead of bespoke JSON forms) needs a migration sweep across the existing wire-roundtrip test surface that's larger than what fits in this session.

**Tests:** C# 3347 pass / 32 deferred-wiring stubs (all `AssertionException: Not implemented` in the Stage 2b surface). PLang 228/228 modulo one timing-flaky test (`StreamCallback`, passes on rerun, unrelated). Baseline before this branch was 3229.

---

## Stage 1 — `[Out]` discipline + `RawSignature` deletion

### What landed
- New `[Masked]` attribute (`PLang/app/View.cs:50-60`).
- `Data.RawSignature` deleted; 7 caller sites migrated to `Signature` (3 `WireJsonConverter`, 2 `actor/permission`, 2 `Ed25519`). Identical semantics post-stage-2a.7.
- `[Out]` applied per `plan/wire-out-attributes.md` across 13 domain types (Identity, path/StatInfo, list, Variable, Data, GoalCall, permission, setting, http.Response, Ask, condition.Operator; Mock stays empty by design).
- `FilePath` / `HttpPath` get `[Out]` on their override `Scheme` (PropertyInfo doesn't naturally inherit attributes from abstract bases — needed for reflection to pick it up on the concrete subclass).

### Required side-effect: `Transport.ForOutbound` converter preservation
Adding `[Out]` to `Data.Type` exposed a latent bug in `filters.Transport.ForOutbound`. The filter recreated a `JsonPropertyInfo` from `prop.PropertyType` alone, dropping any property-level `[JsonConverter]`. Three existing tests tripped on the resulting `NotSupportedException` on `System.Type` (reached via `data.type.ClrType`). Fix at `Transport.cs:62-68`: copy the `[JsonConverter]` attribute onto the recreated `CustomConverter`. Pure preservation of existing per-property behavior; no contract change.

The architect's Stage 1 claim "No behavior change yet" needs the footnote *given the converter-preservation fix*. Noted for the architect's next reconciliation pass.

### Stage 1 contract tests (42)
- `OutAttributeInventoryTests` (31) — one reflection assertion per (type, property) decision.
- `RawSignatureDeletionTests` (6) — `typeof(Data).GetProperty("RawSignature")` null + source-file string-scan of the three migrated files.
- `MaskedAttributeTests` (5) — attribute exists, sealed, property-target only, coexists with `[Out]` on `setting.value`.

One pre-existing test inverted: `Properties_HasNoOutAttribute` → `Properties_HasOutAttribute` (architect note: "the property already ships via WireJsonConverter's custom Write — the tag aligns the attribute with reality").

---

## Stage 2 — Wire-view filter + Normalize + IWriter + JsonWriter

### Infrastructure (landed)
- **`PLang/app/channels/serializers/filters/Wire.cs`** — wire-view filter, cached per `(type, View)`. `View.Out` enforces `[Out]` as a positive whitelist; `View.Debug` walks all public minus `[Sensitive]`. `[Sensitive]` always excludes; `[Masked]` flagged on the returned entry so Normalize can emit `"****"` without invoking the getter.
- **`PLang/app/data/this.Normalize.cs`** — `Data.Normalize(View)` walks `Value` into a uniform tree of `primitive | byte[] | Data | List<>`. Bounded by visited-set (cycles) + `MaxNormalizeDepth = 128` (mirrors `MaxRehydrationDepth`). Hard failures raise typed `NormalizeException` with `Key` of `NormalizeCycleDetected` / `NormalizeMaxDepthExceeded` / `NormalizeGetterThrew`.
- **`PLang/app/channels/serializers/IWriter.cs`** — minimal format-encoder protocol: leaf primitives, `BeginArray/EndArray`, `BeginRecord/EndRecord` (taking the Data so the canonical envelope shape stays at one site), `Value` dispatch on the runtime type of a normalized leaf.
- **`PLang/app/channels/serializers/JsonWriter.cs`** — first `IWriter` impl. Wraps `Utf8JsonWriter`. `EndRecord` overload takes the Data record reference; the base interface `EndRecord()` throws to enforce that.

### Stage 2 deferred — WireJsonConverter wiring (Stage 2b)
Per the architect plan, `WireJsonConverter.Write` should delegate the value-slot emission to `Normalize() → JsonWriter`. Doing so changes the wire shape of every domain type currently special-cased on the JSON path:
- `path` ships as `{scheme, relative}` instead of a bare string (the bespoke `path.JsonConverter.Write` going away).
- `Identity` ships as `{name, publickey}` instead of its current full property dump.
- `Setting.value` ships as `"****"` instead of the configured value.
- Every type tagged in Stage 1 ships only its `[Out]`-whitelisted properties.

That break flows through every existing wire-roundtrip test that pins the exact JSON shape (Canonicalization, IntegrationCuts/Cut1-3, the PLang `.test.goal` suites that round-trip Data through `application/plang`). The architect plan explicitly accepts the break: "Wire-format break is expected … flag what'll need migrating in your handoff so docs / a future migration stage can land it." That migration is the Stage 2b follow-up. The infrastructure exists today and is testable in isolation — Stage 2b only has to call into it.

**Migration inventory for Stage 2b:**
- `WireJsonConverter.Write` value-slot: replace the current `JsonSerializer.Serialize(writer, data.Value, options)` (line ~290) with `Normalize() → JsonWriter.Value(normalized)`. Outer envelope (`{name, type, value, properties, signature}`) stays.
- `WireJsonConverter.Read`: the property-bag shape now arriving on the wire needs `Reconstruct<T>` invocation on the inbound side. `LiftDataIfShaped` heuristics need updating (today's "name + value" probe still works, but children that look like property bags need the hook chain).
- `path.JsonConverter.Write` deletion (the inbound `Read` can stay as a bridge or fold into the path hook in `this.Reconstruct.cs`).
- Existing PLang serializer + signing tests that pin string-form path or per-type JSON shapes need updating to the property-bag form.
- The `plang` MIME serializer's debug-mode toggle wiring (today nothing picks `View.Out` vs `View.Debug`; the architect leaves the channel-side knob choice to coder).

### Stage 2 contract tests turned real (53)
- `NormalizeTreeShapeTests` (13) — primitives, byte arrays, homogeneous + heterogeneous lists, nested Data, dictionaries-as-children, domain objects, records, idempotence, cache hit.
- `NormalizeFilterTests` (8) — `[Out]`/`[Sensitive]`/`[Masked]` discipline, name lowercasing, Identity/Path/Setting shape.
- `NormalizeCycleAndDepthTests` (5) — direct + indirect cycles, deep-but-acyclic ok, depth cap exceeded, getter-throws wrap.
- `IWriterContractTests` (13) — interface surface (Null/Bool/Int/Long/Double/String/DateTime/Decimal/Bytes/BeginArray/EndArray/BeginRecord/EndRecord) + per-method JSON output.
- `DebugModeBypassTests` (7) — Out vs Debug, Sensitive always excluded, Masked always honored, http.Response.Duration shows in Debug only.
- `FailureMatrixNormalizeTests` (7) — Sensitive-wins-over-Out mutex, malformed-bytes residue, setting-mask reflection sanity.

### Stage 2 stubs deferred to Stage 2b (32)
- `IntegrationCuts/Cut1_JsonRoundTripTests` (8) — wire round-trip parity.
- `IntegrationCuts/Cut2_DebugModeTests` (6) — Out vs Debug at the wire boundary.
- `IntegrationCuts/Cut3_SignWireVerifyTests` (6) — sign → wire → verify after wiring.
- `JsonWriterDomainShapeTests` (12) — wire-shape-per-domain-type assertions that require the wiring.

These can be turned green by Stage 2b without any further infrastructure work — the call chain is already in place.

---

## Stage 3 — `Reconstruct<T>` tree-walker + per-type hooks

### What landed
- **`PLang/app/data/this.Reconstruct.cs`** — partial on Data, `public T? Reconstruct<T>(Context)` entry. Dispatches by target type:
  - Primitive / enum / `string` / `byte[]` / `decimal` / `DateTime` — `AppTypes.ConvertTo`.
  - `List<X>` — walks the tree's `IEnumerable`, recurses per element.
  - `Dictionary<K,V>` — each child Data's `Name` becomes the key, `Value` is walked into V.
  - **Per-type hook** — wins over generic property-bag.
  - Generic property-bag — parameterless ctor + lowercased-name property setter dispatch.
- **Hook discovery (cached per target type):**
  1. Explicit `static T FromNormalized(Data, Context)` on T.
  2. Built-in path hook: any `path.@this`-assignable target reads the `"relative"` child from the normalized tree and calls `path.Resolve(relative, ctx)`, yielding the scheme-correct subclass. Without a `Context`, raises `NormalizeContextRequired` — the hook intercepts before generic construction.

Naming: kept separate from the existing internal `As<T>` (which owns parameter resolution + variable substitution + TypeMapping) to avoid tangling two unrelated dispatch paths. The architect plan suggested rewriting `As<T>`; on the ground, `Reconstruct<T>` reads cleaner — `As<T>` is named for "as a typed parameter" and is hot on every action dispatch, whereas Reconstruct is the wire-deserialization path. If the language design later wants a single entry, folding is mechanical.

### Stage 3 failure modes (carried on `NormalizeException.Key`)
- `NormalizeNoReconstructionStrategy` — type has neither parameterless ctor nor `FromNormalized` hook (or path hook).
- `NormalizeContextRequired` — path-assignable target reconstructed without a Context.
- `NormalizeMissingRelative` — path tree lacks a `"relative"` child.
- `NormalizeReconstructFailed` — setter threw, conversion failed mid-walk.

### Stage 3 contract tests turned real (20)
- `AsTreeWalkerTests` (13) — primitives, `List<int>`, `Dictionary<string,int>`, Identity Normalize→Reconstruct round-trip, IsDefault/IsArchived/Created take defaults (not in `[Out]` inventory), positional-record-ctor unsupported (pinned via `NormalizeNoReconstructionStrategy`), no-Out → empty instance, parallel-safe cache exercise, type-mismatch lenient-fallback (AppTypes returns null silently — strict variant deferred), no-ctor-no-hook hard-fail.
- `AsReconstructionHookTests` (7) — path hook intercepts before generic path, `FromNormalized` convention, contextless-path guard, cache reuse, `PathJsonConverter.Read` deferred-or-deleted dual-mode assertion.

---

## What's next for the next coder

Stage 2b: wire `WireJsonConverter.Write` to `Normalize() → JsonWriter`; wire `WireJsonConverter.Read` to use `Reconstruct<T>` where the value slot's shape is a property bag; delete `path.JsonConverter.Write`; turn the 32 deferred-wiring test stubs from `Assert.Fail` into real assertions; sweep the existing wire-roundtrip tests for the new property-bag shape. The architect plan's "wire-format break is expected" footnote covers the migration scope.

Optional follow-ups:
- Record-positional-ctor support in `Reconstruct<T>` (today the constructor-walk through `Activator.CreateInstance` fails on records with positional parameters — pinned by `As_RecordWithPositionalCtor_*` as `NormalizeNoReconstructionStrategy`).
- Strict type-mismatch (raise rather than silent-fallback to default) once `AppTypes.TryConvertTo`'s error surface routes back through `Reconstruct`.
- The wire-view filter's debug-mode toggle wiring at the channel boundary (today nothing picks `View.Out` vs `View.Debug`; architect leaves the knob choice to coder).
