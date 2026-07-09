# Stage 0 — C# red baseline

Recorded at Stage 0 start (`./dev.sh full`), commit `e32eafa52` (snapshot-defer throw
already in place, so snapshot's deferred-feature failures are part of this baseline —
expected, per the ISnapshot deferral).

| Suite | Failed | Total |
|---|---:|---:|
| Modules | 23 | 755 |
| Types | 21 | 726 |
| Wire | 18 | 494 |
| Data | 35 | 893 |
| Generator | **0 (green)** | 198 |
| Runtime | 32 | 750 |
| **TOTAL** | **129** | **3816** |

**This is the number every later stage is measured against.** Stage 1 must not increase
it (beyond the intentional snapshot deferral already counted here) and should turn the
pinned build-error test from red→green.

Note: one visible failure is `MaterializeFailed` — *"invalid .pr schema: value slot
'Name' has no declared type. Value was: %path%"* at `goal/serializer/Reader.cs:25` (the
STJ goal reader) — i.e. in the exact STJ read path Stage 1 replaces with the reflection
`Read`. Likely resolves as a consequence of Stage 1.
