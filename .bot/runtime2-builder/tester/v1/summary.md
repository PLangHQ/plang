# v1 Summary: Fix `plang p build --llmservice=openai` path resolution

## What this is
Fix for a path resolution bug that prevented the PLang v0.2 builder from running. The `plang p build --llmservice=openai` command failed with `InstructionFileNotLoaded` because system goal .pr files were being resolved relative to the wrong directory.

## What was done
**Approach:** Fixed the root cause in `PrParser.ParsePrFile()` rather than patching paths after parsing in `Build2()` (as the original plan suggested). This is cleaner because it fixes all callers, not just `Build2`.

**The bug:** `ParsePrFile` checks if a .pr file path starts with `RootDirectory` to determine the base directory. Since `SystemDirectory` is a subdirectory under `RootDirectory` (via symlink), system goal paths incorrectly used `RootDirectory` as their base, producing wrong absolute paths for step .pr files.

**The fix:** Added `absolutePrFilePath.StartsWith(fileSystem.SystemDirectory)` as a condition to use the "extract path before .build" logic. This correctly derives the base directory for system goals from their file path.

**File modified:** `PLang/Building/Parsers/PrParser.cs` — line 96

## Code example

Before:
```csharp
var appAbsoluteStartupPath = fileSystem.RootDirectory;
if (!absolutePrFilePath.StartsWith(fileSystem.RootDirectory))
{
    appAbsoluteStartupPath = absolutePrFilePath.Substring(0, absolutePrFilePath.IndexOf(".build"));
}
```

After:
```csharp
var appAbsoluteStartupPath = fileSystem.RootDirectory;
if (absolutePrFilePath.StartsWith(fileSystem.SystemDirectory) || !absolutePrFilePath.StartsWith(fileSystem.RootDirectory))
{
    appAbsoluteStartupPath = absolutePrFilePath.Substring(0, absolutePrFilePath.IndexOf(".build"));
}
```

## Verification
- C# tests: 1423/1423 passed
- PLang tests: 11 passed, 8 pre-existing failures (unrelated)
- `plang p build --llmservice=openai`: successfully starts and sends prompts to LLM
