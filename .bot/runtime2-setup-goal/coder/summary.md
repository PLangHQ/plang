# runtime2-setup-goal / coder

## v1 — Setup.goal Run-Once Execution System
Implemented Setup object using `engine.System.DataSource` for persistence (table `"setup"`, key = step.Hash). Refactored Goal.RunAsync to delegate step iteration to Steps.RunAsync (OBP rule 5). Setup goals excluded from regular lookup. context.Setup propagates through goal.call. C# tests: 1474/1474, PLang tests: 23/23. See [v1/summary.md](v1/summary.md).
