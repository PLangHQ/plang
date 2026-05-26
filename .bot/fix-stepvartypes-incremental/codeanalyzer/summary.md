# codeanalyzer — fix-stepvartypes-incremental

**Version:** v2 (post-merge re-validation)

## What this is

Branch `fix-stepvartypes-incremental` carries the `%var%` slot-description fix plus surrounding builder/web/test-report scaffolding. v1 PASSed; the branch was then caught up with `origin/runtime2` (merging the entire `purge-systemio-from-actions` body of work) plus a one-line coder follow-up. v2 re-validates the original eight-file C# diff against the post-merge state.

## What was done

Re-examined the eight v1-reviewed C# files against post-merge code (commits `434604399` merge + `be0ebf18a` coder fix). The merge brought in 65 C# files / 1525+ lines from `purge-systemio-from-actions`, which had already PASSed codeanalyzer/security/auditor on its source branch — not re-litigated here.

**Verdict: PASS (with one LOW finding).**

Key changes since v1:
- v1 pre-existing System.IO debt in `report.cs` is **closed** by the merge (now uses `path.@this.Resolve(...).WriteText(...)`)
- Coder's `.ToString()` fix at `test/run.cs:163` is mechanically correct and idiomatic
- `ResolveImage` in `OpenAi.cs` rerouted to `path.ReadAsDataUri()` (merge content, reviewed elsewhere)
- `Describe()` and markdown-root became async (merge content)
- v1 findings (cost math, event-binding lifetime, `Timings` OBP shape, `%var%` description) all intact

LOW finding (for coder if/when convenient):
- `PLang/app/modules/test/report.cs:40` — dead local `var app = Context.App;` left behind by merge cleanup. Trivial deletion.
- (Optional) `PLang/app/modules/test/report.cs:302` — hoist `new[] { '/', '\\' }` to a `private static readonly char[]` to avoid per-row allocation in `GroupBy`.

Files created:
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v2/plan.md`
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v2/report.md`
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v2/verdict.json`

## Code example — what the merge fixed

The v1 report flagged this pre-existing pattern in `report.cs` (line 40):

```csharp
// pre-merge — System.IO banned in production C#
var outDir = System.IO.Path.Combine(app.AbsolutePath, ".test");
if (!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir);
reportFile = System.IO.Path.Combine(outDir, "results.json");
await System.IO.File.WriteAllTextAsync(reportFile, content);
```

After merge it reads:

```csharp
var writeTarget = global::app.types.path.@this.Resolve("/.test/results.json", ctx);
// WriteText creates parent dirs via EnsureParentDir; AuthGate(Write)
// fast-passes in-root and prompts/denies otherwise.
var written = await writeTarget.WriteText(content);
if (!written.Success) return global::app.data.@this.FromError(written.Error!);
```

The canonical migration pattern for the System.IO ban.
