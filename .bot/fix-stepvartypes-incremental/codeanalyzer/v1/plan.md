# Plan — codeanalyzer v1 on fix-stepvartypes-incremental

## Scope

39 commits between `merge-base(HEAD, origin/runtime2)` and HEAD. Most touch generated `.pr` JSON, `.goal` source, web UI (HTML/Python), and markdown teaching files — out of codeanalyzer scope. The **C# production diff** is narrow:

- `PLang/app/modules/builder/BuildResponse.cs` — comment-only
- `PLang/app/modules/llm/code/OpenAi.cs` — cost computation + cachedTokens accumulation
- `PLang/app/modules/test/report.cs` — pass `output` + `timings` into JSON; local `JsonSerializerOptions` clone with `IgnoreCycles`
- `PLang/app/modules/test/run.cs` — register coverage / output / per-step-timing event bindings
- `PLang/app/modules/this.cs` — drop `"string"` suffix from `%var%` slot description
- `PLang/app/tester/Run.cs` — rename `CapturedOutput`→`Output`, add `Timings`
- `PLang/app/tester/Timing.cs` — new (record)
- `PLang/app/tester/Timings.cs` — new (collection)

## Passes

1. OBP (rules + shape smells)
2. Simplification
3. Readability
4. Behavioral reasoning (cost math, event-binding lifetime, output-buf interleaving)
5. Deletion test

## Out-of-scope notes

- `report.cs` has `System.IO.Path.Combine` / `Directory.Exists/CreateDirectory` / `File.WriteAllTextAsync` / `Path.GetDirectoryName` (banned by CLAUDE.md). All pre-date this branch (commit `e88eaee04`, Stage 8 rip-out of `IFileSystem`). Out of scope here — flag once at the top of the report so it isn't lost, but don't fail the branch on it.
