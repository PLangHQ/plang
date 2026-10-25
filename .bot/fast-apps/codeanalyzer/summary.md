# codeanalyzer summary (fast-apps)

**Version:** v8

**What this is:** The last read before the merge (decision 103): the commits since review7 and `49aaf8199`, with their files. Tip 5665127ef. Read only; `./dev.sh build` at the tip as evidence. Report: `.bot/fast-apps/codeanalyzer/review8.md`.

**What was done:** PASS; nothing blocks the merge. `channel.list.ChannelNames` is gone with its tests; `identity.export` reads the app's own identities once and gives out only one it holds, never asking the origin and never making a default (closes review7's two notes); the image's `Width`/`Height`/`Probe`/`Sized` are gone with the size gate and the advisory comment (the image type runs no ImageSharp decoder); `dev.sh` restores on its own stamp when any project file or the test props changed (verified: restored once, built the tip, skipped the next run); the grant test's probe file is untracked. Not yet on the tip: the coder's `dev.sh` unset of the two link flags.

**Next:** the merge on Ingi's go; the follow-up branch takes the older smells and the clean-build row.

**Code example:**
```csharp
// export: one read, the named or the stored default; the origin's identity is never asked     module/identity/code/Default.cs
var (items, err) = await LoadAll(action);
var exported = name != null ? items!.Find(i => i.Name == name) : items!.Find(i => i.IsDefault && !i.IsArchived);
```
