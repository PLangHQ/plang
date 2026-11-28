# ConditionalModule

Hint: `[conditional]`  
Type: `PLang.Modules.ConditionalModule.Program`

if statement for the user intent. Example 1:'if %isValid% is true then call SomeGoal, else call OtherGoal', this condition would return true if %isValid% is true and call a goals on either conditions. Example 2:'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false. Use when checking if file or directory exists. Prefer predefined methods in this module over SimpleCondition and CompoundCondition. 
if statement can throw an error, e.g. `if %isValid% is false, then throw error 'Not valid'`
A one line if that calls goals, `if %codeCount% >= 2 then call goal HandleOverLimit, else call goal IssueCodeNow`, sets goalToCallIfTrue to HandleOverLimit and goalToCallIfFalse to IssueCodeNow.

## Methods

### CompoundCondition

```
CompoundCondition(PLang.Modules.ConditionalModule.ConditionEvaluator+CompoundCondition condition, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

Logic: AND|OR Operator: ==|!=|<|>|<=|>=|in|notIn|isEmpty|isNotEmpty|contains|notContains|startswith|notStartswith|endswith|notEndswith|indexOf|matches|notMatches. Equality is `==` (never a single `=`). A test written in the negative takes the negative operator and IsNot stays false, e.g. `is not empty` => isNotEmpty. A goal the step calls goes in goalToCallIfTrue (after `then`) and goalToCallIfFalse (after `else`), also when the whole if is written on one line.

- `condition` *PLang.Modules.ConditionalModule.ConditionEvaluator+CompoundCondition* — (see Type information in SupportingObjects)
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %count% > 0 and %isValid% then call goal Process, else call goal Reject => condition={"Kind":"Compound","Logic":"AND","Conditions":[...]}, goalToCallIfTrue={Name="Process"}, goalToCallIfFalse={Name="Reject"}

### ContainsNumbers

```
ContainsNumbers(Object item, List<Int32> contains = null, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

example: `if %code% contains 123, 345 then ....`, `if %zip% is one of (223,333) then...`

- `item` *Object* — The value being tested, the left side of the comparison
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

- `item` *Object* — The value being tested, the left side of the comparison, e.g. %name% in `if %name% contains "john"`
- `contains` *String* — What it is tested against, the right side, e.g. "john" in `if %name% contains "john"`
- `isNot` *Boolean*, default `False` — true when the step negates the test, e.g. `does not contain`, `does not start with`, `is not`. false when the step states the test plainly
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

### EndsWith

```
EndsWith(Object item, String endsWith, Boolean ignoreCase = False, Boolean isNot = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

For a step that tests how a value ends, e.g. `if %path% ends with ".md" then`. For the negative form use NotEndsWith.

- `item` *Object* — The value being tested, the left side, e.g. %path% in `if %path% ends with ".md"`
- `endsWith` *String* — What it is tested against, the right side, e.g. ".md"
- `ignoreCase` *Boolean*, default `False` — true when letter case must not matter. false unless the step asks for that
- `isNot` *Boolean*, default `False` — true when the step negates the test. Prefer the NotEndsWith method, which says it in its name
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %file% ends with ".csv" then => item="%file%", endsWith=".csv"

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

Is a value empty or null, e.g. `if %question% is empty then`. This is the dedicated method for that test and is the one to use, rather than SimpleCondition with its isEmpty operator

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
- `ignoreCase` *Boolean*, default `True` — true is the normal case, letter case does not matter. false only when the step asks for an exact, case sensitive comparison
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
- `ignoreCase` *Boolean*, default `True` — true is the normal case, letter case does not matter. false only when the step asks for an exact, case sensitive comparison
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

### NotContainsString

```
NotContainsString(Object item, String contains, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

For a step that says the value does NOT contain something, e.g. `if %user.role% does not contain "admin" then`. For the positive form use ContainsString.

- `item` *Object* — The value being tested, the left side, e.g. %user.role% in `if %user.role% does not contain "admin"`
- `contains` *String* — What it is tested against, the right side, e.g. "admin"
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %tags% does not contain "draft" then => item="%tags%", contains="draft"

### NotEndsWith

```
NotEndsWith(Object item, String endsWith, Boolean ignoreCase = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

For a step that says the value does NOT end with something, e.g. `if %path% does not end with ".md" then`. For the positive form use EndsWith.

- `item` *Object* — The value being tested, the left side, e.g. %path% in `if %path% does not end with ".md"`
- `endsWith` *String* — What it is tested against, the right side, e.g. ".md"
- `ignoreCase` *Boolean*, default `False` — true when letter case must not matter. false unless the step asks for that
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %file% does not end with ".tmp" then => item="%file%", endsWith=".tmp"

### NotStartsWith

```
NotStartsWith(Object item, String startsWith, Boolean ignoreCase = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

For a step that says the value does NOT start with something, e.g. `if %goal% does not start with "tests/" then`. For the positive form use StartsWith.

- `item` *Object* — The value being tested, the left side, e.g. %goal% in `if %goal% does not start with "tests/"`
- `startsWith` *String* — What it is tested against, the right side, e.g. "tests/"
- `ignoreCase` *Boolean*, default `False` — true when letter case must not matter. false unless the step asks for that
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %file% does not start with "tmp/" then => item="%file%", startsWith="tmp/"

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

One condition made of a value and an operator, for cases no dedicated method covers. When a dedicated method exists for the test, use that instead: IsEmpty for `is empty`, IsEqual for `==`, StartsWith for `starts with`, ContainsString for `contains`.
Operator: ==|!=|<|>|<=|>=|in|notIn|isEmpty|isNotEmpty|contains|notContains|startswith|notStartswith|endswith|notEndswith|indexOf|matches|notMatches. Equality is `==` (never a single `=`). A test written in the negative takes the negative operator and IsNot stays false, e.g. `does not contain` => notContains.
Logic: convert "&&" => "AND", "||" => "OR"
A goal the step calls goes in goalToCallIfTrue (after `then`) and goalToCallIfFalse (after `else`), also when the whole if is written on one line. Never leave them out when the step says `call`.

- `condition` *PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition* — (see Type information in SupportingObjects)
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

Examples:

- if %codeCount% >= 2 then call goal HandleOverLimit, else call goal IssueCodeNow => condition={"Kind":"Simple","LeftValue":"%codeCount%","Operator":">=","RightValue":2}, goalToCallIfTrue={Name="HandleOverLimit"}, goalToCallIfFalse={Name="IssueCodeNow"}
- if %total% > 100 then call ApplyDiscount amount=%total% => condition={"Kind":"Simple","LeftValue":"%total%","Operator":">","RightValue":100}, goalToCallIfTrue={Name="ApplyDiscount", Parameters={"amount":"%total%"}}

### StartsWith

```
StartsWith(Object item, String startsWith, Boolean isNot = False, Boolean ignoreCase = False, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

isNot property reverse true to false, example: `if %name% starts with "john" then`, `if %source% starts with "t" then call goal Track`, `if %name% does not start with "bill"` (isNot=true). Use ignoreCase=true to compare case insensitive.

- `item` *Object* — The value being tested, the left side of the comparison, e.g. %path% in `if %path% starts with "/"`
- `startsWith` *String* — What it is tested against, the right side, e.g. "/" in `if %path% starts with "/"`
- `isNot` *Boolean*, default `False` — true when the step negates the test, e.g. `does not contain`, `does not start with`, `is not`. false when the step states the test plainly
- `ignoreCase` *Boolean*, default `False` — true when letter case must not matter in the comparison. false unless the step asks for that
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

