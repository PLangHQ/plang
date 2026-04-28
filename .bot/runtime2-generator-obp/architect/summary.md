# Architect — runtime2-generator-obp

- **v1** ([details](v1/summary.md)) — Design only. Plan to restructure `LazyParamsGenerator` into OBP shape: round 1 extracts the 11-way per-property `if/else` into a polymorphic `ActionProperty` hierarchy under `Emission/Property/`. Markers, `ExecuteAsync`, helpers, and snapshot stay procedural. Hard promise: byte-for-byte identical `.g.cs` output (regression harness pins current output as `golden/`). Plan approved by Ingi; ready for coder handoff.
