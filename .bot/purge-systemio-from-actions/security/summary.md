# security — purge-systemio-from-actions

## Version

v2. PASS verdict.

## What this is

Re-review after coder's response to v1. The branch's stated security
goal is to make `path.@this` verbs (gated by `AuthGate`) the single
chokepoint for filesystem access from action handlers. v1 verified the
chokepoint was in place but FAILED on F1 — the chokepoint had a hole
exactly the size of `..`. v2 re-verifies after the fix.

## What was done

Coder shipped two commits addressing all three v1 findings:

- **064724fda** — F1 + F3. FilePath ctor canonicalizes `_absolutePath`
  via `PathHelper.GetFullPath`; new `PathHelper.cs` is the single
  allowed bridge to `System.IO.Path.*`; PLNG002 narrowed to two
  carve-outs (`Path.*` → PathHelper.cs only; File/Directory/... →
  app/types/path/** only); no whole-file exemptions remaining.
- **987a5148e** — F2. MarkdownTeaching's 8 ungated System.IO reaches
  lifted to the `path.@this` verb surface; `MarkdownTeachingRoot`
  override routed through `path.@this.Resolve(System.Context)`.

### v2 verification

1. **Read both diffs** end-to-end.
2. **F1 coverage trace** — every FilePath construction site (Resolve,
   JsonConverter, implicit operator, derivation verbs, direct ctor)
   now inherits canonicalization. The three deliberate skips (empty,
   non-rooted, `//`-prefixed) reasoned safe in `v2/result.md`.
3. **F1 mutation test** — temporarily neutered `Canonicalize` to
   `return absolutePath`; all 3 regression tests in
   `PathTests/DotDotTraversalRegressionTests.cs` failed:
   - `FilePath_Ctor_Canonicalizes_RemovesDotDot`
   - `FilePath_Resolve_RelativeWithDotDot_FromGoalRuntimeDir_LeavesRoot`
   - `ReadText_RelativeDotDot_OutOfRoot_DeniedByAuthGate`

   Reverted; `git status` clean.
4. **F2 trace** — every disk touch in MarkdownTeaching.cs is a `path`
   verb. ResolveMarkdownTeachingRoot routes the string override
   through Resolve. PLNG002 carve-out for MarkdownTeaching dropped.
5. **PathHelper contract** — body is pure name math only.
   GetTempPath/GetTempFileName explicitly forbidden by class doc.
6. **PLNG002** — only two carve-outs remain, both visible at the use
   site. No whole-file exemptions.

## Verdict

**PASS** — F1, F2, F3 all closed. F1 mutation-verified. No new
findings. Recommend merging.

Files written:
- `.bot/purge-systemio-from-actions/security/v2/plan.md`
- `.bot/purge-systemio-from-actions/security/v2/v1_review_summary.md`
- `.bot/purge-systemio-from-actions/security/v2/result.md`
- `.bot/purge-systemio-from-actions/security/v2/verdict.json`
- `.bot/purge-systemio-from-actions/security-report.json` (updated)

## Code example (the fix shape)

```csharp
// PLang/app/types/path/file/this.cs:25-47
public @this(string absolutePath, ...) : base(Canonicalize(absolutePath), ...) { }

private static string Canonicalize(string absolutePath)
{
    if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
    if (!PathHelper.IsPathRooted(absolutePath)) return absolutePath;
    if (absolutePath.StartsWith("//")) return absolutePath;
    try { return PathHelper.GetFullPath(absolutePath); }
    catch { return absolutePath; }
}
```

Mutating this to `return absolutePath` unconditionally is what made
the F1 attack reproduce — and is what the regression suite now
catches. Single site, every construction path covered.

## Next bot

`auditor` — confirm merge-readiness across reviewer bots:
```
run.ps1 auditor path-class "Review the code on branch purge-systemio-from-actions" -b purge-systemio-from-actions
```
