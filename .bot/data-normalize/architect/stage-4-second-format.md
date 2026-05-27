# Stage 4: Second Format Proof — Protobuf (or MsgPack) Adapter

> **Note for coder:** every code snippet, type signature, library choice, and file path in this file is a **suggestion** that captures architect intent — not a contract. You own the implementation. Reshape, pick a different library, restructure layout, or replace approaches as the real constraints demand. Push back on the design itself if you find it wrong.

**Goal:** Ship a second `IWriter` implementation in a non-reflection format. This is the load-bearing proof for the whole branch — if a protobuf (or MsgPack) writer slots in cleanly alongside `JsonWriter` with no changes to Normalize, As<T>, or any domain type, the architecture works. If it requires hacking Normalize or special-casing per-format in the domain types, the architecture has a leak and we revisit.

**Scope:**
- One concrete `IWriter` implementation in a non-reflection format. Recommended: protobuf (the original motivator) or MsgPack (simpler, good interim proof). Coder's call — pick the one that's least painful to wire while still proving the point.
- Reader (deserialize) side for the chosen format — the round-trip has to work to count as a proof.
- Channel / content-type registration so the new format can be selected from the existing channel-serializer plumbing.
- Feature flag (config or env var) so it ships dark — the JSON path stays default until the format is exercised in real workloads.
- Debug-mode toggle, if not landed in Stage 2 (this is the natural place).

**Dependencies:** Stages 1–3 (Out discipline, Normalize + IWriter + JsonWriter, As<T> as tree-walker).

**Out of scope:**
- A third format. One proof is enough; protobuf + MsgPack are not both needed.
- Cross-language schema export (PLang doesn't generate `.proto` files for non-PLang consumers — that's a separate future concern).
- Performance tuning beyond "doesn't obviously regress JSON path."
- Stream-mode encoding for huge payloads.

**Deliverables:**

1. **`<Format>Writer : IWriter`** implementation. Wraps the chosen format's writer API (e.g. `Google.Protobuf.CodedOutputStream` or `MessagePack.MessagePackWriter`). Implements every method of `IWriter` Stage 2 defined. Domain types pass through unchanged — they don't know which writer is active.
2. **`<Format>Reader`** (or whatever the read-side equivalent is). Reverse direction: format bytes → normalized tree (the same shape Normalize produces) → `Data.As<T>` from Stage 3 reconstructs.
3. **Content-type registration.** Existing channel plumbing recognizes the new MIME type (`application/x-protobuf` or `application/msgpack`) and routes to the new writer/reader.
4. **Feature flag.** A config knob or env var (`PLANG_WIRE_FORMAT=protobuf`?) selects the active format on a channel; default stays JSON. Coder picks the cleanest mechanism that fits the existing channel config.
5. **Round-trip parity test.** A small set of domain values (a primitive Data, a Data<path>, a Data<List<Data>>, a Data<Identity>) round-trip through both JSON and the new format and produce semantically equivalent `Data` on the other side. This is the test that proves the architecture, not a per-byte equivalence test.
6. **Debug-mode bypass** wired in if not done in Stage 2. View.Debug = serializer skips `[Out]` filter (still excludes `[Sensitive]` and the `settings` type).

## Design

This stage is mostly mechanical *if* Stages 1–3 landed cleanly. The IWriter interface from Stage 2 is the load-bearing contract — the protobuf/MsgPack writer is just a different implementation of the same methods.

**Why protobuf is the better proof.** It's the original motivating use case ("how does a non-reflection format encode it?"), it's strict about types (so any leak in Normalize's output shape surfaces immediately), and there's deployment demand for it. MsgPack is friendlier (self-describing, no schema file) but doesn't stress the architecture as hard. If protobuf wires in too painfully due to needing field numbers, MsgPack is the fallback — and the proof still holds.

**On field numbers (protobuf).** Protobuf requires stable per-property field numbers. The wire-out-attributes inventory in `plan/wire-out-attributes.md` is the obvious starting point — assign each `[Out]` property a stable number per type. Coder owns the mechanism (attribute? source generator? convention based on declaration order?). Bear in mind: once shipped, field numbers can never change. Be conservative; leave gaps for future properties.

**On MsgPack.** No field numbers, but the writer needs to know how many properties it'll emit before emitting (`WriteMapHeader(N)`). That means Normalize's output for a record needs to be enumerable-with-count, not a streaming sequence. The `BeginRecord(Data)` method in IWriter probably already needs to know the count — design Stage 2's interface with that in mind (the suggested shape in stage-2-normalize-jsonwriter.md already implies this).

**On the round-trip test.** Don't go for exhaustive — pick 4–6 representative domain values that stress the interesting cases: primitive, nested Data, list-of-Data, a path (the type that broke the wire shape), an Identity (the type with `[Sensitive]` + `[LlmBuilder]`). If those round-trip cleanly through both formats, the architecture's case is made.

**On what "feature-flagged" means.** The new format ships disabled. Channels keep using JSON. Anyone who wants to try the new format flips the flag on their channel config. After a release or two of soak time, the flag flips to default-on; eventually it's not a flag, it's the default with JSON as the legacy option. The flag isn't permanent.

**If the architecture leaks.** If you find yourself adding format-specific code paths inside Normalize, As<T>, or any domain type — stop and raise it. The whole point of this stage is to validate that the boundary holds. A leak means we missed something in Stage 2's IWriter shape, and that's the architect's problem to fix, not yours to hack around.
