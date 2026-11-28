# VariableModule

Hint: `[variable]`  
Type: `PLang.Modules.VariableModule.Program`

Set, Get & Return variable(s). Load and store %variable%s so their value is kept between runs: a step that starts with `load %x%` or `store %x%` (save, persist) names a %variable%, never a table, and belongs here even when it names a datasource, e.g. `store %round%, ds: "data"`. Set(NOT if statement) on variable includes condition such as empty or null, and a fallback written as `, default is X`, `, or X if empty` or `?? X`. A step that starts with `set %variable% =` belongs here, never to the database module, even when the word default appears. A step that sets something that is not written as a %variable% is not a variable step: `set debug mode = false`, `set debug mode on` and `remove debug mode` switch the app's debug mode and belong to the environment module, `set culture to is-IS` too. Bind onCreate, onChange, onRemove events to variable. Trim variable for llm. Use this module on `- return %variable`. Modify datetime (%now%, etc.)

## Methods

### AppendToVariable

```
AppendToVariable(String key, Object value = null, Char seperator = 
, String valueLocation = postfix, String seperatorLocation = end, Boolean shouldBeUnique = False, Boolean doNotLoadVariablesInValue = False) : Object
```

Append to variable. valueLocation=postfix|prefix seperatorLocation=end|start


### ConvertToBase64

```
ConvertToBase64(String key) : String
```


### GetEnvironmentVariable

```
GetEnvironmentVariable(String key) : String
```


### GetSettings

```
GetSettings(String key) : object
```


### GetTimeSpanRelated

```
GetTimeSpanRelated(DateTime fromDate, DateTime toDate, String pattern) : Object
```

Converts 2 dates to a number or timespan, such as days ago.
pattern=days|hours|minutes|seconds|milliseconds|nanoseconds|ticks|totaldays|totalhours|totalminutes|totalseconds|totalmilliseconds|totalnanoseconds


Examples:

- set %days% = total days between %fromDate% and %toDate% => pattern="totaldays"

### GetVariable

```
GetVariable(String key) : Object
```


### Load

```
Load(List<String> variables = null, String dataSourceName = null) : Object
```

Loads %variable%s that an earlier run saved with store. A variable that has not been stored is empty

- `variables` *List<String>*, default `null`
- `dataSourceName` *String*, default `null` — The datasource the step names, e.g. data in `ds: "data"`. Leave it out when the step names none: the app's default datasource is used

Examples:

- load %step% => variables=["%step%"]
- load %step%, ds: "data" => variables=["%step%"], dataSourceName="data"

### LoadVariables

```
LoadVariables(String key) : Object
```


### LoadWithDefaultValue

```
LoadWithDefaultValue(Dictionary<String, Object> variablesWithDefaultValue = null, String dataSourceName = null) : Object
```

Loads(not setting a variable) %variable%s that an earlier run saved with store, each with a default value used when it has not been stored. key: variable name, value: default value. example: load %age%, default 10 => key:%age% value:10

- `variablesWithDefaultValue` *Dictionary<String, Object>*, default `null`
- `dataSourceName` *String*, default `null` — The datasource the step names, e.g. data in `ds: "data"`. Leave it out when the step names none: the app's default datasource is used

Examples:

- load %step%, set as 1 if empty => variablesWithDefaultValue={"%step%": 1}
- load %samples%, default [] => variablesWithDefaultValue={"%samples%": []}
- load %round%, default 0, ds: "data" => variablesWithDefaultValue={"%round%": 0}, dataSourceName="data"

### ModifyDateTime

```
ModifyDateTime(PLang.Modules.VariableModule.Program+ModifyDateTimeParameters p) : Object
```

Modifies a DateTime by applying one or more operations.

- `p` *PLang.Modules.VariableModule.Program+ModifyDateTimeParameters* — (see Type information in SupportingObjects)

Examples:

- remove 1 day from %fromDate%, format 'yyyy-MM-dd' => Operations=[{Method='AddDays', Parameters=[-1]}], Format='yyyy-MM-dd'
- add 3 months to %startDate% => Operations=[{Method='AddMonths', Parameters=[3]}]
- subtract 2 hours from %timestamp% => Operations=[{Method='AddHours', Parameters=[-2]}]
- remove 26 hours from %now% => Operations=[{Method='AddHours', Parameters=[-26]}]
- set %date% as %now% but add one month then subtract 2 days => Operations=[{Method='AddMonths', Parameters=[1]}, {Method='AddDays', Parameters=[-2]}]
- take %startDate%, add 1 year, add 6 months, subtract 1 day => Operations=[{Method='AddYears', Parameters=[1]}, {Method='AddMonths', Parameters=[6]}, {Method='AddDays', Parameters=[-1]}]
- add %months% months then %days% days to %date%, format 'yyyy-MM-dd' => Operations=[{Method='AddMonths', Parameters=[%months%]}, {Method='AddDays', Parameters=[%days%]}], Format='yyyy-MM-dd'

### OnChangeVariablesListener

```
OnChangeVariablesListener(List<String> keys = null, PLang.Models.GoalToCallInfo goalName, Boolean notifyWhenCreated = True, Boolean waitForResponse = True, Int32 delayWhenNotWaitingInMilliseconds = 50) : object
```

- `keys` *List<String>*, default `null`
- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `notifyWhenCreated` *Boolean*, default `True`
- `waitForResponse` *Boolean*, default `True`
- `delayWhenNotWaitingInMilliseconds` *Int32*, default `50`

### OnCreateVariablesListener

```
OnCreateVariablesListener(List<String> keys = null, PLang.Models.GoalToCallInfo goalName, Boolean waitForResponse = True, Int32 delayWhenNotWaitingInMilliseconds = 50) : object
```

- `keys` *List<String>*, default `null`
- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `waitForResponse` *Boolean*, default `True`
- `delayWhenNotWaitingInMilliseconds` *Int32*, default `50`

### OnRemoveVariablesListener

```
OnRemoveVariablesListener(List<String> keys = null, PLang.Models.GoalToCallInfo goalName, Boolean waitForResponse = True, Int32 delayWhenNotWaitingInMilliseconds = 50) : object
```

- `keys` *List<String>*, default `null`
- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `waitForResponse` *Boolean*, default `True`
- `delayWhenNotWaitingInMilliseconds` *Int32*, default `50`

### RemoveVariables

```
RemoveVariables(String[] keys) : object
```


### Return

```
Return(Dictionary<String, Object> variables = null) : object
```

Return one or more variables out of the goal. variables is always set: a step that names a variable to return always has at least one entry.


Examples:

- return %result% => variables={"result": "%result%"}
- return %total%, %count% => variables={"total": "%total%", "count": "%count%"}
- return %user.name% as %name% => variables={"name": "%user.name%"}
- return !callback=%callback% => variables={"!callback": "%callback%"}

### SetBoolVariable

```
SetBoolVariable(String key, Nullable<Boolean> value = null, Nullable<Boolean> defaultValue = null) : object
```

Set bool variable.


### SetBoolVariableWithCondition

```
SetBoolVariableWithCondition(String key, PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition simpleCondition, Nullable<Boolean> defaultValue = null) : object
```

Set bool variable width condition.

- `key` *String*
- `simpleCondition` *PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition* — (see Type information in SupportingObjects)
- `defaultValue` *Nullable<Boolean>*, default `null`

### SetDateTimeVariable

```
SetDateTimeVariable(String key, Object value = null) : object
```

Set date time variable. Example: set %lastUpdate% = "2020-01-01 12:20", or set %yesterday% = %now.AddDays(-1)%. When date/time being set, format it to ISO 8601.


### SetDefaultNumberVariable

```
SetDefaultNumberVariable(String key, Nullable<Int64> value = null, Nullable<Int64> defaultValue = null, Nullable<Int64> maxValue = null, Nullable<Int64> minValue = null) : object
```

Set default value of int/long variable. If value already exists it wont be set.


### SetDefaultSettingValue

```
SetDefaultSettingValue(String key, Object value) : object
```

Sets a value to %Settings.XXXX% variable but only if it is not set before


### SetDefaultValueOnVariables

```
SetDefaultValueOnVariables(Dictionary<String, Object> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

Set default value on variables if not set, good for setting value if variable is empty. Number can be represented with _, e.g. 100_000. A json object or array is the value itself, written as json, and never a string holding json: {{"ok":true}} and NOT "{{\"ok\":true}}". Written as a string the variable holds text, and reading a property off it, e.g. %lookup.ok%, finds nothing.  onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.
Example:
`set default value %name% = %request.body.name%, %zip% = %request.body.zip%` => [{key=%name%, value=%request.body.zip%}, {key=%zip%, value=%request.body.zip%}]
`set default value %target% = "#body" => {key=%target%, value="#body"}

Bad (dont use for):
`set default %page% = %request.query.page% ?? 1` => use:SetValueOnVariablesOrDefaultIfValueIsEmpty

- `keyValues` *Dictionary<String, Object>*, default `null`
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `keyIsDynamic` *Boolean*, default `False`
- `onlyIfValueIsNot` *Object*, default `null` — Almost always unset. Only when the step says the assignment is conditional on the current value, e.g. `set %x% = 1 only if it is not null`

### SetDoubleVariable

```
SetDoubleVariable(String key, Double value, Nullable<Double> maxValue = null, Nullable<Double> minValue = null) : object
```

Set double variable.


### SetDoubleVariables

```
SetDoubleVariables(Dictionary<String, Double> values = null) : object
```

Set multiple double variables.


### SetFloatVariable

```
SetFloatVariable(String key, Nullable<Single> value = null, Nullable<Single> defaultValue = null) : object
```

Set float variable.


### SetFloatVariable

```
SetFloatVariable(Dictionary<String, Single> values = null) : object
```

Set multiple float variables.


### SetJsonObjectVariable

```
SetJsonObjectVariable(String key, Object value = null, Boolean doNotLoadVariablesInValue = False, Object defaultValue = null) : object
```

Set one variable to a json object or array written out in the step, e.g. set %lookup% = {{"ok": true, "code": "%code%"}}. The value is the json itself, never a string holding json: {{"ok":true}} and NOT "{{\"ok\":true}}".

- `key` *String*
- `value` *Object*, default `null`
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `defaultValue` *Object*, default `null` — A fallback, used only when the step itself states one, e.g. `set %x% = %y%, default is 10` or `%y% ?? 10`. When the step just assigns a value, this stays unset: the value belongs to the value parameter, not here

Examples:

- set %result% = {"ok": true, "id": "%id%"} => key="%result%", value={"ok": true, "id": "%id%"}

### SetNumberVariable

```
SetNumberVariable(String key, Nullable<Int64> value = null, Nullable<Int64> defaultValue = null, Nullable<Int64> maxValue = null, Nullable<Int64> minValue = null) : object
```

Set int/long variable.


### SetSettingValue

```
SetSettingValue(String key, Object value) : object
```

Sets a value to %Settings.XXXX% variable


### SetStringVariable

```
SetStringVariable(String key, String value = null, Boolean urlDecode = False, Boolean htmlDecode = False, Boolean doNotLoadVariablesInValue = False, String defaultValue = null, Boolean sensitive = False) : object
```

Set one variable to a text value written in single/double quotes in the step, e.g. set %name% = "John". The wrapping quotes are not part of the value. A quoted text with %variables% written inside it is still a quoted text and belongs here, e.g. set %target% = "/%path%" or set %msg% = "Halló %name%": the variables are replaced with their values inside the text, and the wrapping quotes are still dropped. Only a value that is not wrapped in quotes at all, a bare %variable%, an object or json, belongs to SetVariable or SetJsonObjectVariable

- `key` *String* — The variable being set: the name on the LEFT of the =, e.g. target in `set %target% = "/%path%"`. Never the value on the right
- `value` *String*, default `null` — What the variable is set to: everything on the RIGHT of the =, with the wrapping quotes dropped, e.g. /%path% in `set %target% = "/%path%"`
- `urlDecode` *Boolean*, default `False` — true only when the step says the value is url encoded and should be decoded. false is the normal case
- `htmlDecode` *Boolean*, default `False` — true only when the step says the value is html encoded and should be decoded. false is the normal case
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `defaultValue` *String*, default `null` — A fallback, used only when the step itself states one, e.g. `set %x% = %y%, default is 10` or `%y% ?? 10`. When the step just assigns a value, this stays unset: the value belongs to the value parameter, not here
- `sensitive` *Boolean*, default `False` — true only when the step says `as sensitive`, e.g. `set %token% = "Bearer %t%" as sensitive`: the value is never shown in an error, the log or an llm request. false is the normal case

### SetValueAndStore

```
SetValueAndStore(Dictionary<String, Object> variables = null, String dataSourceName = null) : object
```

Set value to a variable then Store/Save variable(s) in a persistant storage. The storing of the variables must be defined by user.

- `variables` *Dictionary<String, Object>*, default `null`
- `dataSourceName` *String*, default `null` — The datasource the step names, e.g. data in `ds: "data"`. Leave it out when the step names none: the app's default datasource is used

### SetValueOnVariablesOrDefaultIfValueIsEmpty

```
SetValueOnVariablesOrDefaultIfValueIsEmpty(List<PLang.Modules.VariableModule.Program+VariableIfEmpty> variables, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

- `variables` *List<PLang.Modules.VariableModule.Program+VariableIfEmpty>* — (see Type information in SupportingObjects)
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `keyIsDynamic` *Boolean*, default `False`
- `onlyIfValueIsNot` *Object*, default `null` — Almost always unset. Only when the step says the assignment is conditional on the current value, e.g. `set %x% = 1 only if it is not null`

### SetValuesOnVariables

```
SetValuesOnVariables(Dictionary<String, Object> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

Set two or more variables in one step, none of them with a default value, e.g. set %page% = %request.body.page%, %note% = %request.body.note%. If any variable has a default, SetVariables is the method. A json object or array is the value itself, written as json, and never a string holding json: {{"ok":true}} and NOT "{{\"ok\":true}}". Written as a string the variable holds text, and reading a property off it, e.g. %lookup.ok%, finds nothing.  onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.

- `keyValues` *Dictionary<String, Object>*, default `null`
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `keyIsDynamic` *Boolean*, default `False`
- `onlyIfValueIsNot` *Object*, default `null` — Almost always unset. Only when the step says the assignment is conditional on the current value, e.g. `set %x% = 1 only if it is not null`

### SetVariable

```
SetVariable(String key, Object value = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null, Object defaultValue = null, String FullTypeName = null, Boolean sensitive = False) : object
```

Set one variable to a value that is not a quoted text: a bare %variable% such as set %question% = %answer.text%, an object, a list or json. Any value the step wraps in quotes belongs to SetStringVariable, including one with %variables% inside it such as set %target% = "/%path%": this method keeps the quote characters as part of the value, so %target% would hold the quotes as text and a file read on it finds nothing. A json object or array is the value itself, written as json, and never a string holding json: {{"ok":true}} and NOT "{{\"ok\":true}}". Written as a string the variable holds text, and reading a property off it, e.g. %lookup.ok%, finds nothing

- `key` *String* — The variable being set: the name on the LEFT of the =, e.g. path in `set %path% = %request.query.path%`. Never the variable being read on the right
- `value` *Object*, default `null` — What the variable is set to: everything on the RIGHT of the =, e.g. %request.query.path% in `set %path% = %request.query.path%`
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `keyIsDynamic` *Boolean*, default `False`
- `onlyIfValueIsNot` *Object*, default `null` — Almost always unset. Only when the step says the assignment is conditional on the current value, e.g. `set %x% = 1 only if it is not null`
- `defaultValue` *Object*, default `null` — A fallback, used only when the step itself states one, e.g. `set %x% = %y%, default is 10` or `%y% ?? 10`. When the step just assigns a value, this stays unset: the value belongs to the value parameter, not here
- `FullTypeName` *String*, default `null` — Almost always unset. Only when the step asks for the value to become a particular .NET type, e.g. `set %n% = "42" as int`. A value that is simply assigned keeps its own type
- `sensitive` *Boolean*, default `False` — true only when the step says `as sensitive`, e.g. `set %pw% = %request.password% as sensitive`: the value is never shown in an error, the log or an llm request. false is the normal case

### SetVariables

```
SetVariables(Dictionary<String, Tuple<Object, Object>> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

Set two or more variables in one step where at least one of them has a default value, e.g. set %page% = %request.query.page% ?? 1, %size% = 20. When no variable has a default, SetValuesOnVariables is the method. Number can be represented with _, e.g. 100_000. A json object or array is the value itself, written as json, and never a string holding json: {{"ok":true}} and NOT "{{\"ok\":true}}". Written as a string the variable holds text, and reading a property off it, e.g. %lookup.ok%, finds nothing. onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.

- `keyValues` *Dictionary<String, Tuple<Object, Object>>*, default `null`
- `doNotLoadVariablesInValue` *Boolean*, default `False` — true only when the step asks for a %variable% written in the value to be kept as literal text instead of being replaced with its value. false is the normal case
- `keyIsDynamic` *Boolean*, default `False`
- `onlyIfValueIsNot` *Object*, default `null` — Almost always unset. Only when the step says the assignment is conditional on the current value, e.g. `set %x% = 1 only if it is not null`

### SetVariableWithCalculation

```
SetVariableWithCalculation(String key, String expression, Int32 decimalRound = 2, Nullable<MidpointRounding> midpointRounding = null) : object
```

Set variable by calcuation, e.g. `set %total% = %quantity% * %price%`. Uses NCalc.Expression method, make sure it is valid NCalc expression. Capitalize any functions called like sqrt() into Sqrt()


Examples:

- set %total% = 5 * %item.amount%, round 4 => key="%total%", expression="5 * %item.amount%, decimalRound=4

### SetVariableWithCondition

```
SetVariableWithCondition(String variableName, Object value, Object valueIfTrue, Object valueIfFalse) : object
```


### Store

```
Store(List<String> variables = null, String dataSourceName = null) : object
```

Store/Save %variable%s in a persistant storage, so a later run can load them. `store %x%` names a variable, never a table

- `variables` *List<String>*, default `null`
- `dataSourceName` *String*, default `null` — The datasource the step names, e.g. data in `ds: "data"`. Leave it out when the step names none: the app's default datasource is used

Examples:

- store %round% => variables=["%round%"]
- store %samples%, %round%, ds: "data" => variables=["%samples%", "%round%"], dataSourceName="data"

### TrimForLlm

```
TrimForLlm(Object obj, Int32 maxItemCount = 30, Nullable<Int32> maxItemLength = null, String groupOn = null, Int32 samplesPerGroup = 5, Int32 listLimit = 50, Int32 totalCharsLimit = 2000, Boolean formatJson = False) : Object
```


