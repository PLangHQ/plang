# Baseline tests — runtime2-channels @ 2c4d37f0

Captured before any coder code change. This is the reference for "what was green before."

## C# (`dotnet run --project PLang.Tests`, TUnit on .NET 10)
- total: 2809
- passed: 2721
- failed: 88
- skipped: 0

All 88 failures are test-designer stubs in `PLang.Tests/App/ChannelsTests/`
(`Stage1_*`, `Stage2_*`, `Stage3_*`, `Stage4_*`, `Stage5_*`, `Stage6_*`,
`Stage7_*`, `Stage8_*`, `Stage9_*`, `Integration/*`). Every failure body is
`Assert.Fail("Not implemented")`. **No non-stub failures**, so any C# test
that goes red because of my code is my regression.

## PLang (`plang --test` from `Tests/`)
- total: 206
- pass: 188
- fail: 0
- timeout: 0
- stale: 18
- skipped: 0

The 18 stale tests are test-designer's channel `.test.goal` stubs under
`Tests/Channels/<Behaviour>/Start.test.goal`, all bodied as
`- throw "not implemented"`. **No real plang failures**, so any plang test
that goes red is my regression.

## Build
- `dotnet build PlangConsole` after clean: 0 errors, 423 warnings
  (pre-existing CS8603/CS8604 warnings, mostly in generator output).

## Stale-binary trap

Cleaned `PlangConsole/bin`, `PlangConsole/obj`, `PLang/bin`, `PLang/obj`,
`PLang.Tests/bin`, `PLang.Tests/obj`, `PLang.Generators/bin`,
`PLang.Generators/obj`, then rebuilt before running both suites. The
`PlangConsole/bin/Debug/net10.0/plang` binary used for `plang --test`
matches the current commit.

## Definition of done per stage

- Stage's C# stubs go green.
- Stage's PLang `.test.goal` stubs are written, built, and pass (where
  applicable — some stages have no PLang surface).
- C# pass count never drops (other stages' stubs still red is fine —
  that's pre-existing relative to "before this stage").
- PLang `fail` stays 0; `stale` only drops as I write goals.
