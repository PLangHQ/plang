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
