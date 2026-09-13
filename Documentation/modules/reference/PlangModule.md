# PlangModule

Hint: `[plang]`  
Type: `PLang.Modules.PlangModule.Program`

get apps available, compiles plang code, gets goals and steps from .goal files, method descriptions and class scheme. Runtime Engine for plang, can run goal and step(s) in plang

## Methods

### BuildPlangCode

```
BuildPlangCode(PLang.Building.Model.Goal goal) : object
```

Builds(compiles) a goal in plang code

- `goal` *PLang.Building.Model.Goal* — (see Type information in SupportingObjects)

### BuildPlangStep

```
BuildPlangStep(PLang.Building.Model.GoalStep step) : PLang.Building.Model.GoalStep
```

Builds(compiles) a step in plang code

- `step` *PLang.Building.Model.GoalStep* — (see Type information in SupportingObjects)

### GetClassDescription

```
GetClassDescription(String moduleName) : PLang.Building.Model.ClassDescription
```

Returns the class description with methods in a module


### GetGoals

```
GetGoals(String fileOrFolderPath, String visibility = public, List<String> propertiesToExtract = null, String parser = pr) : Object
```

Get goals in file or folder. visiblity is either public|public_and_private|private. parser=pr|goal


### GetMehodInfo

```
GetMehodInfo(String type, String methodName) : PLang.Building.Model.ClassDescription
```


### GetMethodMappingScheme

```
GetMethodMappingScheme() : String
```


### GetMethods

```
GetMethods(List<String> modules = null, String format = null) : Object
```


### GetModules

```
GetModules(String stepText = null, List<String> excludeModules = null) : String
```


### GetModules2

```
GetModules2(String format = null) : Object
```


### GetSetupGoals

```
GetSetupGoals(String appPath = null, String visibility = public, List<String> propertiesToExtract = null) : List<Goal>
```

Get all setup goals. visibility=public|private|public_and_private. propertiesToExtract define what properties from the goal should be extracted


### GetStep

```
GetStep(String goalPrPath, String stepPrFile) : PLang.Building.Model.GoalStep
```


### GetStepProperties

```
GetStepProperties(String moduleName, String methodName) : Dictionary<String,Object>
```


### GetSteps

```
GetSteps(String goalPath) : IReadOnlyList<GoalStep>
```


### GetVariables

```
GetVariables(PLang.Building.Model.GoalStep step) : PLang.Runtime.ObjectValue
```

- `step` *PLang.Building.Model.GoalStep* — (see Type information in SupportingObjects)

### ListModules

```
ListModules() : List<ModuleInfo>
```

Lists the runtime modules by full name with the module description


### Run

```
Run(String namespace, String class, String method, Dictionary<String, Object> Parameters = null) : Object
```


### RunFromStep

```
RunFromStep(String prFileName) : Object
```

Run from a specific step and the following steps


### RunFunction

```
RunFunction(PLang.Runtime.ObjectValue genericFunction) : Object
```

- `genericFunction` *PLang.Runtime.ObjectValue* — (see Type information in SupportingObjects)

### RunModule

```
RunModule(String moduleName, String method, Dictionary<String, Object> parameters = null, Boolean fromAppRoot = False) : Object
```

Runs a method on a runtime module by name at runtime, e.g. moduleName=PLang.Modules.FileModule, method=ReadTextFile, parameters={path:"file.txt"}. Parameter names must match the method's parameter names. Relative paths resolve from the app root when fromAppRoot is true, otherwise from the calling goal's folder. Returns what the method returns


### RunStep

```
RunStep(PLang.Building.Model.GoalStep step, Dictionary<String, Object> parameters = null) : Object
```

Runs a plang step. No other step is executed

- `step` *PLang.Building.Model.GoalStep* — (see Type information in SupportingObjects)
- `parameters` *Dictionary<String, Object>*, default `null`

### SaveGoal

```
SaveGoal(PLang.Building.Model.Goal goal) : Object
```

- `goal` *PLang.Building.Model.Goal* — (see Type information in SupportingObjects)

### SaveMethod

```
SaveMethod(Object methodPr) : Object
```


### StartCSharpDebugger

```
StartCSharpDebugger() : object
```


### ValidateGoal

```
ValidateGoal(PLang.Building.Model.Goal goal) : Object
```

- `goal` *PLang.Building.Model.Goal* — (see Type information in SupportingObjects)

### ValidateMethod

```
ValidateMethod(PLang.Building.Model.GoalStep step, Object function) : Object
```

- `step` *PLang.Building.Model.GoalStep* — (see Type information in SupportingObjects)
- `function` *Object*

