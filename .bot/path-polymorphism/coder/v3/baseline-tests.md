# v3 baseline — before touching code

Captured 2026-05-22, branch `path-polymorphism` HEAD `81200939`.

## Build
`dotnet build PlangConsole` — **0 errors**, 447 warnings (pre-existing nullable noise).

## C# — `dotnet run --project PLang.Tests`
- total **2875** / passed **2875** / failed **0** / skipped 0.

## PLang — `cd Tests && plang --test`
- total **203** / pass **202** / fail **0** / timeout 0 / **stale 1** / skipped 0.
- The 1 stale: `ContextVars2.test.goal` — pre-existing, documented (its `%!fileSystem%`
  assertion was removed with the Stage 8 wrapper deletion; `.pr` rebuild blocked by a
  pre-existing `plang build` breakage unrelated to this branch).

Matches codeanalyzer v1's reported baseline. Any green test that goes red is a v3
regression.
