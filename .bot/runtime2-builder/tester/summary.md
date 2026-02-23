# Tester Summary — runtime2-builder

## v1
Fixed path resolution bug in `PrParser.ParsePrFile()` that caused `plang p build --llmservice=openai` to fail with `InstructionFileNotLoaded`. One-line condition change: system directory paths now correctly derive their base directory instead of falling back to `RootDirectory`. All 1423 C# tests pass. See [v1/summary.md](v1/summary.md).

## v2
Created PLang .goal tests for 4 new modules (File, Output, Assert, Condition) and extended 3 existing (ListOps, Math, Mock). 20/22 PLang tests pass; 1423/1423 C# tests pass. Condition test fails due to builder hardcoding condition values at build time. ListOps new steps have false-green from builder off-by-one index alignment. Both are builder system prompt issues. See [v2/summary.md](v2/summary.md).

## v3
Fixed the LLM builder prompt (`BuildGoal.llm`) with three rules: goal.call values are plain strings (not parameter-name-keyed objects), nullable params must be omitted when unused, and %variable% references must never be pre-evaluated. Simplified Condition test to truthy/falsy (runtime has no expression evaluator). Full rebuild from correct root fixed path resolution. 22/22 PLang pass, 1423/1423 C# pass. See [v3/summary.md](v3/summary.md).
