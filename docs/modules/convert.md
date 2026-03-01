# Convert Module

Convert values between types: JSON, numbers, booleans, dates, strings, and base64.

## Actions

### toJson

Serialize a value to a JSON string.

```plang
- set %user% = {name: "John", age: 30}
- convert %user% to json, write to %json%
/ '{"name":"John","age":30}'

/ Pretty-printed
- convert %user% to json indented, write to %prettyJson%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Value | object | no | — | Value to serialize |
| Indent | bool | no | false | Pretty-print the output |

### fromJson

Parse a JSON string into an object.

```plang
- set %json% = '{"name":"John","age":30}'
- parse %json% from json, write to %user%
- write out %user.name%    / "John"
```

**Errors:**

| Key | When |
|-----|------|
| JsonParseError | Invalid JSON syntax |
| JsonDepthExceeded | JSON nesting too deep |

### toInt

Convert to a 32-bit integer.

```plang
- convert '42' to int, write to %num%
```

**Error:** Returns `ConversionError` if the value can't be converted.

### toLong

Convert to a 64-bit integer.

```plang
- convert %bigNumber% to long, write to %result%
```

**Error:** Returns `ConversionError` if the value can't be converted.

### toDouble

Convert to a double-precision floating point.

```plang
- convert '3.14' to double, write to %pi%
```

**Error:** Returns `ConversionError` if the value can't be converted.

### toBool

Convert to a boolean.

```plang
- convert 'true' to bool, write to %flag%
- convert 1 to bool, write to %flag%       / true
- convert 'yes' to bool, write to %flag%   / true
```

Truthy values: `true`, `"true"`, `"1"`, `"yes"` (case-insensitive). Everything else evaluates based on whether the value is non-null.

### toDateTime

Convert to a DateTime.

```plang
- convert '2024-01-15' to datetime, write to %date%

/ With format
- convert '15/01/2024' to datetime format 'dd/MM/yyyy', write to %date%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Value | object | yes | — | Value to convert |
| Format | string | no | — | Exact format string (e.g., "yyyy-MM-dd") |

**Error:** Returns `ConversionError` if the value can't be parsed.

### toString

Convert to a string.

```plang
- convert %number% to string, write to %text%

/ With format
- convert %price% to string format 'C2', write to %formatted%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Value | object | no | — | Value to convert |
| Format | string | no | — | .NET format string |

### toBase64

Encode a value as base64.

```plang
- convert 'Hello world' to base64, write to %encoded%
```

### fromBase64

Decode a base64 string.

```plang
- convert %encoded% from base64, write to %decoded%

/ Get raw bytes
- convert %encoded% from base64 as bytes, write to %bytes%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Value | string | yes | — | Base64 string |
| AsBytes | bool | no | false | Return byte array instead of string |

**Error:** Returns `ConversionError` for invalid base64.
