# fast-apps: eighth review — the last read before the merge (decision 103)

Tip 5665127ef (HEAD at the time of the read). Scope: the commits since review7 (a9262309e) and the earlier `49aaf8199`, with their files: `channel/list/this.cs` (49aaf8199), `dev.sh` (96340862e, 749e1430f), `type/item/image/this.cs` (f02ee4772), `module/identity/code/Default.cs` (c5a4b6549), f2f8750da (the grant test's saved probe untracked), 5665127ef (backlog notes). Read from the diffs and the whole sections they sit in; `Default.cs` and `image/this.cs` were read whole in review7. Read only. I ran `./dev.sh build` at the tip as evidence; it restored and built.

**Verdict: PASS. Nothing blocks the merge.**

## What I read

| Commit | Verdict | Where |
|---|---|---|
| 49aaf8199 `ChannelNames` gone | Pass | `channel/list/this.cs` loses two lines; no production caller (rule 35); the three Wire tests that used it read the list another way; `surface_golden.txt` loses its line, re-pinned with the decision |
| c5a4b6549 export reads once, makes nothing | Pass; fixes review7's note 1 and 2 | `ExportAsync` (`Default.cs:197-217`): one `LoadAll`, then `items.Find` by the name or the stored default; the origin's identity is never asked, no default is made to be given out. A missing name answers `NotFound` 404; no default of its own answers `IdentityNotOwn` 404 with a message true for a root app too ("this app holds no default identity of its own to give out"). The membership scan, the second store read and the "the app's that made this one" message are gone. Behaviour change worth knowing: an app that holds identities but none flagged default no longer promotes one on export (the old path went through `GetOrCreateDefaultAsync`); by design ("none is made") |
| f02ee4772 image `Width`/`Height`/`Probe`/`Sized` gone | Pass; closes review7's image notes | The size gate, its static `Sized` list, the two `int?` lazies and the advisory comment are removed with the tests that read them; nothing in `PLang` reads an image's size (the remaining `.Width`/`.Height` are the screen types' own); the image type runs no ImageSharp decoder (`ValidateKind` uses `DetectFormat`, a magic-byte sniff). The format-facts note (the type's formats vs a second list) went to the backlog (5665127ef) and, with `Sized` gone, no longer has a second list to drift |
| 96340862e, 749e1430f `dev.sh` restores | Pass; closes review7's tooling gap | `restore()` (`dev.sh:198-220`): restores when any project's assets file is missing, or any project file or `PLang.Tests/Directory.Build.props` is newer than dev.sh's own stamp `PLang.Tests/obj/devsh.restored`; the stamp is written only after a successful restore (`set -euo pipefail` and `scream_build` end the script before `touch` on a failure). **Verified:** with the stamp missing, `./dev.sh build` restored (3 s) and built the tip (0 errors, 107 s); the next `./dev.sh build` skipped the restore (4 s). A tree built before decision 89 now restores itself |
| f2f8750da | Pass | the grant test's saved probe question is untracked and ignored; `plang --test` no longer dirties the working tree (the file I had to `git checkout` in review7) |
| 5665127ef | Pass | backlog notes only |

## Not on the tip yet
The coder's "one-line `dev.sh` unset of the two link flags" (`CreateSymbolicLinksForCopyLocalIfPossible`, `CreateHardLinksForAdditionalFilesIfPossible`) is not in `dev.sh` at this tip (`grep` finds neither name); `Tools/Bench/run.sh` unsets them. It was announced as coming, so I did not wait for it: a symbolic-link copy of a test bin's DLL lands outside the app's root and `module.add` refuses it (the item-doors link rule), which showed in review decision 21 as six red `ModuleAddTests`. If it is not in before the merge, that failure returns on a container that sets the flags.

## New findings
None blocking. No new smell in the changed lines: no static added (the removed `Sized` was the one), no `caller == null`/`_parent` null, no flag at many doors, no `.Clr` added, no history or task comments added.

## Locked core and bench
Removed public members only: `channel.list.ChannelNames` (golden re-pinned, per the log), `image.Width`/`Height` (not core). Nothing added to a locked surface. The bench numbers from review7 stand: nothing in these commits touches the runtime path measured (birth, requests, held memory); the clean-build row is the tester's, on a quiet machine.

## Older, still standing
As listed in review3 and review7 (rule 66 older smells in the touched files): the identity slot at six doors (N10), `making`, the verb+noun private names and `Clr<string>()` density in `Default.cs`; the orphan `<summary>` and the throwaway `Open` Data in `image/this.cs`. All on the follow-up; none blocks.
