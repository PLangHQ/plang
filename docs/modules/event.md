# Event Module

Hook into the PLang execution lifecycle. Register handlers that run before or after goals, steps, and actions.

## Actions

### on

Register an event binding. All event types use this single action with a `Type` parameter.

```plang
- before each goal call !LogStart
- after goal, call Cleanup
- before step, call LogStep, on goal pattern 'Api/*'
- before action, call MockHttp, on action pattern 'http.*'
- on before goal, call AuthCheck, on goal pattern '^Admin', is regex
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| Type | the leading phrase (`before each goal`, `after goal`, `before step`, `before action`) | string | yes | — | Event type (see below) |
| GoalToCall | `call <GoalName>` (`call Cleanup`) | goal | yes | — | Goal to execute when event fires |
| GoalPattern | `on goal pattern '<glob>'` | string | no | — | Glob or regex pattern to match goal names (null = all goals) |
| StepPattern | pattern to match step text, inline | string | no | — | Glob or regex pattern to match step text (step-level events only) |
| ActionPattern | `on action pattern '<glob>'` (`… 'http.*'`) | string | no | — | Glob or regex pattern to match action names, e.g., `http.*` (action-level events only) |
| IsRegex | `is regex` | bool | no | false | Treat patterns as regular expressions |
| Priority | priority number, inline | int | no | 0 | Execution priority (higher = runs first) |

**Event types:**

| Type | When it fires |
|------|--------------|
| `BeforeGoal` | Before a goal starts |
| `AfterGoal` | After a goal completes |
| `BeforeStep` | Before a step executes |
| `AfterStep` | After a step completes |
| `BeforeAction` | Before a specific action type executes |
| `AfterAction` | After a specific action type completes |

**Returns:** The binding ID (use with `remove`).

### remove

Remove a registered event binding by ID.

```plang
- remove event %eventId%
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| EventId | `remove event <id>` (`remove event %eventId%`) | string | yes | The binding ID returned when the event was registered |

### skipAction

Skip the current action and return a custom value instead. Use this inside a `BeforeAction` event handler to prevent the real action from running.

```plang
- skip action with value 'mocked result'
- skip action, value = %mockResponse%
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Value | `with value <x>` or `value = <x>` | object | no | Value to return instead of the action's real result |

## Examples

### Logging All Goal Execution

```plang
Start
- before each goal call !LogBefore
- after each goal call !LogAfter
- call !DoWork

LogBefore
- write out 'Starting goal...'

LogAfter
- write out 'Goal complete.'
```

### Intercepting Actions

```plang
Start
- before action, call MockHttp, on action pattern 'http.*'
- get 'https://api.example.com/data', write to %result%
- write out %result%

MockHttp
- skip action with value { "mocked": true }
```

### Event Cleanup

```plang
Start
- before each step call !Monitor, write to %monitorId%
- call !DoWork
- remove event %monitorId%
/ Monitor is no longer active
```

### Pattern Matching with Regex

```plang
Start
- before goal, call AuthCheck, on goal pattern '^Admin', is regex
- call !AdminDashboard

AuthCheck
- write out 'Checking auth...'
```
