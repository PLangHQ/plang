# v1 Review Summary

Tester v1 found 3 major findings (severity initially rated minor, corrected after user challenge):

1. **Engine.Providers** — 60% untested public API (Get/Has/Remove). Deletion test: could delete 3 methods without failing any test.
2. **False-green on JSON serialization** — `Hash_ObjectInput_SerializesToJsonBeforeHashing` checked consistency only, didn't prove JSON serialization happened.
3. **Algorithm override unverified** — `Hash_ExplicitAlgorithm_OverridesDefault` only checked name + length, not that hash value differed between algorithms.

Coder fixed all three in commit `acf4f0e0`:
- Added `ProviderRegistryTests.cs` (9 tests covering all 5 methods)
- Rewrote serialization test with known-value anchor via `DefaultProvider.Hash(JsonSerializer.Serialize("hello"))` reference
- Rewrote algorithm test to hash same input with both algorithms and assert hashes differ
