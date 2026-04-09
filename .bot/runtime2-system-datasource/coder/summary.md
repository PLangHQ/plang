# runtime2-system-datasource / coder

## v1 — Fix 6 Failing PLang Tests
Fixed `Variables.Get()` array/index navigation bug (+4 C# tests), rebuilt 3 stale `.pr` files (Retry, ListOps, SetMaxGzipSize), fixed `list.unique` returning wrapped type instead of raw list. PLang tests: 17/23 → 22/23. C# tests: 1465/1465. See [v1/summary.md](v1/summary.md).
