# Tester v3 Plan — runtime2-builder2-signing

## What changed
Coder v3 changed `IdentityData.ResolveDefault()` from returning null on failure to throwing `InvalidOperationException`. Two throw paths introduced (lines 55-56, 61-62).

## What I'm checking
1. Tests pass
2. Both throw paths have tests
3. Test quality (exception message assertions, not just catch-all)
