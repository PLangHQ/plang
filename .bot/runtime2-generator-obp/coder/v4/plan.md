# Coder v4 — close tester v3's 5 findings

## Goal

Fix the MAJOR toothlessness Ingi flagged in v2 and that recurred in v3's fix to v2. Specifically: `NoGeneratedHandlerExposesUnusedPublicMethod` (Pattern B) is named after the `__paramData/ParamData()` regression but its regex is anchored to `^\s*public\s+...` — the original `ParamData()` was `protected`. The test cannot catch the regression it claims to catch.

Fold in the 4 minor/nit findings while we're in the file.

## Phase 1 — Widen Pattern B + extract `IsOrphanMethod` + 3 synthetic tests + comment/string stripping (Findings #1, #2, #3)

These three are tightly coupled — once Pattern B widens to `protected`, the names checked include `Data` and `Error`, which are common enough that comment/string false-greens become a real risk. So we make all three changes together.

### Widen the declaration regex

`PLang.Tests/Generator/NoDeadEmissionTests.cs:140-142`. Rename `PublicMethodDecl` → `PublicOrProtectedMethodDecl`, change `^\s*public\s+...` to `^\s*(?:public|protected)\s+...`.

```csharp
private static readonly Regex PublicOrProtectedMethodDecl = new(
    @"^\s*(?:public|protected)\s+(?:async\s+|partial\s+|static\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*\(",
    RegexOptions.Multiline);
```

### Extract `IsOrphanMethod` helper

The cross-file scan loop in `NoGeneratedHandlerExposesUnusedPublicMethod` becomes a call to a testable internal helper:

```csharp
internal static bool IsOrphanMethod(
    string methodName,
    string allCallableSources,
    ISet<string> exemptions)
{
    if (exemptions.Contains(methodName)) return false;
    var callerPattern = new Regex(@"\b" + Regex.Escape(methodName) + @"\s*\(");
    return !callerPattern.IsMatch(allCallableSources);
}
```

This mirrors `HasReadOf` for Pattern A — a pure-string helper that synthetic tests can drive directly.

### Strip comments and string literals from `LoadAllCallableSources`

Critical once Pattern B widens. The literal text `Data()` appears in raw strings inside `PLang.Generators/Emission/Action/this.cs` (the emitter source) and in plenty of comments across the tree. Without stripping, those false-positively count as callers — and a future genuinely-dead `Data()` would silently pass.

Add a helper that strips, in order:
1. Raw string literals (`"""..."""`) — non-greedy
2. Verbatim string literals (`@"..."`) — handle `""` escapes
3. Block comments (`/* ... */`)
4. Line comments (`//...$`)
5. Regular string literals (`"..."`) — handle `\"` escapes
6. Char literals (`'...'`) — handle `\'` escapes

Applied to each source file before concatenation.

```csharp
private static string StripCommentsAndStrings(string src)
{
    src = Regex.Replace(src, @"""""""[\s\S]*?""""""", " ");          // raw strings
    src = Regex.Replace(src, @"@""(?:[^""]|"""")*""", " ");           // verbatim strings
    src = Regex.Replace(src, @"/\*[\s\S]*?\*/", " ");                 // block comments
    src = Regex.Replace(src, @"//[^\n]*", " ");                       // line comments
    src = Regex.Replace(src, @"""(?:\\.|[^""\\\n])*""", " ");         // regular strings
    src = Regex.Replace(src, @"'(?:\\.|[^'\\])'", " ");               // char literals
    return src;
}
```

### 3 synthetic regression tests for Pattern B

Mirror the `Heuristic_*` shape Pattern A uses. Drive `IsOrphanMethod` with synthetic source.

```csharp
[Test]
public async Task Heuristic_OrphanProtectedMethod_IsFlagged()
{
    // The v1 ParamData() regression: a protected method declared in a generated
    // handler with zero callers anywhere. Widening the declaration regex isn't
    // enough — IsOrphanMethod must actually return true for the orphan shape.
    var allCallers = "// nothing references ParamData() here";
    var exempt = new HashSet<string>();
    await Assert.That(IsOrphanMethod("ParamData", allCallers, exempt)).IsTrue();
}

[Test]
public async Task Heuristic_CalledMethod_IsNotFlagged()
{
    var allCallers = "void Caller() { var x = MyHelper(arg); }";
    var exempt = new HashSet<string>();
    await Assert.That(IsOrphanMethod("MyHelper", allCallers, exempt)).IsFalse();
}

[Test]
public async Task Heuristic_ExemptedMethod_IsNotFlagged()
{
    // Framework-dispatched methods (ExecuteAsync, SnapshotParams) have no callable
    // text reference but are wired by interface dispatch in App.Run.
    var allCallers = "// no caller text";
    var exempt = new HashSet<string> { "ExecuteAsync" };
    await Assert.That(IsOrphanMethod("ExecuteAsync", allCallers, exempt)).IsFalse();
}
```

Plus 1-2 stripping tests to pin the comment/string filter (synthetic, not against the live tree):

```csharp
[Test]
public async Task Strip_MethodNameInsideComment_DoesNotCountAsCaller()
{
    // Defense for Finding #3: `// Data()` in a comment must not register as a caller.
    var src = "// Data() is a helper provided by the generator\n";
    var stripped = StripCommentsAndStrings(src);
    await Assert.That(IsOrphanMethod("Data", stripped, new HashSet<string>())).IsTrue();
}

[Test]
public async Task Strip_MethodNameInsideStringLiteral_DoesNotCountAsCaller()
{
    var src = """var template = "Data()";""";
    var stripped = StripCommentsAndStrings(src);
    await Assert.That(IsOrphanMethod("Data", stripped, new HashSet<string>())).IsTrue();
}
```

### Apply stripping in `LoadAllCallableSources`

```csharp
private static string LoadAllCallableSources()
{
    var sb = new System.Text.StringBuilder();
    foreach (var dir in new[] { "PLang", "PLang.Tests", "PLang.Generators", "PlangConsole" })
    {
        var fullDir = Path.Combine(RepoRoot, dir);
        if (!Directory.Exists(fullDir)) continue;
        foreach (var f in Directory.GetFiles(fullDir, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            if (f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
            sb.AppendLine(StripCommentsAndStrings(File.ReadAllText(f)));   // <-- changed
        }
    }
    return sb.ToString();
}
```

### Update the live cross-file test to use the helper

```csharp
foreach (Match m in PublicOrProtectedMethodDecl.Matches(src))
{
    var name = m.Groups[1].Value;
    if (IsOrphanMethod(name, allCallableSources, _publicMethodCallerExemptions))
        orphans.Add($"{Path.GetFileName(path)}:{name}");
}
```

### Risk: live tree may surface real orphans now

Once we include `protected`, the live test will inspect `Data` (4 overloads) and `Error` in every generated handler file. They are intentionally always-emitted framework helpers and are called from user partial classes (e.g. `PLang/App/modules/event/skipAction.cs:21` calls `Data(Value?.Value)`). So at least one user partial calls each. Cross-file scan should find a caller for both → not orphans → green.

If empirically there's no caller for one of them (e.g. nothing in the codebase happens to call `Error(...)`), we have two choices:
1. Add the helper to `_publicMethodCallerExemptions` (with a comment explaining why — they're framework-emitted helpers always available to user partials).
2. Add a tiny "this is here so the helper isn't an orphan" reference somewhere appropriate.

I'll determine empirically during implementation. Lean toward option 1 — these helpers are framework-emitted by design, not regression candidates.

## Phase 2 — Document `--coverage` interaction (Finding #4)

`PLang.Tests/Generator/IncrementalCacheTests.cs`. Add a comment block at the top of the file:

```csharp
// Known limitation: the two `PipelineCache_*` tests below fail when this suite is
// run with `dotnet run --project PLang.Tests -- --coverage`. The Roslyn
// `CSharpGeneratorDriver` with `trackIncrementalGeneratorSteps:true` interacts
// poorly with coverage instrumentation — coverage hooks wrap generator pipeline
// lambdas and strip the tracked-step labels, so `runResult.TrackedSteps` does
// not contain the `ActionInfoFiltered`/`ActionInfo` keys.
//
// The tests pass cleanly without `--coverage`. Do not gate CI on coverage of
// this file. Tracked: tester v3 Finding #4.
```

No code change. Lowest-cost option per the tester.

## Phase 3 — Add cache-hit test for the unfiltered ActionInfo step (Finding #5)

Tester offers delete-vs-add. Recommend add — stronger contract that catches transform-step instability the post-Where step's value-equality hides.

`PLang.Tests/Generator/IncrementalCacheTests.cs`, mirror the existing `PipelineCache_RerunWithUnchangedSyntax_StepOutputsAreCachedOrUnchanged` against the unfiltered tracking name:

```csharp
[Test]
public async Task PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged()
{
    // Pre-Where step: catches transform-step instability that the post-Where step
    // hides via ActionClassInfo value-equality. If syntax-provider lambda regressed
    // to capturing non-deterministic state (e.g. a HashSet identity), this would
    // fail with `Modified` reasons even when ActionClassInfo's value is equal.
    var driver = CreateDriver();
    var compilation1 = MakeCompilation(MinimalSource);
    var driver1 = driver.RunGenerators(compilation1);
    var driver2 = driver1.RunGenerators(compilation1);   // identical input

    var result = driver2.GetRunResult().Results.Single();
    await Assert.That(result.TrackedSteps).ContainsKey(@this.ActionInfoTrackingName);

    var step = result.TrackedSteps[@this.ActionInfoTrackingName].Single();
    foreach (var (_, reason) in step.Outputs)
        await Assert.That(reason)
            .IsEqualTo(IncrementalStepRunReason.Cached)
            .Or.IsEqualTo(IncrementalStepRunReason.Unchanged);
}
```

If the unfiltered step has multiple invocations per re-run, adapt the iteration accordingly — I'll match the existing test's iteration shape during implementation.

## What stays unchanged

- Pattern A (`HasReadOf`) and its 5 `Heuristic_*` tests — tester confirmed honest.
- `PrivateFieldDecl` regex — tester confirmed correct.
- All v3 production fixes — tester confirmed via 4 empirical deletion tests.
- `EveryGeneratedPrivateFieldUsesDoubleUnderscorePrefix` — tester confirmed honest.
- All cycle-protection, OCE-asymmetry, and diagnostic-span tests — tester confirmed honest.

## Verification

1. `dotnet build PLang.sln -c Debug`
2. `dotnet run --project PLang.Tests` — expect 2456 + new tests = ~2462/2462 green.
3. **Empirical deletion test on the new Pattern B widening.** Mentally + empirically: revert `PublicOrProtectedMethodDecl` back to `public` only, run `Heuristic_OrphanProtectedMethod_IsFlagged` — should still pass since the synthetic test calls `IsOrphanMethod` directly. But in the live cross-file test, also confirm widening actually finds the protected helpers. Sanity-mutate: emit a fake orphan `protected` helper temporarily in the generator, build, run live test → should fail with that orphan listed. Revert.
4. `plang --test` — expect same 169/48/5 (pre-existing infrastructure failures unchanged).

## Out of scope

- The 22 deferred v1 findings from coder/v2's batched-cleanup list.
- Any production-code changes beyond what Findings #1-5 require. (None of #1-5 require production-code changes — they are all test-quality findings, except #5's recommendation which uses the existing `ActionInfoTrackingName` constant.)
- PLang infrastructure failures (signing, identity, fixtures) — pre-existing, unrelated.

## Files touched (planned)

**Modified:**
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` — Pattern B widened, `IsOrphanMethod` extracted, comment/string stripping, 3 synthetic regression tests + 2 stripping tests.
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — `--coverage` caveat comment block + new `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` test.

**Created:**
- `.bot/runtime2-generator-obp/coder/v4/v3_review_summary.md` (already created).
- `.bot/runtime2-generator-obp/coder/v4/plan.md` (this file).
- `.bot/runtime2-generator-obp/coder/v4/summary.md` (at session end).

**No production code is modified in v4.**

## Next step

Awaiting Ingi's approval on this plan before any test code changes.
