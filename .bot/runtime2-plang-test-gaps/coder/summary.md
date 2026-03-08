# PLang Test Gaps — Coder Summary

## v1
Added 33 new PLang integration test suites (64 total, all passing). Fixed 3 runtime bugs: test runner root directory resolution, setup goal execution before tests, and condition handler type mismatch. Found and documented builder inconsistencies with parameter naming and onError generation. See [v1/summary.md](v1/summary.md) for details.

## v2
Fixed tester v1 findings: resolved C# build failure (DiscoverAsync tests → RunAsync), added 7 PrPath keying tests and 2 convention discovery tests, enforced strict Goal.Path requirement in Goals.Add() with 60+ test sites fixed across 8 files. Exposed and fixed 3 bugs: Names property returning PrPaths, Get() variations not searching Names, GetByPrPathAsync missing _goals check. 1509/1509 C# tests passing. See [v2/summary.md](v2/summary.md) for details.
