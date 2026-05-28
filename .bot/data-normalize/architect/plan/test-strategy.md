# Test Strategy — data-normalize

> **Note for test-designer:** every test name, fixture shape, layer assignment, integration cut, and failure case in this file and the companion `test-coverage.md` is a **suggestion** that captures architect intent — not a contract. You own the test suite. Reshape names, restructure fixtures, move tests between layers, or replace whole approaches as the real constraints demand. Push back on the strategy itself if you find it wrong.

## Scope

The integration cuts below are the contract for end-to-end behavior; per-topic and negative-path tests sit beneath them in [`test-coverage.md`](test-coverage.md).

This branch reshapes how `Data` is encoded on the wire — every test here lives downstream of that reshape. One thing has to be true at the end:

**The new pipeline works.** `Data.Normalize()` → `IWriter` → bytes → reader → `As<T>` round-trips correctly for every domain type in [`wire-out-attributes.md`](wire-out-attributes.md), with `[Out]` filtering in production mode and debug-mode bypass that still honors `[Sensitive]` + `[Masked]`.

A second non-reflection format (protobuf / MsgPack) is *not* shipped on this branch — the `IWriter` abstraction is shaped to accept one without changes to Normalize or any domain type, but actually proving that with a concrete second format is deferred. No cross-format tests here.

## Test layer mapping

**C# TUnit (`PLang.Tests/`)** — pins internal behavior the engine owns:
- `Data.Normalize()` produces the expected tree shape per input type.
- Cycle detection trips at the right depth / on the right visited reference.
- `[Out]` filter actually filters (properties without `[Out]` don't appear in the normalized tree; properties with `[Sensitive]` never appear; `settings` type never appears).
- `As<T>` reconstructs each domain type from its normalized tree.
- `IWriter` contract — each method emits the right bytes / sequence per writer impl.
- Property-lookup cache works (Normalize the same type twice, second call doesn't re-reflect).

**PLang `.goal` (`Tests/`)** — pins developer-facing surfaces:
- A goal that writes a `path` to a Data, serializes it via the channel, reads it back, and verifies the path resolves correctly on the other side. Today this worked because `path.JsonConverter` collapsed to a string; after this branch it works through the property-bag pipeline. The goal-level test catches if the developer-visible behavior changed.
- A goal that signs Data, serializes it, deserializes on the receiving side, and verifies the signature. Tests Stage 1's RawSignature → Signature migration didn't break signing end-to-end.
- A goal that round-trips a Data through the channel with `[Sensitive]` properties — confirms secrets don't leak.

**Integration cuts (full pipeline, end-to-end)** — pin the architect-level contracts:
- See the named cuts below.

Rule of thumb: if the assertion is about a type's *internal* behavior (a method, a property filter, a reflection cache), it's C#. If the assertion is about what a developer *sees* when they write `serialize %x%` in a goal, it's `.goal`. If the assertion is about a multi-stage flow (sign → serialize → channel → channel → deserialize → verify), it's an integration cut.

The matrix in `test-coverage.md` assigns each individual behavior to a layer.

## Integration cuts

Three named cuts. Each is the contract for one end-to-end behavior; per-`@this` and negative tests live beneath them in the matrix.

### Cut 1: JSON round-trip parity

**Setup:** Build a representative `Data` value (a Data<path>, a Data<Identity>, a Data<List<Data>>, a Data with `[Sensitive]` properties, a Data<setting>). Pre-normalization shape, in memory.

**Capture:** Serialize via the wire serializer using `JsonWriter` (Stage 2's first and only format on this branch). Capture the bytes.

**Resume:** Read those bytes back through the deserializer. Reconstruct via `As<T>` (Stage 3).

**Must prove:**
- Reconstructed Data is semantically equal to the original (for the properties marked `[Out]` — derived/local properties may differ).
- `[Sensitive]` properties are absent from the bytes.
- `[Masked]` properties' names appear on the wire but values are `"****"`.
- path round-trips through the new property-bag shape, not the old single-string shape.
- Signature, if present, verifies after the round trip.

### Cut 2: Debug-mode bypass

**Setup:** Domain values that span all property categories: Out, Skip-derived, Skip-local, Sensitive, Masked. The `path` and `Identity` types together cover most categories.

**Capture:** Serialize once in `View.Out` mode, once in `View.Debug` mode.

**Resume:** Compare the byte payloads / the parsed property maps.

**Must prove:**
- Debug-mode payload contains every public property *except* `[Sensitive]` (and `[Masked]` values, which still appear as `"****"`).
- Out-mode payload contains only `[Out]`-tagged properties.
- `[Sensitive]` is never exposed even in debug.
- `[Masked]` values are never unmasked, even in debug.

### Cut 3: Sign → wire → verify

**Setup:** A `Data` instance with a signed envelope (the Stage 1 RawSignature → Signature migration is the surface here).

**Capture:** Sign the Data. Serialize via the wire serializer (`JsonWriter` from Stage 2 — the only concrete format on this branch).

**Resume:** Receive the bytes on the other side. Deserialize. Verify the signature.

**Must prove:**
- Signing pipeline still works after RawSignature deletion.
- Signature serializes and deserializes intact.
- Verification succeeds on the reconstructed Data.
- The four call-site migrations (signing.verify Ed25519, actor/permission ×2, plang serializer ×2) all work end-to-end.

## What's *not* covered by these cuts

The matrix and failure matrix in `test-coverage.md` pick up:

- Per-`@this` Normalize unit tests (one per domain type — does Normalize produce the right shape for *that specific type's* properties).
- Per-`@this` As<T> unit tests (does reconstruction work for that type).
- Negative paths: cycle detection at depth N, cycle detection on a reference loop, malformed normalized tree, type mismatch in As<T>, missing required property.
- The property-lookup cache (Normalize the same type twice, assert second call doesn't reflect).
- The IWriter contract per method per format.
- Stage 1's `[Out]` attribute applications — one test per type confirming the right properties got tagged (read the type via reflection, check the attribute table matches the wire-out-attributes inventory).

Read `test-coverage.md` top-to-bottom; each row in the coverage matrix is one test.
