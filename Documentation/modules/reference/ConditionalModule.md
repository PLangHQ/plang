# ConditionalModule

Hint: `[conditional]`  
Type: `PLang.Modules.ConditionalModule.Program`

if statement for the user intent. Example 1:'if %isValid% is true then call SomeGoal, else call OtherGoal', this condition would return true if %isValid% is true and call a goals on either conditions. Example 2:'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false. Use when checking if file or directory exists. Prefer predefined methods in this module over SimpleCondition and CompoundCondition. 
if statement can throw an error, e.g. `if %isValid% is false, then throw error 'Not valid'`

## Methods

### CompoundCondition

```
CompoundCondition(PLang.Modules.ConditionalModule.ConditionEvaluator+CompoundCondition condition, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

Logic: AND|OR Operator: ==|!=|<|>|<=|>=|in|contains|startswith|endswith|indexOf.  IsNot property indicates if the condition is a negation of the specified operator. True for ‘is not’, ‘does not’, etc.

- `condition` *PLang.Modules.ConditionalModule.ConditionEvaluator+CompoundCondition* — (see Type information in SupportingObjects)
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### ContainsNumbers

```
ContainsNumbers(Object item, List<Int32> contains = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

example: `if %code% contains 123, 345 then ....`, `if %zip% is one of (223,333) then...`

- `item` *Object*
- `contains` *List<Int32>*, default `null`
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### ContainsString

```
ContainsString(Object item, String contains, Boolean isNot = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

isNot property reverse true to false, example: `if %name% contains "john" then`, `if %product% contains %title% then call goal DoProdudct`, `if %name% does not contain "bill"` (isNot=true)

- `item` *Object*
- `contains` *String*
- `isNot` *Boolean*, default `False`
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### DirectoryExists

```
DirectoryExists(String dirPathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `dirPathOrVariableName` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### FileExists

```
FileExists(String filePathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `filePathOrVariableName` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### HasAccessToPath

```
HasAccessToPath(String dirOrFilePathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `dirOrFilePathOrVariableName` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsBase64

```
IsBase64(String content, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `content` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsEmpty

```
IsEmpty(Object item, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `item` *Object*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %id% is empty then call Create, else call Update => item=%id%, goalTocallIfTrue={Name="Create"}, goalToCallifFalse={Name="Update"}
- if %name% is empty then throw => item=%name%, throwErrorOnTrue=...generate ErrorInfo

### IsEqual

```
IsEqual(Object item1, Object item2, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, Boolean ignoreCase = True, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

Check if item equals(==) another object. 
`if %id% == 100 then call IsCorrectNumber, else DoSomeThingElse....` => IsCorrectNumber is goalToCallIfTrue, DoSomeThingElse is goalToCallIfFalse
`if %name% is 'john'....`
`if %zip equals 123....

- `item1` *Object*
- `item2` *Object*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `ignoreCase` *Boolean*, default `True`
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsFalse

```
IsFalse(Nullable<Boolean> item = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `item` *Nullable<Boolean>*, default `null`
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsMod

```
IsMod(Double leftValue, Double modValue, Double equalsValue, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `leftValue` *Double*
- `modValue` *Double*
- `equalsValue` *Double*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsNotEmpty

```
IsNotEmpty(Object item, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `item` *Object*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsNotEqual

```
IsNotEqual(Object item1, Object item2, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, Boolean ignoreCase = True, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `item1` *Object*
- `item2` *Object*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `ignoreCase` *Boolean*, default `True`
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsOsPath

```
IsOsPath(String path, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `path` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsSystemPath

```
IsSystemPath(String path, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `path` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### IsTrue

```
IsTrue(Nullable<Boolean> item = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `item` *Nullable<Boolean>*, default `null`
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### RunInlineCode

```
RunInlineCode(PLang.Services.CompilerService.ConditionImplementationResponse implementation) : Object
```

Choose RunInlineCode when no other method matches user intent. Implementation variable can be set to null.

- `implementation` *PLang.Services.CompilerService.ConditionImplementationResponse* — (see Type information in SupportingObjects)

### SetVariableWithCondition

```
SetVariableWithCondition(String variableName, Boolean boolValue, Object valueIfTrue, Object valueIfFalse) : object
```


### SimpleCondition

```
SimpleCondition(PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition condition, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

Operator: ==|!=|<|>|<=|>=|in|isEmpty|contains|startswith|endswith|indexOf. IsNot property indicates if the condition is a negation of the specified operator. 
IsNot=True for ‘is not’, ‘does not’, 
Logic: convert "&&" => "AND", "||" => "OR"

- `condition` *PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition* — (see Type information in SupportingObjects)
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### StartsWith

```
StartsWith(Object item, String startsWith, Boolean isNot = False, Boolean ignoreCase = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

isNot property reverse true to false, example: `if %name% starts with "john" then`, `if %source% starts with "t" then call goal Track`, `if %name% does not start with "bill"` (isNot=true). Use ignoreCase=true to compare case insensitive.

- `item` *Object*
- `startsWith` *String*
- `isNot` *Boolean*, default `False`
- `ignoreCase` *Boolean*, default `False`
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

