# VariableModule

Hint: `[variable]`  
Type: `PLang.Modules.VariableModule.Program`

Set, Get & Return variable(s). Set(NOT if statement) on variable includes condition such as empty or null. Bind onCreate, onChange, onRemove events to variable. Trim variable for llm. Use this module on `- return %variable`. Modify datetime (%now%, etc.)

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


Examples:

- load %step% => variables=["%step%"]

### LoadVariables

```
LoadVariables(String key) : Object
```


### LoadWithDefaultValue

```
LoadWithDefaultValue(Dictionary<String, Object> variablesWithDefaultValue = null, String dataSourceName = null) : Object
```

Loads(not setting a variable) a variable with a default value, key: variable name, value: default value. example: load %age%, default 10 => key:%age% value:10


Examples:

- load %step%, set as 1 if empty => variablesWithDefaultValue=["%step%", 1]

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

One or more variables to return. Variable can contain !, e.g. !callback=%callback%. When key is undefined, it is same as value, e.g. return %name% => then variables dictionary has key and value as name=%name%


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

Set default value on variables if not set, good for setting value if variable is empty. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote(") by escaping it.  onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.
Example:
`set default value %name% = %request.body.name%, %zip% = %request.body.zip%` => [{key=%name%, value=%request.body.zip%}, {key=%zip%, value=%request.body.zip%}]
`set default value %target% = "#body" => {key=%target%, value="#body"}

Bad (dont use for):
`set default %page% = %request.query.page% ?? 1` => use:SetValueOnVariablesOrDefaultIfValueIsEmpty


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

Set json variable. Make sure value is valid json


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
SetStringVariable(String key, String value = null, Boolean urlDecode = False, Boolean htmlDecode = False, Boolean doNotLoadVariablesInValue = False, String defaultValue = null) : object
```

Set string variable. Developer might use single/double quote to indicate the string value, the wrapped quote should not be included in the value. If value is json, make sure to format it as valid json, use double quote(") by escaping it


### SetValueAndStore

```
SetValueAndStore(Dictionary<String, Object> variables = null, String dataSourceName = null) : object
```

Set value to a variable then Store/Save variable(s) in a persistant storage. The storing of the variables must be defined by user.


### SetValueOnVariablesOrDefaultIfValueIsEmpty

```
SetValueOnVariablesOrDefaultIfValueIsEmpty(List<PLang.Modules.VariableModule.Program+VariableIfEmpty> variables, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

Set value on variables or a default value is value is empty. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote(") by escaping it.  onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.

- `variables` *List<PLang.Modules.VariableModule.Program+VariableIfEmpty>* — (see Type information in SupportingObjects)
- `doNotLoadVariablesInValue` *Boolean*, default `False`
- `keyIsDynamic` *Boolean*, default `False`
- `onlyIfValueIsNot` *Object*, default `null`

Examples:

- set %q% = %request.query.q%, or "hello" if empty => keyValues.key="%q%", value=["%request.query.q%", "hello"]
- set %q% = %request.query.page% ?? 1 => keyValues.key="%page%", value=["%request.query.page%", 1, "System.Int64"]

### SetValuesOnVariables

```
SetValuesOnVariables(Dictionary<String, Object> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

Set value on variables. If value is json, make sure to format it as valid json, use double quote(") by escaping it.  onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.


### SetVariable

```
SetVariable(String key, Object value = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null, Object defaultValue = null, String FullTypeName = null) : object
```

Set variable. Developer might use single/double quote to indicate the string value. If value is json, make sure to format it as valid json, use double quote(") by escaping it


### SetVariables

```
SetVariables(Dictionary<String, Tuple<Object, Object>> keyValues = null, Boolean doNotLoadVariablesInValue = False, Boolean keyIsDynamic = False, Object onlyIfValueIsNot = null) : object
```

Set multiple variables with possible default values used with variable(such as %request.query.*% and fix default. Number can be represented with _, e.g. 100_000. If value is json, make sure to format it as valid json, use double quote(") by escaping it. onlyIfValueIsSet can be define by user, null|"null"|"empty" or value a user defines. Be carefull, there is difference between null and "null", to be "null" is must be defined by user.


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

Store/Save variable(s) in a persistant storage


### TrimForLlm

```
TrimForLlm(Object obj, Int32 maxItemCount = 30, Nullable<Int32> maxItemLength = null, String groupOn = null, Int32 samplesPerGroup = 5, Int32 listLimit = 50, Int32 totalCharsLimit = 2000, Boolean formatJson = False) : Object
```


