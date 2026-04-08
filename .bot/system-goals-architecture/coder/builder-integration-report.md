# Builder V2 Integration — Status Report

## What works
- `plang Test.goal` — hello world runs clean
- `plang Test.goal --debug` — debug fires for all steps including sub-goals
- All 1986 C# tests pass
- C#-driven execution: Goal.RunAsync → Steps.RunAsync → Step.RunAsync → Action.RunAsync with lifecycle events
- Builder PLang code exists in `system/builder/` with pre-built .pr files

## What's broken: `plang build`

### Symptom
Running `plang build` from `Tests/Runtime2/SystemGoalTest/` reports "Found 5 goals" and tries to build `ValidateBuildResponse` (a system builder goal). It should find 1 goal (Test.goal with 2 goals inside). It retries indefinitely on LLM validation failures.

### Root cause (suspected)
`App.Start()` at `PLang/App/this.cs:380` calls the builder via:
```csharp
var buildCall = new GoalCall { Name = "Build", PrPath = "/system/builder/.build/build.pr" };
return await RunGoalAsync(buildCall, context);
```

The builder's `Build.goal` does `GetGoalsV2, fileOrFolderPath=%path%` — the `%path%` variable needs to be the user's app directory. Currently `Executor.cs` sets `path` on user variables (`userVars.Set("path", fileSystem.RootDirectory)`), but the builder runs on the **system** context (since `App.Start()` uses `System.Context`). The system context can't see user variables.

### Issues to resolve

1. **Variable context mismatch**: Builder PLang code runs on system context but reads `%path%` which is set on user variables. Either `%path%` needs to be on system variables, or the builder needs to run on a context that can see user variables.

2. **Goal file scanning**: The builder's `GetGoalsV2` (`PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:50`) uses `file.List` with `*.goal` pattern recursively. If the path resolves to the system directory or includes system folders, it picks up builder goals too. Need to ensure it only scans the user's app directory.

3. **Builder context**: The builder is PLang code that calls LLM, reads/writes files, validates schemas. It needs:
   - `%path%` — user's app root
   - File system access to both user directory (read .goal files, write .pr files) and system directory (read templates, LLM prompts)
   - LLM access (API key, endpoint)
   - Correct working directory context

4. **Goal count**: "Found 5 goals" from a directory with 1 .goal file (Test.goal containing Start + WriteOut = 1 root goal after our Parse fix) suggests path resolution is wrong — it's scanning more than the user's directory.

5. **LLM retry loop**: `ValidateBuildResponse` is a system builder sub-goal. The builder shouldn't be trying to build its own goals — it should be building the user's goals. This confirms the path/scanning issue.

### Key files
- `PLang/App/this.cs:373-385` — App.Start() builder path
- `PLang/Executor.cs:48-79` — build mode setup, path variable
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:50-126` — Goals action (file scanning)
- `system/builder/Build.goal` — builder entry point
- `system/builder/BuildGoal.goal` — per-goal build logic
- `system/builder/.build/build.pr` — compiled builder

### Recommendation
The architect should design how the builder context works:
- Which actor/context does the builder run on?
- How does `%path%` flow from CLI to builder PLang code?
- How do we prevent the builder from scanning its own system/ directory?
- Should builder have a dedicated context that bridges system (for LLM/templates) and user (for .goal files)?
