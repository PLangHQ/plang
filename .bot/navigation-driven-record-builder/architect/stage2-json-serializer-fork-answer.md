# Ruling — the fork is already half-dead: the write collapse LANDED; finish the strip at the perimeter formatters, not the channel

Answers [`coder/stage2-converter-strip-blocked-on-json-serializer-fork.md`](../coder/stage2-converter-strip-blocked-on-json-serializer-fork.md). Settled with Ingi 2026-07-13.

**You own this.** Bodies, factoring, and mechanics are yours. Members cited with `file:line` were verified against HEAD (a7ccb0b8d) during the ruling; re-verify as you go.

## Q1 — one path is the intent AND the landed state for writes

No plan amendment: the STJ `application/json` serializer is not a keeper — and as a WRITE path it already doesn't exist. `channel.serializer.Json.SerializeAsync` (`Json.cs:83-104`) drives `json.Writer` + `data.Output` — its own comment: "no STJ in the value path, no per-type converters." That is the option-B ruling (`stage2-json-channel-write-answer.md`, settled 2026-07-10), landed via the wire-source-split merge. The `Json` CLASS stays — it is the registered `application/json` serializer and the registry `_default`; what dies is its residual STJ weight, and `application/json` is exactly what you asked: a bare view of the one writer (`emitsSchema: false`, no envelope, `[Out]` filtering via the bound view).

## The fork's actual remaining body (verified at HEAD)

- **`Json.cs:57` (`new dict.Json()`) is a DEAD registration.** `_options` reaches an STJ *serialize* in exactly one place — `writer.cs:88`, serializing `record.Type` (a type entity, never a dict). `DeserializeAsync` only reads, and `dict.Json` is write-only (`dict/Json.cs:41-43` — its `Read` throws). `ForView` / `WithConverter` / `WithIndentation` / `WithModifier`: zero production callers. `RawOptions` (`:189`): zero consumers. All of it strips.
- **The live firing surface for the `[JsonConverter]` attributes is the raw-STJ perimeter formatters** handing LIVE ITEMS to STJ: `goal/Methods.cs:46,81` (`p.Peek()` into the builder's LLM format), `build/code/Default.cs:329,753`, `ui/code/Fluid.cs:171`, `type/spec/render/this.cs:90,101,111`. Your "stripped all 13, build green" check couldn't see this — attributes fire by dispatch, not by reference. Strip without touching these sites and the LLM/debug previews silently degrade to reflected C# surfaces (`Cacheable`/`Prior`/`Template`…) or cycle.
- **The dict doc's consumer list (`dict/this.cs:17-23`) is STALE**: the snapshot-clone options bag (`variable/list/this.cs:18` `_snapshotClone`) is a never-used field — delete it; `set … type=json` `SerializeToNode` is grep-zero in production; the `application/json` channel is writer-driven. Rewrite the doc when the attribute dies (docs state what IS).
- `Error.FormatVerboseValue` (`Error.cs:435-443`) serializes only raw CLR `IDictionary`/`IList` — `dict.@this` implements neither, so nothing re-homes there.

## Q2 — the re-homes

| consumer | disposition |
|---|---|
| perimeter formatters (LLM previews, build/Fluid debug faces, spec render) | lower FIRST — `Peek()` → `.Clr<object>()` (dict/list decompose recursively to raw CLR, text → string) — then anonymous-graph STJ is converter-free and legitimate: these are perimeter debug/LLM faces, not the wire. Per-site shape is yours; if a site wants a real JSON face of a whole value, driving `json.Writer` over a buffer (the one writer) is the purer alternative — your call, site by site |
| debug-variable display | nothing to re-home (see `Error.cs` fact above) |
| snapshot-clone round-trip | dead field — delete `_snapshotClone`. Snapshot's own `Io.cs` rides the Wire/plang world and is the deferred snapshot branch — untouched here |
| `set … type=json` `SerializeToNode` | does not exist — stale doc, nothing to do |
| registry `_default` | unchanged — the `Json` class remains the `application/json` serializer |
| `DeserializeAsync` (the READ side) | stays STJ **this branch**. The read-side collapse (channel read → `Kind[json].Parse` → `clr(json)`) is real future work with the same status as the plang-channel STJ asymmetry: logged, not scope-crept. `json.Converter` (`Json.cs:58`) is read-side-live and stays |

## Q3 — plang is fully independent

`plang.@this` is `sealed class : ITransport` with its own `_outbound`/`_inbound` options (`plang/this.cs:44-50`) — no inheritance from `Json`, no STJ behavior inherited. The doc phrase "lives on the base `Json`" allocates the *Sensitive-stripping responsibility* (external channels strip; the inter-actor transport deliberately doesn't), not a base class. That plang still DRIVES STJ + the `Wire` converter is the already-logged asymmetry todo (2026-07-10) — not this branch.

## The type-entity converter (the 14th wearer)

Nativize `writer.cs:88`: `BeginRecord` emits the `{name, kind?, strict?}` entity through writer primitives (NEW, small — three fields) instead of `JsonSerializer.Serialize(_writer, record.Type, _options)`. Then strip the type entity's converter ONLY if nothing else fires it — grep the plang serializer's `Wire` drive first; if `Wire` depends on it for the envelope's `type` slot, the converter stays `[Obsolete]`-marked and dies with the asymmetry todo, noted there. Check the read side stays green either way (the wire reader's structured-entity parse is token-based, but verify).

## Order of work

1. Delete the dead surface: `Json.cs:57` registration, `_snapshotClone`, `RawOptions` + the caller-less `With*`/`ForView` members (re-verify zero callers as you delete).
2. Golden-pin the perimeter faces: the builder LLM format (`goal/Methods`) for a step with dict/list/text params, plus one build-preview shape.
3. Perimeter sites lower-then-STJ (or writer-driven, your per-site call).
4. Strip the 13 `Json.cs` files + `[JsonConverter]` attribute lines (grep fully-qualified `System.Text.Json.Serialization.JsonConverter`). `item/serializer/json.cs` stays (ruling 8). Update the stale dict doc.
5. `writer.cs:88` nativize + the 14th per its dependency check.

Acceptance: goldens byte-identical through step 4; zero `Json.cs` under `type/item/` except `kind/json/`; grep-zero `[JsonConverter]` under `type/` (modulo the 14th if deferred); baseline suites vs the recorded reds.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| perimeter lower-then-STJ | the value lowers ITSELF (`Clr`) before the CLR-edge tool runs — no converter shims keeping STJ in the value path | ✓ |
| `BeginRecord` emits the type entity natively | the one writer owns the envelope wholly; no STJ re-entry mid-envelope | ✓ |
| `Json` class retained as the `application/json` format | format selection stays the channel's job; the value still writes itself | ✓ |
| read side deferred, logged | no half-migration; same discipline as the plang asymmetry todo | ✓ |
| deleted: dead registration, dead options plumbing, dead clone bag | no speculative keepers | ✓ |
