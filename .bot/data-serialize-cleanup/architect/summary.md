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
