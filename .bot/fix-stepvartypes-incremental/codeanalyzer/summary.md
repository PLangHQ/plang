# codeanalyzer — fix-stepvartypes-incremental

**Version:** v1

## What this is

Branch `fix-stepvartypes-incremental` carries 39 commits beyond `runtime2`. Most are generated `.pr` files, `.goal` builder source, web UI (HTML/Python), and markdown teaching files. The narrow **C# production diff** covers:

- LLM cost computation + cached-token accumulation in `OpenAi.cs`
- Per-step timing capture + output capture in `test/run.cs` (via event bindings on the child App)
- New `Timing` record and `Timings` collection (`app/tester/`)
- `Run` entity rename `CapturedOutput`→`Output` and add `Timings` property
- `test/report.cs` extends results JSON with `output` + `timings`, switches to local `JsonSerializerOptions` with `IgnoreCycles` (the `AssertionError.Variables` graph reaches back into `App.CallStack`)
- `modules/this.cs` drops the misleading `"string"` suffix from `%var%` slot descriptions

The branch name suggests stepvartypes work, but the actual fix to that ("%var% string" → "%var%") is one line — most of the branch is the surrounding builder/web/test-report scaffolding that grew around it.

## What was done

Five-pass review of the 8-file C# diff. **Verdict: PASS.**

- Zero OBP violations introduced
- New `Timings` class is a textbook OBP shape (private list, narrow `Add`, `IEnumerable<Timing>` read)
- Cost math in `OpenAi.cs` is correct given OpenAI's `usage` semantics (`prompt_tokens` includes the cached portion → `Max(0, prompt - cached)` is the right non-cached input bucket)
- `Truncated`-path exit now correctly sets `Cost` + `CachedTokens` (previously missing)
- `ReportOptions = new(Format.Options) { ReferenceHandler = ReferenceHandler.IgnoreCycles }` — clones rather than mutating the shared options, exactly right

One pre-existing issue flagged but not gated on: `test/report.cs` still uses `System.IO.*` (banned by CLAUDE.md). Hits predate this branch (commit `e88eaee04`, Stage 8 rip-out of `IFileSystem`). Out of scope here.

Files created:
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v1/plan.md`
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v1/report.md`
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v1/verdict.json`
- `.bot/fix-stepvartypes-incremental/report.json` (new session entry)

## Code example

The cleanest pattern on this branch — `app/tester/Timings.cs` — shows what fixing an OBP smell-1 violation looks like (private collection, owned discipline):

```csharp
public sealed class Timings : IEnumerable<Timing>
{
    private readonly List<Timing> _items = new();
    public int Count => _items.Count;
    public void Add(int stepIndex, double ms) => _items.Add(new Timing(stepIndex, ms));
    public IEnumerator<Timing> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

And the matching one-liner in `OpenAi.cs` that earns its comment by explaining *why*:

```csharp
// Cost: prompt_tokens includes the cached portion, so bill cached
// separately and subtract it from the non-cached input bucket.
int nonCachedInput = Math.Max(0, callPrompt - callCached);
```
