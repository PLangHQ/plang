# Code Analyzer v2 — Re-review of Coder Fixes

## Scope
Only files changed in coder v2 (focus on fix-introduced code):
- `PLang/Runtime2/modules/identity/get.cs` — removed Update on by-name, delegates to GetOrCreateDefaultAsync
- `PLang/Runtime2/modules/identity/types.cs` — sealed, GetOrCreateDefaultAsync, fixed Created, removed dead code
- `PLang/Runtime2/modules/identity/IdentityData.cs` — delegates to GetOrCreateDefaultAsync
- `PLang/Runtime2/modules/identity/rename.cs` — save-first-then-remove
- `PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs` — 2 new tests

## Focus
- Does the fix actually resolve the bug? Trace the by-name path end-to-end.
- Does GetOrCreateDefaultAsync handle all edge cases that the two original paths handled?
- Is the rename save-first-then-remove correct under all failure modes?
- Are the new tests sufficient to prove the fixes?
