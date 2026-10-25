# codeanalyzer summary (fast-apps)

**Version:** v3

**What this is:** The third fast-apps review (decisions 75–78, tip f4f754455): review2's findings checked against the coder's fixes, and every file the branch touched read again (84 files; the 48 changed since review2 weigh most). Read only; not built or run. Report: `.bot/fast-apps/codeanalyzer/review3.md`. The speed row (`Tools/Bench/run.sh`) is not in: the architect asked for it after stage 6 lands.

**What was done:** Three readers read the files; I re-grepped the two blocking claims. All of review2's blocking findings are fixed at the root (`Registry.Under` is a loop again, the rule-35 members are gone, the store decides in-memory once, the hot getters read the field first). Two small new blocking findings came with the fixes: `type/reader/this.cs:50,77,80` tells the code's reader from a layer's by a null `Shipped` (decision 40 OPEN 2 again), and `call/list/this.cs:210-212` checks the stack depth after the frame is built, so a thrown overflow leaks a diff-capture scope (`call/this.cs:257-262`). Non-blocking: the goal index's key is mutable state, `Has<T>()` constructs the shipped provider, `system.cs` `Row` allocates per miss, residue around the goal stamp, older smells by file.

**Next:** the coder fixes the two blocking ones and the listed non-blocking ones; I run the bench after stage 6 and send the row.

**Code example:**
```csharp
// new blocking: a layer told from the code's reader by a null     type/reader/this.cs:50,77,80
private readonly Lazy<shipped>? _shipped;
private protected @this() { }
private shipped? Shipped => _shipped?.Value;
// root: Shipped answers non-null in both kinds (the layer passes an empty one, or the second table is a virtual)
```
