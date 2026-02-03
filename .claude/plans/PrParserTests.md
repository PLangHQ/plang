# Plan: PrParser Unit Tests

## Summary

Created comprehensive unit tests for `PrParser` which parses compiled `.pr` files (JSON) into `Goal` objects at runtime. The parser handles goal resolution by name, path, and various folder structures (apps, .services, system).

## Files Created

- `PLang.Tests/Building/Parsers/PrParserTests.cs` - 35 unit tests for PrParser

## Test Categories

### ParsePrFile Tests (11 tests)
- Valid .pr file returns Goal
- Non-.pr file throws ArgumentException
- File not found returns null
- Sets absolute paths correctly
- Apps folder sets AppName
- Services folder sets AppName
- System folder sets IsSystem = true
- Regular path sets IsSystem = false
- Sets step Goal reference
- Sets step AbsolutePrFilePath
- Sets step Index and Number

### GetGoal(GoalToCallInfo) Tests (4 tests)
- Find goal by path
- Find goal by name
- Goal not found returns error
- Multiple goals with same name returns error

### GetGoalByAppAndGoalName Tests (8 tests)
- Null appStartupPath throws ArgumentNullException
- Null goalName throws ArgumentNullException
- Simple goal name finds goal
- Goal not found returns null
- Path with folder finds goal
- Root path (/Start) finds goal
- Multiple matching goals throws GoalNotFoundException
- Removes .goal extension from search
- Removes ! prefix from search

### GetEvents Tests (5 tests)
- Event not found returns empty list
- Event found returns goals
- Cache hit returns same list
- GetEvent returns first match
- GetEvent not found returns null

### GetSystemEvents Tests (2 tests)
- System event found returns goals
- GetSystemEvent returns first match

### LoadAllGoalsByPath Tests (2 tests)
- No build directory returns empty list
- With goals returns all goals

### GetPublicGoals Tests (1 test)
- Returns only public visibility goals

### ForceLoadAllGoals Tests (1 test)
- Reloads goals when called

## Key Decisions

1. **Mock JSON serialization**: Created helper method `CreateGoalJson()` to generate valid JSON for Goal objects, which is what PrParser expects to read from .pr files.

2. **File system mocking strategy**: Mocked `IFile.Exists()` and `IFile.ReadAllText()` to simulate file content, and `IDirectory.GetFiles()` to return expected file lists.

3. **Constructor initialization**: PrParser constructor calls `GetGoals()` and `GetSystemGoals()`, so mocks need to be set up before creating the parser instance.

4. **Path handling**: Set up comprehensive `IPath` mocks to delegate to real `System.IO.Path` methods for consistent path joining and manipulation.

## Lessons Learned

1. **PrParser constructor side effects**: The constructor immediately calls `GetGoals()`, `GetSystemGoals()`, and `GetEventsFiles()`. This means all file system mocks must be configured before instantiating the parser.

2. **Goal JSON structure**: PrParser uses `JsonHelper.ParseFilePath<Goal>()` which deserializes JSON to Goal objects. Tests need to provide valid JSON with required properties.

3. **Path adjustment**: PrParser calls `AdjustPathToOs()` on paths to handle cross-platform compatibility. Tests use Windows-style paths (`\`) but the parser normalizes them.

4. **Event caching**: The `GetEvents()` method uses a `ConcurrentDictionary` cache. Second calls for the same event name return the cached result.

5. **Goal visibility filtering**: `GetPublicGoals()` filters by `Visibility.Public`, so test data needs to include visibility settings.

## Verification

All 628 tests pass:
- 35 new PrParser tests
- 30 GoalParser tests
- 563 existing tests unchanged
