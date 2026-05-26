## 2026-05-26 — Initial plan: Data serialization cleanup

Designed the variable→wire→variable round trip and carved four stages. The core reframe is that **Data is the universal currency in PLang's channel boundary**, which collapses every other ambiguity in the current code: ISerializer's `object?` polymorphism, the parallel `Envelope` class, the duplicated STJ options, the strip-wrapper-then-rebuild-wrapper choreography in `Stream.WriteCore` → `plang/Data.cs`, and the double-wrap in `Compress`.

The wire shape is flat: `{name, type, value, signature}`. Nesting through byte-decoding layers (decompress → another serialized Data), not JSON object nesting. Signing moves from the serializer to `Channel.WriteCore` — channels sign because their writes are externally visible, in-pipeline transforms don't sign because their output stays in-process. "Only outermost signs" stops being a recursion rule and becomes a caller-type rule, automatic by construction.

The work is settled enough to hand to test-designer; the design conversation with Ingi covered the round-trip semantics, the role of `name` as the addressability coordinate that survives transport, and the position of signing in the call sequence.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [ISerializer tightened to Data](stage-1-iserializer-data.md) | pending |
| 2 | [Merge application/plang serializers + drop Envelope + signing moves](stage-2-plang-merge.md) | pending |
| 3 | [Flatten Compress/Decompress](stage-3-flat-compress.md) | pending |
| 4 | [Vocabulary sweep — drop "envelope" + rename this.Envelope.cs](stage-4-vocabulary-sweep.md) | pending |
