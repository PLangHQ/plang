# Plan: Fix `plang p build --llmservice=openai` path resolution

## Context
`plang p build --llmservice=openai` fails with `InstructionFileNotLoaded` because `PrParser.ParsePrFile()` resolves system goal paths relative to `RootDirectory` instead of `SystemDirectory`.

## Root Cause
In `PrParser.ParsePrFile()` (line 95-99), the path detection logic checks if `absolutePrFilePath` starts with `RootDirectory`. Since `SystemDirectory` (`/workspace/plang/PlangConsole/bin/Debug/net10.0/system`) starts with `RootDirectory` (`/workspace/plang`), system goal paths incorrectly use `RootDirectory` as their base. This produces wrong absolute paths for step .pr files (e.g., `/workspace/plang/.build/Build/01. step.pr` instead of `/workspace/plang/.../system/.build/Build/01. step.pr`).

## Fix
One-line change in `PrParser.ParsePrFile()`: Add a check for `SystemDirectory` before the `RootDirectory` check. System paths use the "before .build" extraction logic, which correctly derives the base directory from the file path itself.

## Verification
1. `dotnet build PlangConsole -c Debug` — no errors
2. `dotnet run --project PLang.Tests` — all 1423 tests pass
3. `plang p build --llmservice=openai` — builder starts, sends prompts to LLM
4. `plang p !test` — 11 pass, 8 pre-existing failures (unrelated to this change)
