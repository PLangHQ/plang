# Test Strategy — lazy-deserialize

> **Note for test-designer:** every test name, fixture, layer assignment, integration cut, and failure case here and in the companion `test-coverage.md` is a **suggestion** that captures architect intent — not a contract. You own the suite. Reshape names, restructure fixtures, move tests between layers, or replace whole approaches as the real constraints demand. Push back on the strategy itself if you find it wrong.

## Scope

This branch reshapes how `Data` is *read* — the mirror of the write-side `data-normalize` work. The integration cuts below are the contract for end-to-end behavior; per-topic and negative-path tests sit beneath them in [`test-coverage.md`](test-coverage.md).

Things that have to be true at the end:

- **One reader decodes everything, with no output change.** `app.type.reader.Of(type, kind).Read(...)` replaces `type.Convert` / the `Convert` hooks / `FromWire` / `path.JsonConverter` / `type.json` / the `JsonConverter<T>` set — and Stage 1 changes *no* observable output (the suite is green before and after with no expected-output edits).
- **Lazy works.** A value read from a source or off the wire is not parsed until touched. Untouched, it serializes its raw straight back out (verbatim passthrough); a signature verifies against the raw without materializing. Touched, it materializes correctly via the reader.
- **Numbers don't lose type.** The full C# scalar tower round-trips losslessly; arithmetic promotes-then-narrows (no silent overflow wrap); `double⊕decimal` errors.
- **One boundary.** `file` and `http` read through `channel.read`; the body is lazy, status/headers are properties; `http.response` is gone; `config.json` lands as `{text, json}`.
- **No guessing.** Type-unknown structured access errors and asks for `as <type>` — nothing sniffs content.

## Test layer mapping

**C# TUnit (`PLang.Tests/`)** — pins internal behavior the engine owns:
- Reader registry: `Of(type, kind)` precedence (runtime-exact → generated-exact → runtime-`"*"` → generated-`"*"`); discovery finds a `serializer/Default.cs` static `Read`.
- Per-type `Read` round-trips (path, number, image, text/json) match what the old converters produced — the "no behavior change" pin.
- Distributed `OwnerOf`: each family declares its CLR types; the central switch is gone (reflection check).
- Numbers: exact-CLR storage + kind derivation across the tower; `Read` parses to the exact kind; promote-then-narrow result kinds; `double⊕decimal` errors.
- Lazy `Data`: `.Value` materializes only when `_value` null and `_raw` set; authored values (`_value` set) never hit the byte path; `_raw` survives materialization; a mutation invalidates `_raw`.
- `Wire.Read` captures the value slot raw and defers; `LiftDataIfShaped` is gone (reflection/behavior check).

**PLang `.goal` (`Tests/`)** — pins developer-facing surfaces:
- Read `config.json`; `%cfg%` untouched is the json text; `%cfg.port%` navigates and returns the field.
- `get http`; `%response!status%` reads without touching the body; `%response.field%` materializes the body.
- Sign a Data, serialize, read back on the other side, verify the signature.
- Number arithmetic in a goal (a big-int sum that would overflow `uint` lands correctly; a `double`+`decimal` step errors).
- Type-unknown value navigated as structured → the "add `as <type>`" error; `%x as json%` resolves it.

**Integration cuts (full pipeline)** — pin the architect-level contracts; see below.

Rule of thumb: internal behavior (a method, the registry, the field split, the cache) → C#. What a developer *sees* writing a goal (`read`, `get http`, `%cfg.port%`) → `.goal`. A multi-stage flow (read → courier → serialize, or sign → wire → verify) → an integration cut. The matrix in `test-coverage.md` assigns each behavior to a layer.

## Integration cuts

### Cut 1: Verbatim passthrough (the headline payoff)

**Setup:** Read a value from a source (a `config.json` file; an `application/plang` wire payload) into a `Data`. Do **not** touch `.Value`.

**Capture:** Route it through a courier path (assign to a variable, pass through a goal call, write to a channel) without any navigation/`As<T>`. Serialize it back out.

**Must prove:**
- The serialized bytes equal the original raw, byte-for-byte (no parse-then-reserialize).
- `_value` was never materialized (the reader was never invoked — assert via a probe/counter or that materialization side effects didn't fire).
- The same Data, once navigated, *does* materialize and round-trips semantically.

### Cut 2: Touch materializes correctly

**Setup:** `config.json` = `{"port": 8080}` read as `{text, json}`; a number `"9999999999999999999999"` read as `{number, biginteger}`; an `image/png` read as `{image, png}`.

**Capture:** `%cfg%` untouched; then `%cfg.port%`; the number used in arithmetic; the image's width read.

**Must prove:**
- `%cfg%` untouched is the json *string* (type=text); `%cfg.port%` parses on navigation and returns `8080`.
- The big-int materializes to a `BigInteger` losslessly.
- The image materializes only when a property is read, not at read time.

### Cut 3: Sign → wire → verify on the raw

**Setup:** A signed `Data`, including a **nested** signed Data (the case `LiftDataIfShaped` used to catch — e.g. a `Signature` carrying a Data).

**Capture:** Serialize, read back on the receiving side (`Wire.Read`), verify.

**Must prove:**
- Verification runs against `_raw` and succeeds without forcing `.Value` (assert the body wasn't materialized to verify).
- The nested signed Data round-trips and its inner signature reaches `signing.verify` — without `LiftDataIfShaped`, via the type-driven reader.
- A tampered raw fails verification.

### Cut 4: http — body lazy, metadata eager

**Setup:** `get http` against a JSON endpoint (status 200, a json body).

**Capture:** `if %response!status% == 200`, then `%response.field%`.

**Must prove:**
- `%response!status%` and headers read from properties without materializing the body.
- `%response.field%` materializes the body (raw → json → field).
- `http.response.@this` no longer exists (the result is plain Data — reflection check it's gone).

### Cut 5: Number tower round-trip + arithmetic

**Setup:** Values across the tower — `sbyte`, `uint`, `ulong`, `Int128`, `BigInteger`, `Half`, `float`, `decimal`.

**Capture:** Round-trip each through the wire; run representative arithmetic.

**Must prove:**
- Each round-trips with its exact kind preserved (a `float` comes back `float`, not `double`; a `uint` comes back `uint`).
- `3000000000u + 2000000000u` → `5000000000` as `long` (no `uint` wrap).
- `double ⊕ decimal` raises the explicit-cast error.
