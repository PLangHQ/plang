# Tester Summary — runtime2-builder

## v1
Fixed path resolution bug in `PrParser.ParsePrFile()` that caused `plang p build --llmservice=openai` to fail with `InstructionFileNotLoaded`. One-line condition change: system directory paths now correctly derive their base directory instead of falling back to `RootDirectory`. All 1423 C# tests pass. See [v1/summary.md](v1/summary.md).
