# codeanalyzer summary (fast-apps)

**Version:** v5

**What this is:** The fifth fast-apps review: review4's fixes (decision 86, tip d050b9f50) and the verdict for the branch. Nine production files changed since review4. Read only; not run. Report: `.bot/fast-apps/codeanalyzer/review5.md`.

**What was done:** Every review4 finding is fixed at the root: `Registry` guards a batch of kinds before any is held and reads the held formats only when a kind answers to a key (root birth back to 2,360 KB, inside its band), setup keeps only setup goals, `SixLabors.ImageSharp` is referenced directly, the names are done, the snapshot writes its lists once, the stamp holds the goal's own `PrPath`. One small blocking finding remains, and it is rule 35 coming back: the origin now uses the internal batch `Add`, so the public single-kind `Add(kind)` is called by tests alone. Not blocking: the collision rule is written three times, a refused later kind leaves earlier ones held, setup adds then removes, the overflow throw repeats in three `Push` overloads, the `ImageSharp` 2.0.0 version needs an advisory check. The clean-build row is still unsettled (load 12.7); it needs a quiet machine.

**Next:** the coder makes `Add(kind)` internal (a surface narrowing the architect takes to Ingi) or gives it a production caller on purpose; then the tester's pass and security.

**Code example:**
```csharp
// production adds kinds as one list; the single-kind door is left for tests alone     type/list/Registry.cs
public void Add(global::app.type.kind.@this kind) => Add([kind]);
internal void Add(IReadOnlyList<global::app.type.kind.@this> kinds) { … }     // origin/code.cs:18 is its only production caller
// root: Add(kind) internal (the tests see internals), re-pinned with the decision
```
