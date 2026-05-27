## 2026-05-27 — Stage 1 OBP renames; Properties shape pivot to nested

Two rounds of follow-up review on the previous pivots.

**Stage 1 OBP renames.** Ingi flagged `ContentType` and `FileExtension` as redundant qualifier suffixes — the owner is `serializer`, so the qualifiers carry no information. Same diagnosis for `WriteCore`/`ReadCore`/`AskCore` on the channel base — `Core` just means "don't call this directly," which is access-modifier discipline, not name decoration. Folded both renames into Stage 1 since it already touches the same surface:

- `ISerializer.ContentType` → `Type`, `FileExtension` → `Extension` (across interface + 4 concrete serializers + Serializers registry + SerializeOptions / DeserializeOptions / ResolveOptions + call sites).
- `channel.WriteCore` → `Write`, `ReadCore` → `Read`, `AskCore` → `Ask` (across base + 6 channel subclasses). Public orchestrators `WriteAsync` / `ReadAsync` / `AskAsync` keep their suffix to mark them as the entry-with-events.

The two `Type` properties (`serializer.Type` for MIME, `data.Type` for PLang type-name) are namespaced by their owner — no ambiguity from context.

**Properties shape pivot: flat → nested.** The Stage 4 wire shape was originally flat — Properties entries emitted as top-level fields next to `name`/`type`/`value`/`signature`. Ingi pushed back on this as the load-bearing question before approving Stage 4. Walked through both shapes against four lenses (forward-compat for new reserved fields, self-documenting wire, future per-Property metadata, LLM-response ergonomics) and changed my position from flat to nested. Three of four favored nested; the LLM-response ergonomic case wasn't strong enough to outweigh them.

Ingi confirmed nested, with a sharpening: **no reserved-key rule needed.** The reason for reserved keys was the flat-shape collision risk (Property key clashing with a top-level reserved field). With Properties living inside their own `properties: { ... }` object, the keys are unconstrained — `"value"`, `"signature"`, anything is fine, because they live at `properties.{key}`, not at the root. Simpler validation surface, less code.

Updated:
- `plan.md` — Properties cross-cutting decision rewritten for nested shape; reserved-key rule removed; receiver-side rule changed to "unknown top-level fields ignored" (default STJ behaviour). The "follow-on simplifications" line in "What this is" updated to say Properties get a scope.
- `stage-4-properties-on-wire.md` — full rewrite. Title flips ("flatten to the wire" → "get a wire scope"). C# Properties type drops the reserved-key guard. Wire converter emits a nested `properties` object (omitted when empty). Read converter parses the five reserved fields and ignores unknown top-level keys. Design narrative re-argues for nested with the four-lens analysis.
- `stage-1-iserializer-data.md` — Extension/Type and Write/Read/Ask renames folded into scope and deliverables.
- `plan/wire-shape.md` — LLM-response example shows nested `properties` object. "What's NOT on the wire" updated for the five-field contract. Round-trip invariants updated.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer input tightened to Data + OBP renames](stage-1-iserializer-data.md) | partial — return half landed, input half + renames remain |
| 2 | [Merge plang serializers + sign-in-converter + canonicalization fix](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Properties get a wire scope](stage-4-properties-on-wire.md) | pending |
| 5 | [Vocabulary sweep](stage-5-vocabulary-sweep.md) | pending |

## 2026-05-27 — Design pivots from review session: sign-in-converter, Properties-on-wire

Review pass with Ingi produced three substantive design pivots and one new stage. Plan, Stage 2, Stage 3, wire-shape, and the stages table all rewritten to reflect.

**Pivot 1 — Signing.** Dropped the earlier "channel signs outermost" model in favor of *sign-if-missing during the wire converter's walk*. The unit of attestation becomes the Data node, not the wire boundary. Three properties fall out for free: forwarding preserves provenance (Bob's outer + Alice's inner survive a re-wrap), Compress signs automatically during byte-conversion (no separate explicit step), List<Data> elements each carry their own attestation. The "two signatures for one payload" concern I'd previously raised dissolved when I realized `EnsureSigned` is no-op-if-signed — there's no double-signing, just sign-once-per-Data.

**Pivot 2 — Canonicalization.** The earlier plan left a real gap: `crypto/Default.cs:20` hashes with default STJ options, which respect `[JsonIgnore]` on Signature. That means today's outer signature canonicalization strips the inner Datas' signatures from the hash, even though they get emitted on the wire (via Transport.ForOutbound). Result: wire-shape and hash-shape diverged. Stage 2 now includes the fix — canonicalize through the same Transport.ForOutbound-configured options the wire serializer uses, so hashed-bytes ≡ wire-bytes. After this, the outer signature transitively binds every inner signature.

**Pivot 3 — Properties flatten to the wire.** New design direction. Today `Properties : IList<Data>` is `[JsonIgnore]` and doesn't cross the wire. The new design changes both the C# shape (`Dictionary<string, object?>` of primitives) and the wire emission (each entry becomes a top-level sibling of `name`/`type`/`value`/`signature`). Two access operators: `%x.field%` reads Value, `%x!key%` reads Properties — disjoint namespaces, no collision possible. Reserved-key check forbids Properties from using the four reserved names. The sign-if-missing walk skips Properties (they're primitives, not Data — nothing to recurse into). New Stage 4 carries this work; vocabulary sweep slides to Stage 5.

The settled scope and the trade-offs sit in `plan.md`'s Cross-cutting decisions. Stage 2 absorbs the canonicalization fix because it shares the merged plang serializer's options. Stage 3's "no signing inside Compress" framing was wrong — rewritten to acknowledge that Compress's byte-conversion implicitly signs via the converter, and that this also fixes today's bug where `_envelopeJsonOptions` strips Signature from compressed bytes.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer input tightened to Data](stage-1-iserializer-data.md) | partial — return half landed, input half remains |
| 2 | [Merge plang serializers + sign-in-converter + canonicalization fix](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Properties flatten to the wire](stage-4-properties-on-wire.md) | pending |
| 5 | [Vocabulary sweep](stage-5-vocabulary-sweep.md) | pending |

## 2026-05-27 — Merged runtime2; Stage 1 narrows to input-tightening only

Merged `origin/runtime2` into `data-serialize-cleanup` cleanly (no conflicts). The `typed-action-returns` work that landed in runtime2 includes commit `5b1b894c4 coder: Serializers/ISerializer return Data instead of bare T?` — that commit shipped the *return* half of Stage 1: every `ISerializer` method now returns `Data` / `Data<T>`, parse and serialize errors travel through `Data.Error` instead of throwing, and all known call sites already read `.Success` / `.Value` / `.Error`. The wrapping `try/catch` over `JsonException`/`NotSupportedException`/`IOException` is in place on Json, Text, plang/this.cs and plang/Data.cs.

What's still required after the merge:

- **Stage 1's input half** — `ISerializer` still accepts `object? value` with an optional `Type? type = null`. Tightening to a single `Data data` parameter (and dropping `Type` from `Deserialize(string, Type)`) is what closes the polymorphic-input gap and lets `Stream.WriteCore` stop stripping the wrapper. The original v1 plan to also drop `DeserializeAsync<T>` is reversed — `typed-action-returns` ships `Data<T>` as the contracted shape and the generic stays as the PLang-shaped surface.
- **Stages 2, 3, 4 — unchanged.** Envelope class still present, signing still inside serializer, Compress still double-wraps `Data{archived, Data{gzip, bytes}}`, "envelope" still in vocabulary. All targets in `stage-2-plang-merge.md`, `stage-3-flat-compress.md`, `stage-4-vocabulary-sweep.md` still apply as described.

Plan updates:
- `plan.md` — problem #3 annotated with the partial-credit note; the "ISerializer takes Data, returns Data" decision section now distinguishes done vs. remaining; stage index marks Stage 1 as "partial".
- `stage-1-iserializer-data.md` — rewritten. Adds a "What's already done" section enumerating the merged work; narrows scope to input-side; updates interface signature (return types match what runtime2 shipped, input types tighten). The `DeserializeAsync<T>`-drop bullet is reversed.
- `stage-2-plang-merge.md` — touch-up only. The code example for the merged plang serializer now returns `Task<Data>` (matching post-Stage-1 reality) and includes the try/catch shape that mirrors today's failure-path discipline.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer input tightened to Data](stage-1-iserializer-data.md) | partial — return half landed, input half remains |
| 2 | [Merge application/plang serializers + drop Envelope + signing moves](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Vocabulary sweep — drop "envelope" + rename this.Envelope.cs](stage-4-vocabulary-sweep.md) | pending |

## 2026-05-26 — Initial plan: Data serialization cleanup

Designed the variable→wire→variable round trip and carved four stages. The core reframe is that **Data is the universal currency in PLang's channel boundary**, which collapses every other ambiguity in the current code: ISerializer's `object?` polymorphism, the parallel `Envelope` class, the duplicated STJ options, the strip-wrapper-then-rebuild-wrapper choreography in `Stream.WriteCore` → `plang/Data.cs`, and the double-wrap in `Compress`.

The wire shape is flat: `{name, type, value, signature}`. Nesting through byte-decoding layers (decompress → another serialized Data), not JSON object nesting. Signing moves from the serializer to `Channel.WriteCore` — channels sign because their writes are externally visible, in-pipeline transforms don't sign because their output stays in-process. "Only outermost signs" stops being a recursion rule and becomes a caller-type rule, automatic by construction.

The work is settled enough to hand to test-designer; the design conversation with Ingi covered the round-trip semantics, the role of `name` as the addressability coordinate that survives transport, and the position of signing in the call sequence.

**Follow-up identified: structural normalization (option 3).** A later design conversation explored how PLang would support non-reflection formats (protobuf, MsgPack, CBOR). The answer is to tighten Data.Value's contract — primitive | byte[] | Data | List<> — and add a `Normalize()` step that walks any C# object into this uniform tree once, at the boundary. Format encoders become trivial walkers (no per-format reflection). The wire shape stays compact: bare primitives ride in the parent's value slot when the parent's `type` covers them ("list&lt;int&gt;" makes `[1,2,3]` bare); Data wrappers appear only when a name or signature needs a home. This is intentionally NOT in this branch — it's a contract change for Data and deserves its own branch (`data-normalize`) on top of this cleanup. Created as a placeholder with a starter plan.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer tightened to Data](stage-1-iserializer-data.md) | pending |
| 2 | [Merge application/plang serializers + drop Envelope + signing moves](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Vocabulary sweep — drop "envelope" + rename this.Envelope.cs](stage-4-vocabulary-sweep.md) | pending |
