# v1 review summary — what Coder addressed

Coder's commit `f5b4ae3a7` lands F1's fix:

- `WireJsonConverter.cs`: `MaxReadDepth = 64` const + `AsyncLocal<int> _readDepth` counter. Increment at top of `Read`, throw `JsonException` past 64, decrement in `finally`. `Read` body extracted into private `ReadBody` so the counter discipline is single-source-of-truth at the entry point.
- `PLang.Tests/App/Serialization/WireConverterDepthBombTests.cs`: 3 tests — 16-level sanity round-trip, 200-level depth-bomb on both string-entry and stream-entry, both expected to surface `PlangDeserializeError`.

C# suite: 3232/3232. PLang suite: 228/228.

F2/F3/F4 (Low/Info) left tracked as standing open items in `security-report.json` per the original verdict.
