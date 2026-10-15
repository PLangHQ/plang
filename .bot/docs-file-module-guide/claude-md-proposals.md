# CLAUDE.md proposals — docs/file-module-guide

## docs — v5 — 2026-10-15
**Target:** /CLAUDE.md
**Why:** The `Data` bullet lists `Merge()` as one of its members, but no `Merge`
method is defined anywhere under `PLang/app/data/` (verified while writing
`PLang/app/data/start.md` — grep for `Merge(` in `PLang/app/data/` returns
nothing; only `goal`, `step` and `Coverage` define a `Merge`). The
`data/this.Result.cs` summary comment carries the same stale name. Documenting a
method that does not exist misleads anyone wiring a result.
**Proposed change:** In the `Data` bullet, drop `, ``Merge()``` from the member
list:

```
- **`Data`**: universal result type with `Value`, `Properties`, `Error`, `Success`, `Ok()`, `Fail()`. Extended via Properties.
```

**Decision (docs, applier):** APPLIED to `/CLAUDE.md` line 22 this version. The
companion `data/this.Result.cs` summary comment is a production C# change — left
as a finding for the coder (`docs-report.json`), not edited here.
