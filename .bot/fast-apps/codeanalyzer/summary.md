# codeanalyzer summary (fast-apps)

**Version:** v2

**What this is:** A re-review of the coder's fixes for my v1 (decisions 63/65/66/74) and, under the boy scout rule (66), every file the branch touched read whole: 79 files, 16.4k lines, tip 5d942c7af. Read only; not built or run; the speed bench (`Tools/Bench`) is not in the tree, so no row this round. Report: `.bot/fast-apps/codeanalyzer/review2.md`.

**What was done:** Three readers read the files whole; I re-grepped the blocking claim. Fixed at the root: v1 1, 2, 3, 9, 10, 13, 15 and the `EnsureTable`/run-if-not-yet doors; partly: 5, 7, 12, 14, 16, 17, 18; ruled items checked as ruled. New blocking: `Registry.Under` answers a null for "the code's list has nothing under it", the shape decision 40 ruled out. The boy scout read also puts the rule-35 members only tests call (`Registry.Add(kind)`, `context.CreateChild/Clone`, `test/list` `Made`, the stream's `ReadAllTextAsync`) on the blocking list. Non-blocking: the store decides "in memory" per verb, `system.cs:45` back to `??=`, dead `DynamicData` checks, a delegate allocated per read of `Context.Setting`, older smells by file.

**Next:** the coder fixes the blocking ones and the cheap renames and deletions; the speed record joins when `Tools/Bench` lands.

**Code example:**
```csharp
// new blocking: a layer and the bottom told apart by a null     type/list/Registry.cs:70,67,78
private @this? Under => Chunks<@this>() is [var under] ? under : null;
private type? Named(string name) => _named.GetValueOrDefault(name) ?? Under?.Named(name);
// root: two kinds, as the reader has them (code asks its own rows; layer adds under.Named(name))
```
