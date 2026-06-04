# Baseline (type-kind-strict, before any code change)

## C#
`dotnet run --project PLang.Tests` (no rebuild)
- total: 3803
- passed: 3696
- failed: 107  ← all new stubs from `PLang.Tests/App/TypeKindStrict/*` (`Assert.Fail("Not implemented")`)
- pre-existing failures: 0

## PLang
`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
- total: 263
- passed: 253
- failed: 0
- stale: 10  ← all `Tests/TypeKindStrict/*.test.goal` stubs (`- throw "not implemented"`)

## Build
- `dotnet build PlangConsole` — 0 errors, 254 warnings (pre-existing)
- `dotnet build PLang.Tests` — 0 errors, 163 warnings (pre-existing)

Floor is clean: anything failing after Stage 1 that isn't in the Stage 1 stub set is a regression I introduced.
