# Lazy deserialize — architect verdict

**Status:** approved with changes. Ready for coder.
**Branch:** `lazy-deserialize`.
**From:** architect review of `.bot/lazy-deserialize/lazy-deserialize-proposal.md`, refined in a design session with Ingi.

## You own this

Every code shape, file path, signature, and stage boundary below is a **suggestion** to make the intent concrete. You (coder/tester) own the final shape. If a cleaner structure falls out while building — different file layout, a better registry signature, a different split between stages — take it, and say what you changed and why. The decisions that are *not* yours to move are the ones in the **Decisions** section: those came from Ingi directly.

## Why

The read side and the write side of the value model are out of balance, and it shows up four ways:

1. **Deserialize is fragmented; serialize is one registry.** Writing a value goes through one path: `data.Normalize` tags the value, `app.type.renderer.Of(type, format)` finds the type's `Write(value, IWriter)`, and an unrenderable value falls back to reflection. Reading a value goes through three-plus different mechanisms (listed under Incumbents). A second wire format would have a clean write half and a from-scratch read half.
2. **Reads parse eagerly and inconsistently.** `file.read` converts at read time based on the file extension; `http.get` deserializes by `Content-Type` and wraps the result in a second type; `channel.read` returns raw text and ignores the channel's `Mime`. Three behaviors, no single "here is where bytes become a typed value" seam.
3. **Some types name a format or transport they shouldn't know.** `path.JsonConverter`, `FromWire`, `type.json` bake JSON/the-wire into a type's own members. A type should hand its data to an `IWriter` (or read from a reader) and let the format be chosen one level up — which is exactly what the write side already does and the read side never did.
4. **Numbers silently lose C# type information.** `float` collapses to `double`, and `uint`/`ulong`/`Int128`/`BigInteger` have no representation at all. The language forgets what C# knew.

Two payoffs are worth stating up front because they're the strongest reasons to do this, and the original proposal buried them:

- **Verbatim passthrough.** A `Data` that is never touched serializes its raw straight back out — no parse-then-reserialize. Couriers (variable memory, callstack, channel routing) that don't read `.Value` cannot force materialization. The OBP courier rule (only leaves touch `.Value`) becomes enforced by construction instead of by discipline.
- **Relay without forced materialization.** A typed value can be routed/stored/forwarded without parsing it. (This was originally framed as "verify a signature on the raw bytes" — that was wrong, and the code proves it: signing recanonicalizes deterministically via `Signature.ToSigningBytes`, so it never compares raw arrival bytes. See Decision 8.)

## The shape, in one sentence

Build one symmetric **reader registry** that mirrors the renderer; consolidate the scattered readers into it; then hang lazy `Data`, the single I/O boundary, access-driven resolution, and the broadened number model off that seam.

## Incumbents — what exists today, and its disposition

The read/convert behavior this verdict re-homes. Trace each before writing; state of each at handoff:

| Mechanism | Where | Disposition |
|---|---|---|
| `type.Convert(raw)` | `app/type/this.cs:257` | Folds into the reader registry as the dispatch entry. |
| `AppTypes.TryConvert` (14-branch chain) | `app/type/list/Conversion.cs` | The branches that are *type-owned reads* move onto each type's `Read`; the generic plumbing (nullable unwrap, assignable fast-path, list element-walk) stays as the registry's residual. |
| `app.type.convert.Of` (per-family `Convert` hook) | `app/type/convert/this.cs:28` | Becomes the type's `Read`, reached through the registry. |
| `app.type.convert.OwnerOf` (clr→family switch) | `app/type/convert/this.cs:58` | The switch distributes onto each family — every family declares the CLR types it owns; the central `if u == typeof(int)…` ladder dies. |
| `FromWire` / `WireReader` | `app/type/this.cs:282`, `app/module/crypto/type/hash/this.cs:72`, `app/snapshot/this.Wire.cs:83` | Each becomes the type's `Read`. **Carve-out:** snapshot's `FromWire` and `app.Snapshot*` signatures stay (another branch owns snapshot) — touch their internals only if needed to compile. |
| `path.JsonConverter` | `app/type/path/this.JsonConverter.cs:24`, registered in 6 sites (`Diagnostics/Format.cs:31`, `channel/serializer/Json.cs:47`, `channel/serializer/plang/this.cs:51`, `module/builder/this.cs:50`, `app/this.cs:420`, `type/list/Conversion.cs:42,64`) | Deletes. Path gets a format-agnostic `Read` in the registry; the 6 registration sites stop wiring a path-specific JSON converter. |
| `type.json` converter | `app/type/this.json.cs:20`, attr at `app/type/this.cs:28` | Folds into the registry. |
| Per-type `JsonConverter<T>` set: `ErrorWire`, `signing.HashDataConverter`, `TimeSpanIso8601`, `EmptyStringToNullEnum…` | `app/error/IError.Wire.cs:33`, `app/module/signing/Signature.cs:49`, `app/channel/serializer/TimeSpanIso8601.cs:15`, `app/data/JsonString.cs:244` | Triage: domain-coupled ones (`ErrorWire`, `HashDataConverter`, `TimeSpanIso8601` — note it even names the format) become `Read` entries; the ones that are genuinely STJ-shape plumbing for JSON itself stay inside the json reader. |
| `Data.ConvertValue()` + lazy-on-navigate | `app/data/this.cs:199`, `app/data/this.Navigation.cs` | Folds into the single materialize-from-bytes path. |
| `Data._valueFactory` + `DynamicData` | `app/data/this.cs:186`, `:1205` | **Stays.** It's a different laziness — live recompute on every access, vs parse-once-and-cache. Two lazinesses that mean different things; the design says so rather than pretending it's one. |
| Renderer registry (the mirror to copy) | `app/type/renderer/this.cs:49`, discovery of `serializer/<format>.cs` static `Write` | Reference shape for the reader registry. |
| `data.Normalize` → `TypedValueNode` gate | `app/data/this.Normalize.cs:33`, `:156` | Reference for the read-side dispatch. |
| `file.read` + `FilePath.ReadText` (extension→convert at read time) | `app/module/file/read.cs:27`, `app/type/path/file/this.Operations.cs:61` | Stops deserializing; becomes a byte source feeding the boundary. |
| `http.get` / `ParseResponseAsync` (content-type→deserialize→`http.response`) | `app/module/http/code/Default.cs:463`, `:518`, `:551` | Stops deserializing; body → lazy value, metadata → Data properties. |
| `http.response.@this` (the parallel type) | `app/http/response/this.cs:10` | Dissolves into `Data`. |
| `channel.read` (returns raw text, ignores `Mime`) | `app/channel/stream/this.cs:69`; `Mime` at `app/channel/this.cs:37` | Becomes the single boundary that stamps type/kind from `Mime` and produces lazy `Data`. |
| Number model (`NumberKind` enum, `Kinds` list, `_i/_d/_f` union, float→double in Build and stamp) | `app/type/number/this.cs:27,48,216`, `this.Build.cs:25`, `this.Convert.cs:40`, `app/data/this.cs:242` | Replaced by Way 3 (below). |

## Decisions

These came from the design session and are settled.

1. **Reader registry, mirroring the renderer.** `Read` lives next to `Write` in `app/type/<name>/serializer/<format>.cs`; a `reader` registry mirrors `renderer` and dispatches by `(type, kind/format)`. Not a separate `reader/` tree — a type's two halves stay in one place. **Re-house, don't reimplement:** json reading still uses the existing System.Text.Json pipeline; the logic moves behind one door, it isn't rewritten. The win is three-plus read mechanisms collapsing to one.
2. **Where materialization fires.** `.Value` reaches into `bytes` (through the reader) **only when the stored value is null.** Inline-authored values (`set %x% = 5`) populate the value directly and leave `bytes` null, so they never hit the byte path and the existing `%var%`-resolves-fresh-per-read contract is untouched. There is no mode flag — which field is set tells you the origin.
3. **bytes vs string.** Raw is `bytes` only where the source is genuinely bytes. Text sources stay text — no utf-8 encode/decode tax on the common path just to make binary uniform.
4. **Access-driven resolution, no guessing.** Scalar/output access decodes utf-8 (and stays bytes if it doesn't decode); navigation into a value of a known type materializes through that type; `as <type>` reads toward that type. Type-unknown bytes touched as structured produce a **clear error** ("value has no type; add `as <type>`"). No content sniffing — a guess at json/xml/yaml/csv contradicts "the type reads itself" and PLang's determinism.
5. **Numbers — Way 3.** Store the number as its exact C# type; the kind *is* that type (no separate label to drift). The full C# scalar tower is supported as kinds: `sbyte/byte/short/ushort/int/uint/long/ulong`, `Int128/UInt128`, `Half/float/double`, `decimal`, `BigInteger`. Arithmetic promotes operands to a carrier that can't lose anything (`BigInteger` for integers, `double` for binary floats, `decimal` for base-10), following C#'s own promotion rules, then narrows the result to the smallest kind that fits — so `3000000000u + 2000000000u` lands as `5000000000` (a `long`) rather than wrapping. The `_i/_d/_f` union and the float→double collapse go away. Anything "N-wide" (a `uint4`-style vector) is a **list of numbers**, not a number kind. Under lazy, an untouched number off the wire is just its text bytes (lossless) carrying a kind hint; it materializes to the exact type on first touch.
6. **http folds into Data.** The http channel is bidirectional: write the request, read the response. The body becomes the lazy value (type/kind from `Content-Type`); status, headers, and duration become Data **properties**, read with `!`. `http.response.@this` deletes.

   Before — a second type sitting inside Data:
   ```csharp
   // app/http/response/this.cs
   public sealed record @this(
       [property: Out] int Status,
       [property: Out] Dictionary<string,string> Headers,
       [property: Out] object? Body,
       System.TimeSpan Duration);
   ```
   After — just Data:
   ```
   Data {
     value:      <lazy body bytes>           // %response%        → the body
     type/kind:  text / json   (from Content-Type)
     properties: { status: 200, headers: {…}, duration: 0.12s }   // %response!status%, %response!content-type%
   }
   ```
   ```
   - get http https://api/...     write to %response%
   - if %response!status% == 200   / property read — body not touched yet
       - write out %response.name%  / now body materializes: bytes → json → .name
   ```
7. **`.plang` self-describing header** — out of scope. Stage 3 may stub the "type/kind from the payload's own header" case; the `.plang` format design lands elsewhere.
8. **Signing — unchanged by lazy.** *(Corrected from an earlier draft after checking the code.)* Signing recanonicalizes deterministically — `Signature.ToSigningBytes` re-serializes with `SigningOptions` (Signature excluded, ordered), so verification recomputes the canonical form and never compares raw arrival bytes. The deterministic canonicalization already guarantees re-serialization matches, so there is no "verify on raw" to build. A signed Data therefore **materializes its value on verify** (recanonicalization touches it) — a legitimate touch, identical to today. Lazy changes nothing about when or how signatures are checked. Do **not** rewire signing to read `_raw`.

## Stages

Land in dependency order; each green before the next.

- **Stage 1 — reader registry + OBP cleanup.** Build the reader registry mirroring the renderer; consolidate the incumbents from the table behind it; distribute `OwnerOf` onto each family; delete the format-named-on-the-type members. **No behavior change** — pure consolidation. (Snapshot signatures stay.)
- **Stage 2 — numbers (Way 3).** Replace the union and the float→double collapse; numbers read toward their exact kind through the Stage-1 registry; arithmetic promotes-then-narrows. Each family (number especially) declares its CLR types — which is where Stage 1's distributed `OwnerOf` pays off.
- **Stage 3 — lazy `Data { bytes, type, kind, value }`.** Add `bytes`; `.Value` reads from `bytes` through the registry only when the value is null. Fold `ConvertValue` in. Keep `_valueFactory`/`DynamicData`.
- **Stage 4 — one I/O boundary.** `channel.read` stamps type/kind from `Mime` and produces lazy `Data`; `file.read` and `http.get` become byte sources; http folds into Data per Decision 6.
- **Stage 5 — access-driven resolution.** utf-8 decode on scalar access; materialize-through-known-type on navigation; `as <type>` cast; clear error on type-unknown structured access. No sniffing.

Stage 2 (numbers) is independent of 3–5 and can land right after 1. Sequence within reason; the hard ordering constraint is 1 before everything.

## OBP cleanup being done here (boyscout)

Two sub-smells, both flagged in review:

- **A type names a format or transport it shouldn't know** — `path.JsonConverter` (JSON baked into the path type), `FromWire` (the transport baked into the method name), `type.json`, the per-type `JsonConverter<T>` set. The cure is the reader registry: each type gets a format-agnostic `Read`, format chosen one level up — same as the write side already does. These names die.
- **A registry switches over types instead of asking each element** — `OwnerOf`'s `clr→family` ladder. The knowledge of which CLR types a family owns belongs on each family; `OwnerOf` composes from those declarations.

Not a smell, for the record: `.Of(type, format)` on a registry. The format is a parameter decided above and passed in — exactly the right pattern. The word "Of" is fine; a format/transport *named inside a type's own member* is not.

## Out of scope

- `.plang` self-describing header format (Decision 7).
- Snapshot rename to OBP names — another branch owns it; `SnapshotToWire`/`SnapshotFromWire`/`ResumeFromWire` and snapshot's `FromWire` signatures stay (internals adjustable to compile).
- SIMD/vector numeric types — a `uint4`-style value is a list of numbers; no vector type is being introduced.
