## 2026-05-26 — Initial plan: structural normalization (option 3)

Placeholder branch created off `data-serialize-cleanup`. Captures the design conversation that surfaced while working on the cleanup: how does PLang carry arbitrary objects to non-reflection formats (protobuf, MsgPack, CBOR)? Answer is structural normalization — `Data.Value`'s contract narrows to `primitive | byte[] | Data | List<>`, a `Normalize()` step walks any C# object into that uniform tree once at the boundary, and per-format encoders become trivial walkers (no reflection).

The plan captures the Value contract, the Normalize sketch, the bare-when-possible wire shape rule (primitives ride bare in their parent's value slot when the parent's `type` describes them), the As<T> reverse direction (tree-walker, not STJ-deserializer), and a rough five-stage outline.

**Not started yet.** Depends on `data-serialize-cleanup` merging first. Stages will be carved when work begins; the spine and plan are placeholders so the design conversation isn't lost.

Stage status: pending stage carve-out when work begins.
