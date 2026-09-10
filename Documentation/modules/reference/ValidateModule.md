# ValidateModule

Hint: `[validate]`  
Type: `PLang.Modules.ValidateModule.Program`

DO NOT USE for if statements. Validates a variable, make sure it's not empty, follows a pattern, is a number, etc.

## Methods

### HasPattern

```
HasPattern(String[] variables, String pattern, String errorMessage, Int32 statusCode = 400) : object
```

Checks if variable contains a regex pattern. Create error message fitting the intent of the validation


### IsLength

```
IsLength(String variableName, Int32 length, String compairer, String errorMessage, Int32 statusCode = 400) : object
```

compairer:==|<|>|<=|>=|!=


### IsNotEmpty

```
IsNotEmpty(List<PLang.Runtime.ObjectValue> variables, PLang.Services.OutputStream.Messages.ErrorMessage errorMessage) : object
```

Check each %variable% if it is empty. Create error message fitting the intent of the validation. channel=error|warning|info|debug|trace. channel is null unless sepecifically defined by user e.g. channel:error

- `variables` *List<PLang.Runtime.ObjectValue>* — (see Type information in SupportingObjects)
- `errorMessage` *PLang.Services.OutputStream.Messages.ErrorMessage* — (see Type information in SupportingObjects)

### IsNotEmpty

```
IsNotEmpty(List<PLang.Runtime.ObjectValue> variables, String errorMessage, Int32 statusCode = 400) : object
```

[Depricated] Check each %variable% if it is empty. Create error message fitting the intent of the validation. Extract all %variables% from this statement as a JSON array of strings, ensuring it is not wrapped as a single string. channel=error|warning|info|debug|trace. channel is null unless sepecifically defined by user e.g. channel:error

- `variables` *List<PLang.Runtime.ObjectValue>* — (see Type information in SupportingObjects)
- `errorMessage` *String*
- `statusCode` *Int32*, default `400`

### IsValid2LetterCountryCode

```
IsValid2LetterCountryCode(String[] variables, String pattern, String errorMessage, Int32 statusCode = 400) : object
```

Checks if variable is a valid 2 letter country code


### ValidateFileExtension

```
ValidateFileExtension(List<String> allowedExtensions = null, String fileName) : object
```


### ValidateItemIsInList

```
ValidateItemIsInList(Object[] itemsToCheckInList, Collections.IList list, String errorMessage = item is not in list, Boolean caseSensitive = False) : List<Object>
```

- `itemsToCheckInList` *Object[]*
- `list` *Collections.IList* — (see Type information in SupportingObjects)
- `errorMessage` *String*, default `item is not in list`
- `caseSensitive` *Boolean*, default `False`

### ValidateSignedContract

```
ValidateSignedContract(Object contract, List<String> signatureProperties = null, List<String> propertiesToMatch = null) : object
```

Validates if signed contract is valid. Uses properties on the contract from signatureProperties as signatures to validate against. propertiesToMatch specifies properties that should be used on the validation, when null all poperties are used except signatureProperties


### ValidateType

```
ValidateType(PLang.Modules.ValidateModule.Program+TypeValidation validation) : object
```

Validates that a value can be converted to a specified type. Supported types: long/int64, int/int32, short/int16, byte, double, float/single, decimal, bool/boolean, guid, datetime, string

- `validation` *PLang.Modules.ValidateModule.Program+TypeValidation* — (see Type information in SupportingObjects)

Examples:

- validate %id% is long => TypeValidation.Value=%id%, TypeValidation.TypeName="long"
- validate %price% is decimal => TypeValidation.Value=%price%, TypeValidation.TypeName="decimal"
- validate %price% is decimal with culture "de-DE" => TypeValidation.Value=%price%, TypeValidation.TypeName="decimal", TypeValidation.Culture="de-DE"
- validate %count% is int => TypeValidation.Value=%count%, TypeValidation.TypeName="int"
- validate %isActive% is bool => TypeValidation.Value=%isActive%, TypeValidation.TypeName="bool"
- validate %userId% is guid => TypeValidation.Value=%userId%, TypeValidation.TypeName="guid"
- validate %startDate% is datetime => TypeValidation.Value=%startDate%, TypeValidation.TypeName="datetime"
- validate %startDate% is datetime format "yyyy-MM-dd" => TypeValidation.Value=%startDate%, TypeValidation.TypeName="datetime", TypeValidation.Format="yyyy-MM-dd"

