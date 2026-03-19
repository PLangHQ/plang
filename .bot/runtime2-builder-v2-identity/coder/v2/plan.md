# Coder v2 Plan — Address Code Analyzer Feedback

## Fixes

1. **get.cs** — Remove `Identity.Update()` from the by-name path. Only update when returning the default identity.
2. **Deduplicate auto-create** — Extract `IdentityVariable.GetOrCreateDefaultAsync(engine)` static method. Both `Get.Run()` and `IdentityData.ResolveDefault()` call it.
3. **types.cs** — Seal the class. Fix double `TryGetValue("Created")`.
4. **rename.cs** — Reverse order: save new name first, then remove old. If save fails, old entry untouched.
5. **Add test: Export with null name** — Exercises default fallback.
6. **Remove JSON round-trip fallback** — Dead code with no test. DataSource returns Dictionary or primitives, never arbitrary objects needing round-trip.
