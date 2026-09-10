# EventModule

Hint: `[event]`  
Type: `PLang.Modules.EventModule.Program`

Bind an event to app, goal, step, error, and any kind of custom errors.
Example:
- run before app starts, call goal Starting
- run on each step, call StepAfter (before|after(default))
- on error call HandleError

## Methods

### BindEvent

```
BindEvent(PLang.Events.EventBinding eventBinding) : object
```

- `eventBinding` *PLang.Events.EventBinding* — (see Type information in SupportingObjects)

