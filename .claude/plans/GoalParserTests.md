# Plan: GoalParser Unit Tests

## Summary

Created comprehensive unit tests for `GoalParser.ParseGoalFile()` method which parses `.goal` files (text) into `Goal` objects with steps. The tests cover basic parsing, step indentation, path resolution, goal properties, error handling, and multi-line steps.

## Files Created

- `PLang.Tests/Building/Parsers/GoalParserTests.cs` - 30 unit tests for GoalParser

## Test Categories

### Basic Parsing (6 tests)
- Single goal with one step
- Single goal with multiple steps
- Goal with line comment
- Goal with block comment
- Multiple goals in one file

### Step Parsing (6 tests)
- Zero indent (Execute = true)
- Four space indent (Execute = false)
- Eight space indent
- Invalid indentation throws BuilderStepException
- Special characters in step text
- Tabs converted to spaces

### Path Resolution (4 tests)
- App goal path relative paths
- System goal startup path
- Goal in subfolder
- Goal in apps folder

### Goal Properties (9 tests)
- First goal has Public visibility
- Subsequent goals have Private visibility
- SubGoals have ParentGoal set
- ParentGoal has SubGoals paths
- New goals have HasChanged = true
- Steps have correct LineNumbers
- Steps have correct Numbers
- Steps have correct Index

### Error Cases (5 tests)
- Empty file returns empty list
- Whitespace-only file
- Non-.goal file throws exception
- Duplicate goal names throws BuilderException
- Goal with no steps

### Other (2 tests)
- Multi-line step continuation
- Steps reference their parent Goal

## Key Decisions

1. **Mocking approach**: Used NSubstitute to mock `IPLangFileSystem`, `ISettings`, and `ILogger`. File system operations are mocked via `IFile`, `IDirectory`, and `IPath` interfaces.

2. **Real ServiceContainer**: Used actual `LightInject.ServiceContainer` since mocking the container's registration methods would be complex and the real container works fine for tests.

3. **Skipped injection tests**: The `HandleInjections` method requires a fully configured container with all dependencies registered, making it unsuitable for unit testing. Marked as requiring integration tests.

4. **IsSystem property**: Discovered that `IsSystem` is only set when there's a previous build (`prevBuildGoal != null`). Changed test to verify `AbsoluteAppStartupFolderPath` instead, which is reliably set based on `isSystem` parameter.

5. **Logger warnings**: Removed assertions on `ILogger.LogWarning()` calls because these are extension methods that can't be verified with NSubstitute. Tests now verify the actual behavior (empty list returned) rather than logging side effects.

## Lessons Learned

1. **TUnit exception assertions**: TUnit uses `Assert.ThrowsAsync<T>()` pattern rather than fluent chain like `.Throws<T>().WithMessageContaining()`. Must capture exception and assert on message separately.

2. **ILogger mocking limitations**: `LogWarning`, `LogError`, etc. are extension methods in Microsoft.Extensions.Logging. They cannot be verified directly with NSubstitute. Either use a custom ILogger implementation or verify behavior instead.

3. **Path behavior**: The `GoalParser` heavily uses `System.IO.Path` for joining and manipulating paths. Mocking `IPath` to delegate to real `Path` methods ensures consistent behavior across tests.

4. **GoalParser state**: The parser accumulates goals in a private `goals` field. Tests that call `ParseGoalFile` directly get fresh results each time since that method doesn't use the cached field.

## Verification

All 593 tests pass:
- 30 new GoalParser tests
- 563 existing tests unchanged
