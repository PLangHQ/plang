# AssertModule

Hint: `[assert]`  
Type: `PLang.Modules.AssertModule.Program`

Assert object/variable/text or other entity to be what is epxected. For unit testing

## Methods

### Contains

```
Contains(Object contains, Object actualValue) : object
```

User can force the type of expectedValue and actualValue, it should be FullName type, e.g. System.Int64, System.Double, etc. By default the types are not set and the runtime will try to match them


### IsEqual

```
IsEqual(Object expectedValue, Object actualValue, String resultVariable = assertResult, String expectedValueType = null, String actualValueType = null) : object
```

User can force the type of expectedValue and actualValue, it should be FullName type, e.g. System.Int64, System.Double, etc. By default the types are not set and the runtime will try to match them


