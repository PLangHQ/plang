# v1 Review Summary (Code Analyzer Feedback)

## Critical
1. **get.cs:24 — `Identity.Update()` on by-name fetch** — Overwrites `%MyIdentity%` with whatever identity was fetched by name, even non-default. Must only update when fetching the default.

## Needs Work
2. **Duplicate auto-create logic** — `IdentityData.ResolveDefault()` and `Get.Run()` both contain identical auto-create-default code. Should be consolidated.
3. **types.cs:99-101 — Double `TryGetValue("Created")`** — Redundant second lookup.
4. **types.cs:10 — Not sealed** — Should be `sealed class`.
5. **rename.cs:32-36 — Non-atomic rename** — Remove-then-save risks data loss if save fails. Should save-first-then-remove.

## Test Gaps
6. **export.cs:27-33** — No test for `Export { Name = null }` (default fallback path).
7. **types.cs:106-114** — No test for JSON round-trip Deserialize fallback.
