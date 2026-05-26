# auditor — summary

**Version:** v1
**Verdict:** **FAIL** — 1 MAJOR review-gap finding.

## What this is

Branch `purge-systemio-from-actions` bans `System.IO.*` reaches from action
handlers and lifts every disk touch through `path.@this` verbs → AuthGate.
The PLNG002 source generator enforces it at compile time (error severity).
The architect+test-designer+coder+codeanalyzer+tester+security pipeline
green-lit v2. Auditor was last in line for a second-opinion pass.

## What was done

Read the three prior bots' reports, then did the three things they didn't:

1. **Clean rebuild + re-run both suites.** C# 3031/3031 pass (tester said
   3025 — they grew the suite; clean rebuild reproduces). **PLang
   `--test` runs 204/206, not 206/206.** Tester's headline number is
   wrong on a full-suite rebuild.

2. **Bisect.** The 2 failing tests (`Builder/CompileLlmNotes/output-write-no-channel.test.goal`
   and `assert-equals-no-message.test.goal`) regress at commit `064724fda`
   — the F1 canonicalization security fix. Both fail with `Channel
   'input' has no interactive answerer (stream EOF)`, the signature of an
   AuthGate prompt firing against the test runner's non-interactive
   channel. F1 is the right shape (closes a real HIGH-severity vuln); the
   test setup needs to align with canonical semantics.

3. **PLNG002 suppression audit.** Grepped for `pragma warning disable PLNG`,
   `[SuppressMessage(...,"PLNG002")]`, csproj `NoWarn` entries, and
   `.editorconfig` overrides — zero hits. The two carve-outs (`IsPathHelperFile`
   and `IsPathTypeSurface`) live exclusively inside `Plng002.cs`. The
   per-file exemptions security v2 retired (`MarkdownTeaching.cs`,
   `app/this.cs`) are gone and stay gone.

## Code example

The regression's introducing change — F1 fix at `PLang/app/types/path/file/this.cs:25–47`:

```csharp
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

`PathHelper.GetFullPath` resolves `..` segments before storing
`_absolutePath`, so `IsInRoot`'s textual prefix-check is no longer
bypassable. The CompileLlmNotes tests' first step:

```
- copy file '../../Simple/.build/start.pr' to 'start-out.txt', overwrite
```

was riding the un-canonicalized prefix-match into the silent fast-path.
After F1, the resolved absolute falls outside the prompt-free zone — the
copy escalates to an AuthGate prompt — the test runner has no interactive
input — `Channel 'input' has no interactive answerer (stream EOF)`.

## Files

- `.bot/purge-systemio-from-actions/auditor/v1/plan.md` — review plan
- `.bot/purge-systemio-from-actions/auditor/v1/result.md` — detailed findings
- `.bot/purge-systemio-from-actions/auditor/v1/verdict.json` — FAIL
- `.bot/purge-systemio-from-actions/auditor-report.json` — structured finding

## Next

Coder fixes the test-side relative path (likely `../../../Simple/...`,
verify by reading `Goal.GetRuntimeDirectory()` for these tests) OR
adjusts the runtime-dir anchoring in `file.@this.Resolve` lines 88–106.
Rebuild the .pr, rerun the full suite from a clean `bin/`/`obj/`, get
**actual** 206/206 before re-handing to auditor.
