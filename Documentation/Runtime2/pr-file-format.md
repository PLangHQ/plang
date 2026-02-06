# .pr File Format

The `.pr` file format is the compiled goal format — JSON files that the Runtime loads and executes.

## Structure

```json
{
  "name": "CreateUser",
  "description": "Creates a new user account",
  "comment": "Internal documentation",
  "visibility": "public",
  "isSetup": false,
  "isEvent": false,
  "hash": "abc123def456",
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
      "module": "validation",
      "method": "notEmpty",
      "parameters": {
        "value": "%name%",
        "errorMessage": "Name is required"
      },
      "returnVariable": null,
      "catchError": false,
      "onErrorGoal": null,
      "waitForExecution": true
    },
    {
      "index": 1,
      "text": "insert into users, name=%name%, email=%email%, write to %user%",
      "lineNumber": 3,
      "indent": 0,
      "module": "db",
      "method": "insert",
      "parameters": {
        "table": "users",
        "data": {
          "name": "%name%",
          "email": "%email%"
        }
      },
      "returnVariable": "user",
      "catchError": false,
      "waitForExecution": true
    }
  ],
  "subGoals": ["ValidateEmail", "SendWelcomeEmail"]
}
```

## Goal Properties

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Goal identifier, used for lookup |
| `description` | string? | Human-readable description |
| `comment` | string? | Internal documentation comment |
| `visibility` | string? | `"public"` or `"private"` (default) |
| `isSetup` | bool | If true, runs during app initialization |
| `isEvent` | bool | If true, goal is an event handler |
| `hash` | string? | Content hash for change detection |
| `inputParameters` | object? | Expected parameters as `name → type` mapping |
| `steps` | array | Ordered list of step objects |
| `subGoals` | array? | Names of referenced sub-goals |

## Step Properties

| Property | Type | Description |
|----------|------|-------------|
| `index` | int | Zero-based position in goal |
| `text` | string | Original PLang natural language text |
| `lineNumber` | int | Line number in source .goal file |
| `indent` | int | Indentation level |
| `comment` | string? | Step comment |
| `module` | string | Module name to handle this step |
| `method` | string | Method name on the module |
| `parameters` | object? | Method parameters |
| `returnVariable` | string? | Variable to store result |
| `catchError` | bool | If true, continue on error |
| `onErrorGoal` | string? | Goal to run if step fails |
| `waitForExecution` | bool | If true (default), wait for completion |

## Variable Syntax

Variables in `text` and `parameters` use `%variable%` syntax:

```json
{
  "text": "insert into users, name=%name%, email=%email%, write to %user%",
  "parameters": {
    "table": "users",
    "data": {
      "name": "%name%",
      "email": "%email%"
    }
  },
  "returnVariable": "user"
}
```

At runtime, `%name%` and `%email%` are resolved from `MemoryStack`.

## Type Hints

Type hints can be embedded in variable syntax:

```json
{
  "text": "set %count%(type:int) to 0",
  "parameters": {
    "name": "count",
    "value": 0,
    "type": "int"
  }
}
```

## GoalData and StepData DTOs

The Runtime uses DTOs for deserialization:

```csharp
public sealed class GoalData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("steps")]
    public List<StepData> Steps { get; set; } = new();

    // ... other properties
}

public sealed class StepData
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("module")]
    public string ModuleName { get; set; } = "";

    [JsonPropertyName("method")]
    public string MethodName { get; set; } = "";

    // ... other properties
}
```

## GoalDataConverter

Converts between DTOs and runtime objects:

```csharp
public static class GoalDataConverter
{
    public static Goal ToGoal(GoalData data, string? filePath = null, string? prFilePath = null, string? relativePath = null)
    public static Step ToStep(StepData data)
    public static GoalData ToData(Goal goal)
    public static StepData ToData(Step step)
}
```

### Usage

```csharp
// Load from .pr file
var json = await File.ReadAllTextAsync("goal.pr");
var goalData = JsonSerializer.Deserialize<GoalData>(json);
var goal = GoalDataConverter.ToGoal(goalData, filePath: "goal.goal", prFilePath: "goal.pr");

// Add to engine
engine.Goals.Add(goal);
```

## File Naming Convention

| File | Purpose |
|------|---------|
| `CreateUser.goal` | Source PLang file |
| `CreateUser.pr` | Compiled runtime file |
| `.build/CreateUser.pr` | Typical output location |

## Example: Complete .pr File

```json
{
  "name": "ProcessOrder",
  "description": "Processes a customer order",
  "visibility": "public",
  "inputParameters": {
    "orderId": "string",
    "customerId": "string"
  },
  "steps": [
    {
      "index": 0,
      "text": "select * from orders where id=%orderId%, write to %order%",
      "lineNumber": 2,
      "module": "db",
      "method": "select",
      "parameters": {
        "sql": "SELECT * FROM orders WHERE id = @orderId",
        "parameters": { "orderId": "%orderId%" }
      },
      "returnVariable": "order"
    },
    {
      "index": 1,
      "text": "if %order% is null then return error 'Order not found'",
      "lineNumber": 3,
      "module": "condition",
      "method": "if",
      "parameters": {
        "condition": "%order% == null",
        "then": { "return": { "error": "Order not found" } }
      }
    },
    {
      "index": 2,
      "text": "call ValidateInventory %order.items%",
      "lineNumber": 4,
      "module": "goal",
      "method": "call",
      "parameters": {
        "goalName": "ValidateInventory",
        "parameters": { "items": "%order.items%" }
      },
      "catchError": true,
      "onErrorGoal": "HandleInventoryError"
    },
    {
      "index": 3,
      "text": "update orders set status='processed' where id=%orderId%",
      "lineNumber": 5,
      "module": "db",
      "method": "update",
      "parameters": {
        "sql": "UPDATE orders SET status = 'processed' WHERE id = @orderId",
        "parameters": { "orderId": "%orderId%" }
      }
    }
  ],
  "subGoals": ["ValidateInventory", "HandleInventoryError"]
}
```

## Relationships

- Loaded by [Engine](engine.md) into [Goals](goals-steps.md) collection
- Deserialized by [JsonStreamSerializer](serializers.md)
- Converted to [Goal](goals-steps.md) and [Step](goals-steps.md) objects via `GoalDataConverter`
- Variables resolved from [MemoryStack](memory-stack.md) at runtime
