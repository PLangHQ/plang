# codeanalyzer summary (fast-apps)

**Version:** v4

**What this is:** The fourth fast-apps review (decisions 81–85, stage 6, tip 02aff152e) and the first bench row I recorded. The 23 production files changed since review3 were read whole against review3's findings; stage 6 (56 packages out of `PLang.csproj`, `Sdk.Web` → `Sdk`, the test projects' runtime identifier) was checked for removed namespaces still named in source. Read only. Report: `.bot/fast-apps/codeanalyzer/review4.md`.

**What was done:** Both blocking findings of review3 are fixed at the root (the reader's `Shipped` is non-null in both kinds; the call frame past the depth is refused before it is made). No new blocking by the branch's rules. Open: `Registry.Add(kind)` holds, then runs a full `Guard()`, three times per root birth (the likely cause of the root birth's +110 KB, outside its band); setup now adds every `.pr` it reads; `QRCoder-ImageSharp` is a hidden dependency; small names. No removed package's namespace is named in source (`WebUtilities` and `Nethereum.Util` stay). Bench (`Tools/Bench/run.sh`, load 16): held memory flat, sub birth −12 KB, request apps lower, `plang --test` 5.6 s with the same counts, sweep 37.3 s, incremental build 9.1 s on the rerun; the clean build (90.5 s, rerun 74.9 s at load 13.7) is not settled and needs a quiet-machine run.

**Next:** the coder fixes `Add(kind)`'s order and re-measures the root birth; someone runs the build row at load under 3.

**Code example:**
```csharp
// holds first, then guards: a refused kind stays held, and the origin's three schemes pay three full scans   type/list/Registry.cs:229
if ((Hold(kind) ?? Guard()) is { } refused) throw new AppException(refused);
// root: Guard checks the incoming kind before it is held (or Add undoes the hold), and the schemes go in as one batch
```
