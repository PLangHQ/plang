# Stage 6 — a faster C# build (decision 64): result

Project files, build settings and dev.sh only.

## Where the time went (measured first, binlogs and MSBuild's performance summary)

| build | before | where |
|---|---|---|
| nothing changed | 7.3 s | evaluation of 11 projects + reference work (ResolveProjectReferences, RAR, the copy-local item lists, Playwright's copy target) |
| one PLang file touched | 15.5 s | PLang's Csc 8.2 s (of it the source generator 1.4 s); the rest the same overhead. A touch keeps the ref assembly, so no dependent recompiles |
| a change to PLang's surface (incl. internals, InternalsVisibleTo) | ~95 s at load 10 | all 10 projects recompile (278 s of Csc in total) |
| clean | restore 6 s + build 53 s (load 11) | |
| size | a test bin ~270 MB, PlangConsole 210 MB, a checkout 3.3 GB | the 56 unused packages' copies, and `runtimes/` for 22 platforms in every test bin |

## Changes, one cause each

| commit | change | measured |
|---|---|---|
| 37501ee53 | PLang references the 9 packages its code names (+ the IDisposableAnalyzers analyzer); 56 removed | a test bin ~270 → ~100 MB, PlangConsole 210 → 45 MB, PLang 133 → 7 MB; no-change build 7.3 → 4.0 s |
| 1a115d31e | PLang on Microsoft.NET.Sdk, not Sdk.Web (it names one ASP.NET type, a package) | no win in the noise (3.8–4.2 s at load 11); kept because it removes a framework the library doesn't use |
| 26c9e0b9c | test projects carry the build machine's native assets only (RuntimeIdentifier from the running runtime, output path unchanged); dev.sh restores when the test props change | a test bin ~100 → 30–42 MB; a checkout 3.3 → 1.5 GB |

## Bench rows (Tools/Bench/record.md)

- before (f4f754455, load 9.9): clean 45.9 s, incremental 11.5 s
- after packages (37501ee53, load 20.9): clean 65.4 s, incremental 10.6 s
- after (26c9e0b9c, load 15.8): clean 87.9 s, incremental 11.8 s

The clean rows were taken at load 10–21 with other sessions' builds running. Per the record's own note they
are a neighbour's numbers, not a change: they need a run at load < 3 before they mean anything. The
incremental rows (one file touched) are 10.6–11.8 s, inside the ±1.2 s band.

## Looked at, not done

- **skip the build when nothing changed** (a stamp in dev.sh): dev.sh refuses it on purpose (dev.sh:186, a
  stamp misfired and passed a stale binary off as fresh).
- **`-m` for dotnet msbuild**: no difference (4.1 vs 4.3 s); .NET 10's msbuild already uses every node.
- **the source generator's 1.4 s**: generator code, outside this stage's limits.
- **a surface change recompiling every test project**: inherent to InternalsVisibleTo + ref assemblies.

## A workflow cost found on the way

A no-change build right after `git rebase` (before every push) took 87–95 s: the rebase re-applies the local
commits, rewriting those files' timestamps, so every project recompiles. Not a build setting; a reason to
rebase only when the remote has code changes.
