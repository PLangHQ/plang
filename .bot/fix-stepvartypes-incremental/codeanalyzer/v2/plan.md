# Plan — codeanalyzer v2 on fix-stepvartypes-incremental (post-merge)

## Context

After v1 PASSed, the branch was caught up with `origin/runtime2` (commit `434604399`, merging 57 commits). The merge brought in the entire `purge-systemio-from-actions` body of work (65 C# files / 1525+ added lines). A subsequent coder commit (`be0ebf18a`) applied a one-line `.ToString()` fix to `test/run.cs:163` after the merge surfaced an API mismatch (`Goal.Path` changed from `string` to `path.@this` on runtime2).

## v2 scope

This is **not** a v1-review-response cycle (no review was filed against v1). It is a **post-merge re-validation**:

1. The `purge-systemio-from-actions` content already PASSed codeanalyzer/security/auditor on its source branch — not re-litigating.
2. Re-check the eight v1-reviewed files for any merge-driven changes that affect previous findings.
3. Verify the coder's `.ToString()` fix is mechanically and idiomatically correct.
4. Flag any **new** smells introduced by merge interactions.

## Files re-examined

The merge touched 5 of the 8 v1-reviewed files:

| File | Changed by merge? |
|------|-------------------|
| `PLang/app/modules/llm/code/OpenAi.cs` | yes — `ResolveImage` rerouted through `path.ReadAsDataUri()` |
| `PLang/app/modules/test/report.cs` | yes — System.IO replaced with `path.@this.Resolve(...).WriteText(...)` |
| `PLang/app/modules/test/run.cs` | yes — coder fix + Parent inheritance + `.ToString()` on Goal.Path |
| `PLang/app/modules/this.cs` | yes — `Describe()` and `ResolveMarkdownTeachingRoot` became async / `path.@this` |
| `PLang/app/tester/Run.cs` | no |
| `PLang/app/tester/Timing.cs` | no |
| `PLang/app/tester/Timings.cs` | no |
| `PLang/app/modules/builder/BuildResponse.cs` | no |
