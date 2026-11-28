# WindowAppModule

Hint: `[windowapp]`  
Type: `PLang.Modules.WindowAppModule.Program`

## Methods

### RunWindowApp

```
RunWindowApp(PLang.Models.GoalToCallInfo goalName, Int32 width = 800, Int32 height = 450, String iconPath = null, String windowTitle = plang) : object
```

goalName is required. It is one word. Example: call !NameOfGoal, run !Google.Search. Do not use the names in your response unless defined by user

- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `width` *Int32*, default `800`
- `height` *Int32*, default `450`
- `iconPath` *String*, default `null`
- `windowTitle` *String*, default `plang`

