# Tester v1 Summary — feature/path-class

## What this is

Test quality review of the Path class feature after 6 coder iterations and 2 auditor reviews. The auditor approved for merge. My job: verify the tests are honest — that they'd fail if the code were broken.

## Test run

- **C# tests**: 1227/1227 pass (0 failed, 0 skipped)
- **PLang tests**: None exist for this feature
- **Duration**: ~4 seconds

## Key findings

### Critical: Exception handling is untested (finding #1)

The auditor's **#1 critical finding** was that Path behavior methods had no exception handling. The coder added try/catch blocks in v6 — all 6 behavior methods now catch `IOException | UnauthorizedAccessException` and return `Data.FromError(ServiceError(..., "IOError", 500))`.

**But no test exercises any of these catch blocks.** If every try/catch were deleted, all 1227 tests still pass. This is the textbook false-green.

```csharp
// Path.cs line 127-130 — this code is completely untested
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
}
```

**Fix**: Mock filesystem operations to throw IOException. Verify the returned Data has `Success == false` and error code "IOError".

### Major: No PLang .goal tests (finding #2)

CLAUDE.md requires PLang tests alongside C#. Zero PLang test goals exist for file operations. The C# integration tests construct goals manually in code — this validates runtime execution but skips builder → .pr → GoalMapper.

### Major: Overwrite conflicts untested (finding #3)

`Overwrite` defaults to `false` on both Copy and Move. No test creates a dest file first and copies/moves over it. The developer explicitly reviewed overwrite behavior (v3 review, line 32 of copy.cs). Without a test, this default-false behavior has no regression protection.

### Major: Save object serialization untested (finding #4)

Save has 3 branches: `byte[]`, `string`, `else`. The `else` branch uses `_engine.Channels.Serializers.SerializeAsync` — the most complex path and the one PLang users hit when saving objects. Zero test coverage.

### Minor findings (#5-#8)

- Error assertions only check `Success == false`, never verify error code/message
- `Relative_StripsRootDirectory` has loose assertions (doesn't check exact value)
- List tests only check count, not file names
- Copy test doesn't verify source still exists after copy

## What's good

- Delete tests verify the file/directory is actually gone, not just `Success == true`
- Move tests verify source removed AND dest exists — intent verification
- Save tests verify actual written content matches
- Integration tests (`Integration_FileExists_FlowsThroughMemoryStack_ToOutput`) are excellent — full engine pipeline with MemoryStack and output channel
- Null guard tests, relative prefix regression test, IgnoreIfNotFound test — all solid

## Verdict: **needs-fixes**

Send back to coder for at minimum:
1. Exception-path tests for try/catch blocks (critical)
2. PLang .goal tests for file operations (required per CLAUDE.md)
3. Overwrite conflict tests
4. Save object serialization test
