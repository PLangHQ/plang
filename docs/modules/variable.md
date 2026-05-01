# Variable Module

Set, get, and manage variables in the memory stack.

Variables in PLang use `%percent%` delimiters and support dot notation for object properties, bracket notation for array access, and special accessors like `.first`, `.last`, and `.random`.

## Actions

### set

Store a value in a variable.

```plang
/ Simple values
- set %name% = 'John'
- set %age% = 30
- set %active% = true

/ Objects
- set %user% = {name: "John", age: 30}

/ Arrays
- set %colors% = ["red", "green", "blue"]

/ With explicit type
- set %count% = '42', type 'int'

/ Set only when unset — useful for goal parameters with a fallback default
- set default %path% = '.'
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Name | string | yes | — | Variable name |
| Value | object | no | — | Value to store |
| Type | string | no | — | Type hint (int, string, bool, etc.) |
| AsDefault | bool | no | false | When true, only sets if the variable doesn't already exist; existing value wins |

#### `set default`

`set default` is the natural form for "use this value, but only if the caller hasn't already provided one." It's the typical pattern for goal-parameter fallbacks:

```plang
ProcessFolder
- set default %path% = '.'
- list files in %path%, write to %files%
```

A caller passing `path=/data` overrides the default; a caller passing nothing falls through to `'.'`. Internally this maps to `variable.set` with `AsDefault=true` — the action checks the variable's `IsInitialized` state before writing.

#### Lists and dictionaries are independent copies

When you `set %x% = %y%` and `%y%` is a list or dictionary, `%x%` is a snapshot — a fresh copy. Mutating `%y%` later does **not** bleed into `%x%`:

```plang
- set %a% = ["one", "two"]
- set %b% = %a%
- add "three" to %a%
/ %a% is ["one","two","three"], %b% is still ["one","two"]
```

If you want both names to refer to the same underlying list (so `add` is visible from either), keep using one name — don't re-assign.

### get

Retrieve a variable's value. Usually you just reference `%varName%` directly — the `get` action is for when you need to check if a variable exists.

```plang
- get %name%, write to %result%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Variable name to retrieve |

### exists

Check if a variable exists in the current scope.

```plang
- check if %name% exists, write to %nameExists%
- if %nameExists% is true then call HandleName
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Variable name to check |

**Returns:** `true` if the variable exists, `false` otherwise.

### remove

Remove a variable from the memory stack.

```plang
- remove variable %tempData%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Variable name to remove |

### clear

Remove all variables from the current memory scope.

```plang
- clear all variables
```

No parameters.

## Variable Access Patterns

PLang variables support rich access syntax:

```plang
- set %user% = {name: "John", age: 30, addresses: [{street: "Main St", nr: 1}, {street: "Oak Ave", nr: 2}]}

/ Dot notation
- write out %user.name%              / "John"

/ Nested access
- write out %user.addresses[0].street%  / "Main St"

/ Special accessors
- write out %user.addresses.first.street%   / "Main St"
- write out %user.addresses.last.street%    / "Oak Ave"
- write out %user.addresses.random.street%  / random address

/ Dynamic index
- set %idx% = 1
- write out %user.addresses[idx].street%   / "Oak Ave"
```
