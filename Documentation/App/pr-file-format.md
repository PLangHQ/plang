# .pr File Format

The `.pr` file format is the compiled goal format — JSON files that the Runtime loads and executes. App supports two versions.

## Format Versions

| Version | Extension | Structure | Location |
|---------|-----------|-----------|----------|
| v0.1 | `.pr` | One file per step, in numbered subdirectories | `.build/{GoalName}/00. Goal.pr`, `01. step_name.pr` |
| v0.2 | `.pr.json` | Single file with all steps | Saved next to the `.goal` file as `{name}.pr.json` |

`PrParser` handles both formats transparently.

## v0.2 Structure (Current)

```json
{
  "name": "CreateUser",
  "description": "Creates a new user account",
  "comment": null,
  "visibility": "public",
  "isSetup": false,
  "isEvent": false,
  "hash": "abc123def456",
  "path": "CreateUser.goal",
  "inputParameters": {
    "name": "string",
    "email": "string"
  },
  "steps": [
    {
      "index": 0,
      "text": "validate %name% is not empty",
      "lineNumber": 2,
      "indent": 0,
      "comment": null,
      "actions": [
        {
          "action": "condition",
          "method": "evaluate",
          "parameters": [
            { "name": "expression", "value": "%name% != null && %name% != ''" },
            { "name": "errorMessage", "value": "Name is required" }
          ],
          "return": null
        }
      ],
      "hash": "step_hash_1",
      "previousHash": null,
      "intent": "Validate that name is not empty",
      "waitForExecution": true
    },
    {
      "index": 1,
      "text": "set %greeting% to 'Hello %name%'",
      "lineNumber": 3,
      "indent": 0,
      "actions": [
        {
          "action": "variable",
          "method": "set",
          "parameters": [
            { "name": "name", "value": "greeting" },
            { "name": "value", "value": "Hello %name%" }
          ],
          "return": null
        }
      ],
      "hash": "step_hash_2",
      "waitForExecution": true
    }
  ],
  "subGoals": [],
  "errors": [],
  "warnings": []
}
```

## Goal Properties

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Goal identifier, used for lookup |
| `description` | string? | Human-readable description |
| `comment` | string? | Builder documentation comment |
| `visibility` | string | `"public"` or `"private"` (default) |
| `isSetup` | bool | If true, runs during app initialization |
| `isEvent` | bool | If true, goal is an event handler |
| `hash` | string? | Content hash for change detection |
| `path` | string? | Relative path to source `.goal` file |
| `inputParameters` | object? | Expected parameters as `name → type` mapping |
| `steps` | array | Ordered list of step objects |
| `subGoals` | array | Names of referenced sub-goals |
| `errors` | array | Build errors (list of `{ key, message }`) |
| `warnings` | array | Build warnings (list of `{ key, message }`) |

## Step Properties

| Property | Type | Description |
|----------|------|-------------|
| `index` | int | Zero-based position in goal |
| `text` | string | Original PLang natural language text |
| `lineNumber` | int | Line number in source .goal file |
| `indent` | int | Indentation level |
| `comment` | string? | Step comment |
| `actions` | array | Action bindings (see below) |
| `hash` | string? | Content hash |
| `previousHash` | string? | Previous build hash |
| `intent` | string? | LLM-inferred intent |
| `onErrorGoal` | string? | Goal to run if step fails |
| `onError` | object? | Error handler configuration |
| `cache` | object? | Caching configuration |
| `timeout` | int? | Timeout in milliseconds |
| `waitForExecution` | bool | If true (default), wait for completion |
| `errors` | array | Build errors |
| `warnings` | array | Build warnings |

## Action Properties

| Property | JSON Name | Type | Description |
|----------|-----------|------|-------------|
| `Class` | `action` | string | Handler class/namespace name |
| `Method` | `method` | string | Handler method name |
| `Parameters` | `parameters` | array | List of `{ name, value }` Data objects |
| `Return` | `return` | array? | List of `{ name, value }` return variable mappings |
| `Errors` | `errors` | array | Build errors |
| `Warnings` | `warnings` | array | Build warnings |

Note: The `Class` property is serialized as `"action"` in JSON via `[JsonPropertyName("action")]`.

## Parameters and Return Format

Parameters and return values are serialized as `List<Data>`:

```json
"parameters": [
  { "name": "name", "value": "greeting" },
  { "name": "value", "value": "Hello World" },
  { "name": "type", "value": "string" }
]
```

Return variables map action outputs to Variables variables:

```json
"return": [
  { "name": "result", "value": null }
]
```

## Variable Syntax

Variables in `text` and parameter values use `%variable%` syntax:

```json
{
  "text": "set %greeting% to 'Hello %name%'",
  "actions": [
    {
      "action": "variable",
      "method": "set",
      "parameters": [
        { "name": "name", "value": "greeting" },
        { "name": "value", "value": "Hello %name%" }
      ]
    }
  ]
}
```

At runtime, `%name%` is resolved from `Variables` via the source-generated lazy parameter records.

## ErrorHandler (onError)

```json
"onError": {
  "goal": {
    "name": "HandleError",
    "parameters": { "error": "%error%" }
  },
  "retryCount": 3,
  "retryOverMs": 10000,
  "order": "before",
  "ignoreError": false,
  "message": "Step failed",
  "statusCode": 500,
  "key": "StepError"
}
```

## CacheSettings (cache)

```json
"cache": {
  "durationMinutes": 30,
  "sliding": true,
  "key": "user_%userId%",
  "location": "memory"
}
```

## PrParser

`App.Parsing.PrParser` handles both v0.1 and v0.2 formats:

```csharp
public static class PrParser
{
    Goal? ParsePrFile(string filePath, IPLangFileSystem fs)
    List<Goal> GetAllGoals(string rootPath, IPLangFileSystem fs)
    AppData? LoadAppData(string rootPath, IPLangFileSystem fs)
    void SaveAppData(string rootPath, AppData data, IPLangFileSystem fs)
}
```

## File Naming Convention

| File | Purpose |
|------|---------|
| `CreateUser.goal` | Source PLang file |
| `.build/createuser.pr` | v0.1 compiled file |
| `CreateUser.pr.json` | v0.2 compiled file (next to .goal) |

## Loading .pr Files

```csharp
// Via App
await app.LoadGoalFromFileAsync("path/to/.build/start.pr");
await app.LoadGoalsFromDirectoryAsync(".build");

// Via Goals collection
await app.Goals.LoadFromFileAsync(app, prFilePath);
await app.Goals.LoadFromDirectoryAsync(app, buildDir);

// Direct parsing
var goal = PrParser.ParsePrFile(prFilePath, app.FileSystem);
```

## Relationships

- Loaded by [App](app.md) via [Goals](goals-steps.md) collection
- Deserialized by [SerializerRegistry](serializers.md) (via IO.ReadAsync)
- Maps to [Goal](goals-steps.md), [Step](goals-steps.md), and [Action](goals-steps.md) objects
- Variables resolved from [Variables](memory-stack.md) at runtime
- `GoalMapper` converts from `Building.Model` to `App.Core` types
