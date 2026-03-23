# Bug: Relative file paths resolve against engine root, not goal folder

## The Problem

`Memory.Path` resolves relative paths (e.g., `testdata.txt`) against the engine's `RootDirectory`, not the goal's folder. This means a goal in `/sub/MyGoal.goal` that does `read subdata.txt` looks for `{root}/subdata.txt` instead of `{root}/sub/subdata.txt`.

**Expected behavior:**
- `testdata.txt` → relative to the **goal's folder**
- `/testdata.txt` → relative to **engine root** (PLang-rooted)

**Current behavior:**
- `testdata.txt` → relative to **engine root** (wrong)
- `/testdata.txt` → relative to engine root (correct)

## Root Cause

`PLang/Runtime2/Engine/Memory/Path.cs:34`:

```csharp
_absolutePath = _fs.ValidatePath(rawPath);
```

`ValidatePath` in `PLangFileSystem` resolves non-rooted paths via `Path.GetFullPath(Path.Join(RootDirectory, path))` — always against root, never against the goal's directory.

`Memory.Path` constructor only receives `(string rawPath, Engine engine)` — it has no knowledge of which goal is running or where it lives. The goal folder is available through the context chain: `context` → current step → `step.Goal` → `goal.Path` → derive directory.

## The Fix

`Memory.Path` needs the goal's folder to resolve relative paths. Two options:

### Option A: Pass context to Path.Resolve

The source generator calls `Path.Resolve(string, Engine)`. Change the signature to include context:

```csharp
public static Path Resolve(string rawPath, Engine.@this engine, PLangContext context)
```

Then in the constructor, for non-rooted paths, resolve relative to the goal's directory:

```csharp
if (!IsRooted(rawPath) && context?.Step?.Goal?.Path != null)
{
    var goalDir = _fs.Path.GetDirectoryName(context.Step.Goal.Path);
    rawPath = _fs.Path.Combine(goalDir, rawPath);
}
_absolutePath = _fs.ValidatePath(rawPath);
```

This requires updating `LazyParamsGenerator.cs` to pass context to `Path.Resolve`.

### Option B: Resolve in ValidatePath with a goal-relative overload

Add an overload to `IPLangFileSystem.ValidatePath` that accepts a base directory:

```csharp
public string ValidatePath(string path, string? relativeBase)
```

When `relativeBase` is non-null and path is not rooted, resolve against `relativeBase` instead of `RootDirectory`.

### Recommendation

Option A is cleaner — keeps the resolution logic in `Memory.Path` and follows OBP (Path navigates to what it needs via context).

## Side note: Memory.Path location

`Memory.Path` lives in `Engine/Memory/` but it's not really a memory concern — it's a rich file path wrapper with `.Read()`, `.Delete()`, `.Exists` etc. Consider moving it closer to the file module or filesystem abstraction. Not blocking, just a naming/location smell.

## How to Validate

There are existing tests in `PLang.Tests/Runtime2/Core/PrPipelineTests.cs` ready to validate the fix.

### Test that currently documents the bug:

`FilePaths_RelativeResolvesAgainstRoot_NotGoalFolder` — this test asserts that a goal in `/sub/` reading `subdata.txt` gets `NotFound`. **After the fix, this test should be updated to expect success** — the file should be found at `{root}/sub/subdata.txt`.

Change from:
```csharp
await Assert.That(result.Success).IsFalse();
await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
```

To:
```csharp
await Assert.That(result.Success).IsTrue();
await Assert.That(context.MemoryStack.GetValue("content")!.ToString()).IsEqualTo("Hello from subfolder");
```

### Test that passes but for wrong reason:

`FilePaths_FromSubfolder_AbsoluteRootWorks` uses `FilePathsFromSub.pr` which only tests `/testdata.txt` (absolute). After the fix, re-add the relative step to `sub/FilePathsFromSub.pr`:

```json
{
  "index": 1,
  "text": "read subdata.txt, write to %localRelative%",
  "lineNumber": 3,
  "actions": [{
    "module": "file",
    "action": "read",
    "parameters": [{ "name": "path", "value": "subdata.txt" }],
    "return": [{ "name": "localRelative" }]
  }]
}
```

And assert:
```csharp
await Assert.That(context.MemoryStack.GetValue("localRelative")!.ToString()).IsEqualTo("Hello from subfolder");
```

### Also update PathTests.cs:

`PLang.Tests/Runtime2/Modules/Path/PathTests.cs` — `Absolute_ResolvesRelativePath` currently expects resolution against the engine's temp dir root. After the fix, if the Path has context with a goal, it should resolve against the goal's directory.

### Run all tests:

```bash
dotnet build PLang.Tests/PLang.Tests.csproj
dotnet run --project PLang.Tests/PLang.Tests.csproj
```

All 1912 tests should pass. The 3 existing failures are pre-existing TestFixture DLL issues unrelated to this.

## Files to modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Memory/Path.cs` | Add context parameter, resolve relative paths against goal folder |
| `PLang.Generators/LazyParamsGenerator.cs` | Pass context to `Path.Resolve()` in generated code |
| `PLang.Tests/Runtime2/Core/PrPipelineTests.cs` | Update `FilePaths_RelativeResolvesAgainstRoot_NotGoalFolder` to expect success |
| `PLang.Tests/Runtime2/Fixtures/pr/sub/FilePathsFromSub.pr` | Re-add relative step (`subdata.txt`) |
| `PLang.Tests/Runtime2/Modules/Path/PathTests.cs` | Update `Absolute_ResolvesRelativePath` if needed |
