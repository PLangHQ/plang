# v1 Review Summary

Code analyzer v1 found 7 issues. Coder v2 addressed all of them:

1. **Bug fixed:** get.cs no longer calls `Identity.Update()` on by-name fetch. Regression test added.
2. **Deduplicated:** `GetOrCreateDefaultAsync` on IdentityVariable, used by both `Get.Run()` and `IdentityData.ResolveDefault()`.
3. **Fixed:** Double TryGetValue for Created — single lookup with type branching.
4. **Fixed:** IdentityVariable sealed.
5. **Fixed:** Rename is now save-first-then-remove (atomic-safe).
6. **Test added:** `Export_NullName_ReturnsDefaultPrivateKey` covers default fallback.
7. **Removed:** Dead JSON round-trip Deserialize fallback.
