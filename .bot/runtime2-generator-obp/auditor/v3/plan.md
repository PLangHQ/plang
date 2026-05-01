# auditor v3 — plan

## Scope

Single commit (`87d7f6be coder/v8`) addressing auditor/v2 finding #1.

## Checks

1. **Discovery flag detection** — does `isRawNameResolvable` correctly
   identify `T : IRawNameResolvable` on `Data<T>` slots? Edge cases:
   `Data<Variable>?` (nullable wrapper) — does the emission filter handle
   it correctly?
2. **Emission placement** — guard fires before Run(), in the right block
   ordering (after eager Provider resolution, after [IsNotNull], before
   `if (__resolutionError != null) return`).
3. **Test coverage** — does the new `MissingVariableNameTests.cs`
   parametrize over all 20 non-nullable `Data<Variable>` slots? Is the
   foreach exclusion correct (nullable, intentionally permissive)?
4. **Test pass at the assertion level** — do tests assert
   `Error.Key == "MissingRequiredParameter"` (the contract the fix
   restores)?
5. **Empty-string edge case** — pre-v7 used `string.IsNullOrEmpty`;
   post-v8 uses `== null`. Empty-string slot now passes through. Is this
   a contract gap worth flagging?
6. **Run full tests** — confirm 2570/2570 C# + 166/166 plang.
7. **Cross-cutting check** — did the fix accidentally introduce drift
   anywhere else (e.g., snapshot tests, cache tests)?

## Verdict outlook

If checks pass: PASS, hand-off to docs.
If empty-string gap is real and relevant: PASS with NIT.
If something else broken: FAIL back to coder.
