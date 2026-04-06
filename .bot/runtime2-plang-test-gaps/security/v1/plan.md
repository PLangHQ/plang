# Security Review Plan — runtime2-plang-test-gaps

## Scope

Review the 7 changed runtime C# files on this branch for security implications. Focus areas:

1. **Setup discovery change** — `DiscoverAsync` went from recursive `*.pr` scan to convention-based (2 fixed paths). Security improvement? Any gaps?
2. **Goals keying change** — `_goals` dictionary keyed by `PrPath` instead of `Name`. Collision resistance, input validation on `Path`.
3. **Goal return value propagation** — Steps/Goals now return last step result instead of `Data.Ok()`. Information disclosure through error propagation?
4. **Test runner root isolation** — Engine root changed from shared `rootDir` to per-test `dir`. Test isolation improvement.
5. **PrPath null/empty guard** — `string.IsNullOrEmpty` instead of null-only check.
6. **Bare catch in DiscoverAsync** — Pre-existing, but could swallow security-relevant exceptions.
7. **Linear scan in Get()** — `_goals.Values.FirstOrDefault(...)` performance with many goals.

## Approach

- Blue team: Map what changed, assess trust boundaries
- Red team: Try to construct attack scenarios for each change
- Rate against PLang threat model (user-sovereign, defend against external data)

## Files to Review

- `PLang/Executor.cs` — setup call consolidation
- `PLang/App/Engine/Goals/Goal/Methods.cs` — return value propagation
- `PLang/App/Engine/Goals/Goal/Steps/this.cs` — return value propagation
- `PLang/App/Engine/Goals/Goal/this.cs` — PrPath empty guard
- `PLang/App/Engine/Goals/Setup/this.cs` — convention-based discovery
- `PLang/App/Engine/Goals/this.cs` — PrPath keying, Get() variations
- `PLang/App/Engine/Test/this.cs` — per-test root isolation
