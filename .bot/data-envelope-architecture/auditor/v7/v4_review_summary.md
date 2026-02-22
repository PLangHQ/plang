# Review of Auditor v4 Findings — Response Summary

## What happened since v4

Auditor v4 had 10 findings (3 major, 4 minor, 2 nit, plus 1 new major from fix review). Coder v5 addressed security hardening (depth limits on all 5 recursive methods, cycle detection, zip bomb test, Verified→private set, fromJson deduplication). Coder v7 added test-only changes (cycle detection tests, Clr depth boundary tests). Tester went through v7→v8→v9, approved.

## Finding-by-finding

1. **Thread safety (major)** — Fixed in earlier commit. ConcurrentDictionary for mutable collections, lock for _allKinds.
2. **SetValueDirect (major)** — Fixed in earlier commit.
3. **ServiceError DecompressError (minor)** — Fixed in earlier commit.
4. **Type.Compressible bypass (minor)** — Fixed in earlier commit.
5. **ContainsValue O(n) (minor)** — Accepted as-is.
6. **Newtonsoft attribute (nit)** — Fixed in earlier commit.
7. **Error.Key assertions (nit)** — Fixed in earlier commit.
8. **Zip bomb (major)** — Fixed in earlier commit. MaxDecompressedSize=100MB with chunked read.
9. **Zip bomb untested (major)** — **Fixed in v5.** `Decompress_ExceedsSizeLimit_ReturnsError` creates 110MB compressed payload, asserts DecompressError with StatusCode 500.
10. **Add()/Remove() race window (minor)** — Still open. Accepted as benign (nanosecond window, null return not corruption).
