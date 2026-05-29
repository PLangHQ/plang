# Tester v1 — builder-ergonomics

## What I'm testing

No `coder/` folder on this branch — work was driven from `user-feedback.md` (7 priorities)
with a `tester-handoff.md` instead of the usual coder plan/baseline. No `baseline-tests.md`
exists (process gap — noted, not held against a coder who didn't run as `coder`).

The shipped work:
- **C#**: foundational-channel snapshot mechanism removed; replaced by per-channel
  `IsExecuting` recursion guard (`channels/channel/goal/this.cs`, `channels/this.cs.Get`,
  `actor/this.cs`). P4 root-cause-first error chaining (`types/Conversion.cs`).
- **PLang**: builder routes build output through a named `"builder"` goal-channel
  (`Build.goal`, `EmitBuildEvent.goal`, `BuilderChannel.goal`, template). Confidence-per-step
  (P6) in the four LLM passes. `list<T>` schemas. Always-on EmitSummary. Plan.llm verb rule.
- **New test goal**: `Tests/ConfidenceCheck/UnknownVerb.test.goal` (the P6 reproduction).

## Approach

1. Clean rebuild (stale-binary trap) → done, exit 0.
2. Run C# suite (`dotnet run --project PLang.Tests`) and plang suite (`cd Tests && plang --test`).
3. Diff failures; classify regression vs environmental vs flaky.
4. False-green hunt on the new test goal + the channel-guard tests + the P4 test.
5. Mutation test the load-bearing channel recursion guard.
6. Read the committed `.pr` for `UnknownVerb` — does it match step text?

## Status: COMPLETE — verdict FAIL. See result.md.
