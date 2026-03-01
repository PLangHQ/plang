# Mock Module

Mock actions in PLang tests. Intercept action calls, return fake values, and verify call counts.

## Actions

### intercept

Create a mock that intercepts matching actions.

```plang
/ Return a fake value
- mock action 'file/read' return 'fake file content', write to %mockHandle%

/ Call a different goal instead
- mock action 'file/read' call !FakeRead, write to %mockHandle%

/ Spy mode — record calls without changing behavior
- mock action 'file/read', write to %mockHandle%

/ With parameter matching
- mock action 'file/read' with parameters {Path: 'config.json'} return '{}', write to %mockHandle%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| ActionPattern | string | yes | Action pattern to mock (supports `*` wildcard) |
| ReturnValue | object | no | Value to return (null = spy mode) |
| GoalToCall | goal | no | Goal to call instead of the action |
| Parameters | dictionary | no | Parameter matchers |

**Returns:** A `MockHandle` with:

| Property | Description |
|----------|-------------|
| `Id` | Unique mock ID |
| `ActionPattern` | The pattern being matched |
| `CallCount` | Number of times the mock was triggered |
| `Calls` | List of recorded calls with parameters and timestamps |
| `IsSpy` | Whether this is a spy (no return value override) |

### verify

Check that a mock was called the expected number of times.

```plang
- verify %mockHandle% was called 2 times
- verify %mockHandle% was called 1 time, 'File should be read exactly once'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Mock | MockHandle | yes | The mock to verify |
| ExpectedCount | int | yes | Expected number of calls |
| Message | string | no | Custom error message |

**Error:** Returns `AssertionError` if the actual call count doesn't match.

### reset

Remove a mock and clear its call history.

```plang
/ Reset a specific mock
- reset mock %mockHandle%

/ Reset all mocks
- reset all mocks
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Mock | MockHandle | no | Specific mock to reset (null = reset all) |

## Examples

### Mock File Operations in a Test

```plang
Start
/ Mock file.read to return test data
- mock action 'file/read' return '{"name":"test"}', write to %readMock%

/ Now when our code reads a file, it gets the mock data
- call !LoadConfig
- verify %readMock% was called 1 time

LoadConfig
- read 'config.json' into %config%
- write out 'Loaded: %config.name%'
```

### Spy on Calls

```plang
Start
/ Spy mode — don't change behavior, just record
- mock action 'output/write', write to %writeSpy%

- write out 'Hello'
- write out 'World'

- verify %writeSpy% was called 2 times
```

### Wildcard Patterns

```plang
/ Match any file action
- mock action 'file/*' return 'mocked', write to %fileMock%

/ Match any action
- mock action '*' return 'mocked', write to %allMock%
```
