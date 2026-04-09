# Docs v1 Plan — Crypto Module Documentation

## Context

Auditor v1 PASS, security v1 PASS, tester v4 PASS, codeanalyzer v2 PASS. All reviewers approved. This is the final gate before merge.

The crypto module adds:
- `Engine.Providers` — type-keyed pluggable provider registry (new infrastructure)
- `crypto/hash` and `crypto/verify` action handlers
- `ICryptoProvider` interface + `DefaultProvider` (Keccak256, SHA256)
- `HashedData` result type
- 6 PLang test goals + 26 C# tests + 16 identity error path tests

## Documentation Gaps to Fill

### 1. XML Doc Comments (C# files)
- **`hash.cs`** — No XML docs on `Run()`, `SerializeData()`, `FormatHash()`, `ResolveProvider()`. Properties `Data` and `Algorithm` undocumented.
- **`verify.cs`** — No XML docs on `Run()`. Properties `Data`, `Hash`, `Algorithm` undocumented.
- **`types.cs`** — `HashedData` class undocumented.
- **`ICryptoProvider.cs`** — Interface and methods undocumented.
- **`DefaultProvider.cs`** — Class undocumented (methods have clear signatures but no docs on supported algorithms).
- **`Engine/Providers/this.cs`** — Already well-documented. No changes needed.

### 2. Architecture Documentation
- **`modules.md`** — Add crypto module section (actions table + provider pattern).
- **`good_to_know.md`** — Add Engine.Providers pattern entry (how modules use pluggable providers).

### 3. CHANGELOG
- Write user-visible changes to `v1/result.md`.

### 4. Consistency Check
- Verify terminology (hash/verify, provider, algorithm) is consistent across code, docs, tests.
- Verify cross-references are valid.

## What I Will NOT Do
- Write PLang code or test goals (tester's job)
- Edit .pr files
- Change runtime behavior

## Deliverables
1. XML doc comments on crypto module public members
2. `modules.md` updated with crypto module section
3. `good_to_know.md` updated with Engine.Providers pattern
4. `v1/result.md` with CHANGELOG and findings
5. `docs-report.json`
6. `v1/verdict.json`
7. `v1/summary.md`
