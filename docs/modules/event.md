# Event Module

Hook into the PLang execution lifecycle. Register handlers that run before or after goals, steps, and actions.

## Actions

### beforeGoal

Run a goal before any matching goal starts.

```plang
- before each goal call !LogStart
- before goal 'ProcessData' call !ValidateInput
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| GoalToCall | goal | yes | — | Goal to execute on event |
| GoalPattern | string | no | — | Goal name pattern to match (null = all goals) |
| IsRegex | bool | no | false | Treat pattern as regex |
| Priority | int | no | 0 | Execution priority (higher = runs first) |

### afterGoal

Run a goal after any matching goal completes.

```plang
- after each goal call !LogEnd
- after goal 'ProcessData' call !NotifyComplete
```

Same parameters as `beforeGoal`.

### beforeStep

Run a goal before any matching step executes.

```plang
- before each step call !LogStep
- before step matching 'write*' call !CaptureOutput
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| GoalToCall | goal | yes | — | Goal to execute on event |
| GoalPattern | string | no | — | Goal name pattern |
| StepPattern | string | no | — | Step text pattern |
| IsRegex | bool | no | false | Treat patterns as regex |
| Priority | int | no | 0 | Execution priority |

### afterStep

Run a goal after any matching step completes. Same parameters as `beforeStep`.

```plang
- after each step call !AuditStep
```

### beforeAction

Run a goal before a specific action type executes.

```plang
- before action 'file/save' call !BackupFirst
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| GoalToCall | goal | yes | — | Goal to execute on event |
| ActionPattern | string | no | — | Action pattern to match |
| IsRegex | bool | no | false | Treat pattern as regex |
| Priority | int | no | 0 | Execution priority |

### afterAction

Run a goal after a specific action type completes. Same parameters as `beforeAction`.

### remove

Remove a registered event binding by ID.

```plang
- remove event %eventId%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| EventId | string | yes | The binding ID returned when the event was registered |

### skipAction

Skip the current action and return a custom value instead. Use this in a `beforeAction` handler to prevent the action from running.

```plang
- skip action with value 'mocked result'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Value | object | no | Value to return instead of the action's result |

## Event Registration Returns

When you register an event, the handler returns an event object with:

| Property | Description |
|----------|-------------|
| `id` | Unique binding ID (use with `remove`) |
| `type` | Event type (beforeGoal, afterStep, etc.) |
| `goalToCall` | The goal that will be called |
| `pattern` | The matching pattern |

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

DoWork
- write out 'Doing work'
```

### Event Cleanup

```plang
Start
- before each step call !Monitor, write to %monitorEvent%
- call !DoWork
- remove event %monitorEvent.id%
/ Monitor is no longer active
```
