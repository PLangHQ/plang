# Assert Module

Test assertions for validating values in PLang tests. Each assertion returns an error if the check fails, stopping the test.

## Actions

### equals

Assert two values are equal.

```plang
- assert %result% equals 42
- assert %name% equals 'John', 'Name should be John'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Expected | object | yes | Expected value |
| Actual | object | yes | Actual value |
| Message | string | no | Custom error message |

Handles numeric type coercion — `42` (int) equals `42` (long).

### notEquals

Assert two values are not equal.

```plang
- assert %status% not equals 'error'
```

### contains

Assert a container includes a value.

```plang
- assert %text% contains 'hello'
- assert %items% contains 'apple'
```

Works with strings (substring match), lists, and dictionaries.

### greaterThan

Assert A is greater than B.

```plang
- assert %score% greater than 80
```

### lessThan

Assert A is less than B.

```plang
- assert %errors% less than 5
```

### isTrue

Assert a value is truthy.

```plang
- assert %isActive% is true
- assert %isActive% is true, 'User should be active'
```

### isFalse

Assert a value is falsy.

```plang
- assert %isDeleted% is false
```

### isNull

Assert a value is null.

```plang
- assert %result% is null
```

### isNotNull

Assert a value is not null.

```plang
- assert %data% is not null, 'Data should be loaded'
```

## Error Behavior

When an assertion fails, it returns an `AssertionError` with the expected vs. actual values. If you provide a custom message, it's included in the error.

Failed assertions stop the current goal execution — they don't just log a warning.

## Examples

### Test File

```plang
Start
- set %items% = ["apple", "banana", "cherry"]
- count items in %items%, write to %total%
- assert %total% equals 3, "should have 3 items"
- assert %items% contains 'banana', "should contain banana"

/ Test after modification
- remove 'banana' from %items%
- count items in %items%, write to %total%
- assert %total% equals 2, "should have 2 items after removal"
```

### Test with Error Handling

```plang
Start
- set %value% = 42
- assert %value% equals 42
- assert %value% greater than 0
- assert %value% is not null
```
