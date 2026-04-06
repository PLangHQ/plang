# v5 Plan — Security hardening + test gaps

Responding to security audit (12 findings), auditor v4 (findings #9-10), and tester v6 (6 findings).

## Scope

### Fix (code changes)

1. **Depth-limit all unbounded recursion** (Security #1, #2, #3, #7, #10)
   - `UnwrapJsonElement` / `UnwrapJsonObject` / `UnwrapJsonArray` (Data.cs) — add `int depth = 0`, max 128
   - `fromJson.cs` duplicate — delete it, call shared `Data.UnwrapJsonElement` instead (needs to become `internal static`)
   - `RehydrateNestedData` (Data.Envelope.cs) — add `int depth = 0`, max 128
   - `GetChild` (Data.Navigation.cs) — add `int depth = 0`, max 100
   - `ResolveVariablesInPath` (Variables.cs) — add `HashSet<string>` cycle detection
   - `Clr()` (Types/this.cs) — add `int depth = 0`, max 20

2. **Make Verified not freely settable** (Security #9)
   - Change `Verified { get; set; }` → `Verified { get; private set; }`
   - Add `internal void SetVerified(bool value)` for future crypto verification to call
   - Or make it computed from Signature presence + verification method

3. **Add zip bomb size limit test** (Auditor #9, Tester #1)
   - Create compressed payload that exceeds limit → assert DecompressError

4. **Add Data.Merge() tests** (Tester #3)
   - Merge two List<Data> values
   - Merge with null other value
   - Merge with non-List value (document current silent-drop behavior)

5. **Add decompress StatusCode assertions** (Tester #4)
   - Add `Error.StatusCode == 500` to existing decompress error tests

## Won't fix (by design per Ingi)

- Security #4 (system variable writes) — user is sovereign
- Security #5 (ObjectNavigator reflection) — user is sovereign
- Security #8 (Newtonsoft namespace) — low practical risk, library.load already gives RCE
- Security #6 (JsonStringNavigator size) — low priority, can do later
- Security #11, #12 — low severity
- Auditor #10 (Add/Remove race window) — benign, future Kind refactor solves it
- Tester #2 (concurrent thread safety test) — nice to have, not blocking
- Tester #5 (context propagation in rehydration) — edge case
- Tester #6 (numeric type widening) — known, GetValue<T> handles it

## Files to modify

| File | Changes |
|------|---------|
| `PLang/App/Engine/Memory/Data.cs` | Depth param on UnwrapJsonElement/Object/Array, make internal static |
| `PLang/App/Engine/Memory/Data.Envelope.cs` | Depth param on RehydrateNestedData, Verified → private set |
| `PLang/App/Engine/Memory/Data.Navigation.cs` | Depth param on GetChild |
| `PLang/App/Engine/Memory/Variables.cs` | Cycle detection on ResolveVariablesInPath |
| `PLang/App/Engine/Types/this.cs` | Depth param on Clr() |
| `PLang/App/actions/convert/fromJson.cs` | Delete duplicate UnwrapJsonElement, call Data's version |
| `PLang.Tests/App/Memory/DataTests.cs` | Zip bomb test, Merge tests, StatusCode assertions, recursion depth tests |

## Order of implementation

1. Depth limits (all 5 locations) + fromJson dedup
2. Verified property change
3. Tests for all the above
4. Existing test adjustments (StatusCode assertions)
