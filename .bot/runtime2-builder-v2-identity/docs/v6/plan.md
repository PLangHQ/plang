# Docs v6 Plan — Identity Module Documentation

## Context

Coder v6 is complete. Auditor: PASS. Tester: APPROVED. Security: PASS. All 1649 tests pass. This is the final gate before merge.

## Gaps Identified

### XML Doc Comments
1. **Already well-documented** — All public types and methods in the identity module have `///` XML docs. `SensitivePropertyFilter`, `SensitiveAttribute`, `View.cs`, `Actor.cs`, `IdentityData.cs`, `KeyGenerator.cs`, and all 8 handlers have meaningful doc comments. No gaps here.

### Architecture Documentation
2. **`modules.md` missing identity module** — The built-in action handlers table doesn't list `identity`. Need to add it with all 8 actions.
3. **`good_to_know.md` missing identity patterns** — Three cross-cutting patterns need documenting:
   - `[Sensitive]` attribute and two-mode serialization (output vs storage)
   - `IdentityData` lazy resolution + sync-over-async safety
   - `%MyIdentity%` DynamicData registration pattern
4. **`README.md` file tree** — Missing the `identity/` folder in the `modules/` section and `SensitivePropertyFilter.cs` in Serializers.

### PLang User Documentation
5. **PLang .goal examples** — All 10 test stubs are placeholder-only. Blocked on builder prompt update. **Flag for tester** — not a docs blocker.

### Consistency
6. **Terminology** — Verify "identity" vs "Identity" consistency across code and docs.

## Plan

1. Update `Documentation/App/modules.md` — add identity module to the built-in handlers table with all 8 actions
2. Update `Documentation/App/good_to_know.md` — add [Sensitive] attribute pattern, IdentityData lazy resolution, %MyIdentity% DynamicData pattern
3. Update `Documentation/App/README.md` — add identity/ to file tree, add SensitivePropertyFilter to Serializers section
4. Write `v6/result.md` with CHANGELOG-style entry for user-visible changes
5. Write reports and verdict
