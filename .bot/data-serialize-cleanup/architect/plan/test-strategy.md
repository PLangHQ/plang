# Test Strategy

## Scope

The integration cuts below are the contract for end-to-end behaviour; per-topic and negative-path tests sit beneath them in [test-coverage.md](test-coverage.md). Test-designer reads this file for the narrative + the cuts, then walks the coverage matrix top-to-bottom and writes one test per row.

## Test layer mapping

Three layers — C# TUnit, PLang `.goal`, and integration cuts. The rule for this branch:

- **C# TUnit** (`PLang.Tests/App/...`) pins internal C# behaviour: the `ISerializer` surface, the wire converter's sign-if-missing rule, the `Properties` C# type and its validation, crypto canonicalization, the `Json` composition extensions (`WithConverter`, `WithModifier`).
- **PLang `.goal`** (`Tests/`) pins developer-facing surfaces: `%x.field%` vs `%x!key%` navigation, write-then-read-back round trips through real channels, multi-step pipelines that combine the new behaviour with everyday actions.
- **Integration cuts** (C# TUnit, but at the channel/serializer integration boundary) exercise the full byte pipeline end-to-end: channel → serializer → wire converter → sign-if-missing → bytes → reverse. The cuts catch issues per-layer tests can't see because each layer's contract is locally correct.

Per-behaviour assignment is in the coverage matrix. Rule of thumb: if it's about the C# API shape, it's C#; if it's about how a PLang developer uses the action or syntax, it's goal; if it's about the byte pipeline holding together across files, it's an integration cut.

## Integration cuts

Four cuts. Each is one TUnit test class.

### Cut 1 — Plain Data round-trip with implicit signing

**Setup.** A `channel.stream.@this.Memory` channel with `Mime = "application/plang"`. Actor context wired so `EnsureSigned` can resolve a signing identity.

**Capture.** `await channel.WriteAsync(Data.Ok("hello", name: "greeting"))`. Then `var read = await channel.ReadAsync()`. Snapshot the bytes the channel wrote.

**Resume / assert:**
- Wire JSON has exactly four top-level fields (no `properties` since none were set): `name`, `type`, `value`, `signature`.
- `read.Value.As<string>() == "hello"`, `read.Name == "greeting"`.
- `read.Signature` is populated.
- `crypto.Verify(read)` succeeds against the wire bytes.

**Proves.** ISerializer input contract holds end-to-end; wire converter Write/Read are symmetric; sign-if-missing fires automatically during the converter walk; canonicalization hash matches wire shape (Stage 2's canonicalization fix).

### Cut 2 — Sign-then-compress preserves inner attestation

**Setup.** Single actor A with a signing identity. A memory channel with `Mime = "application/plang"`.

**Capture.** With actor A in context: `var d1 = new Data("user", new { firstName="Ingi" }); var d2 = d1.Compress(); await channel.WriteAsync(d2);`. Snapshot the wire bytes.

**Resume / assert:**
- Outer wire JSON has `type: "archived"`, `value` as base64-encoded byte[], `signature` populated.
- Decoding the byte[] (base64 + gunzip) yields a serialized Data with its own populated `signature`. Sign-if-missing fired during Compress's serialize step, before the bytes were gzipped.
- `await d2.Decompress()` returns a Data equal to `d1` in name/value, with the inner signature preserved.
- Mutating any byte inside the wire `value` (post-encode) fails outer signature verification.

**Proves.** Compress routes through the registered `application/plang` serializer (Stage 3 fix); sign-if-missing fires during in-process byte conversion; sign-then-compress chain is cryptographically tight (outer signature binds inner through the byte-leaf).

### Cut 3 — Multi-actor forwarding chain

**Setup.** Three actor contexts A, B, C with distinct signing identities. Each has its own channel/context.

**Capture.**
1. In context A: write `d1 = Data("user", {firstName: "Ingi"})` to a channel. Capture wire bytes A. Sign-if-missing → `d1` carries A's signature.
2. In context B: read bytes A as `d1Received`. Wrap as `d3 = Data("forwarded", d1Received)`. Write `d3`. Capture wire bytes B. Sign-if-missing → `d3` gets B's signature; the walk into `d3.Value` sees `d1Received.Signature` already populated → skip.
3. In context C: read bytes B as `d3Received`. Extract the inner via `d3Received.Value` (or `%d3.user%` if navigating).

**Resume / assert:**
- `d3Received.Signature.Identity == B`.
- The inner Data (in `d3Received.Value`) has `Signature.Identity == A`. Chain preserved across the wrap.
- Both signatures verify independently against their respective bytes.
- Mutating any byte of `d3.Value` (the JSON of `d1Received`, including its `signature` sub-object) fails `d3`'s outer signature verification — the Stage 2 canonicalization fix is what makes this work.

**Proves.** Sign-if-missing is idempotent — the walk doesn't re-sign an already-signed Data. Forwarding semantic preserves provenance without explicit choreography. Canonicalization covers structurally-nested inner signatures, not just byte-leaf payloads.

### Cut 4 — Data with Properties — nested wire shape and navigation

**Setup.** Memory channel with `Mime = "application/plang"`, actor context wired.

**Capture.** `var d = Data.Ok("Hello!", name: "response"); d.Properties["cost"] = 100; d.Properties["model"] = "claude-opus-4-7"; await channel.WriteAsync(d);`. Snapshot bytes. Then `var read = await channel.ReadAsync()`.

**Resume / assert:**
- Wire JSON has five top-level fields: `name`, `type`, `value`, `properties`, `signature`.
- `properties` is a nested object: `{ "cost": 100, "model": "claude-opus-4-7" }`. No top-level `cost` or `model` field.
- `read.Properties["cost"]` equals 100 (note JSON int→long promotion in the coverage matrix).
- `read.Properties["model"]` equals "claude-opus-4-7".
- A sibling `.goal` test verifies `%response!cost%` resolves to 100 and `%response%` renders the LLM text "Hello!".
- Mutating `properties.cost` in the wire JSON (re-encode without re-signing) fails outer signature verification.

**Proves.** Properties round-trip through the nested wire shape; Property keys are unconstrained (no reserved-key throw at insertion); canonicalization binds Properties; `!` navigation reaches the correct store.

## What's NOT covered by these cuts

The cuts give end-to-end signal for the four headline scenarios. Everything else lives in [test-coverage.md](test-coverage.md):

- Per-stage compile-error tests (ISerializer non-Data input fails to build).
- OBP-rename mechanical tests (`serializer.Type` returns MIME, `channel.Write` overridden in each subclass).
- Per-MIME serializer behaviour (Json strips the wrapper; Text emits `Value.ToString()`).
- Canonicalization-options identity (`crypto.Hash(data)` bytes ≡ wire-serializer bytes minus the outermost Signature, the latter excluded by `Signature.SigningOptions`).
- Non-compressible `Type` returns self unchanged from `Compress()`.
- `EnsureSigned` without Context throws `InvalidOperationException`.
- `Properties.set` with an unsupported value type throws.
- `Decompress` with malformed archived value returns a Data with Error populated.
- Unknown top-level wire fields are silently ignored on read.
- A Data with empty Properties dictionary omits `properties` from the wire entirely.
- Variable parser disambiguation: `%!flag%` (negation prefix) vs `%x!key%` (Properties access).
- The merged plang serializer's `application/plang+data` removal (registry shape; registry lookup of the retired MIME returns null).

The cuts only exercise the `application/plang` serializer end-to-end. Per-MIME serializer tests (Json, Text) are C# unit tests in the coverage matrix.
