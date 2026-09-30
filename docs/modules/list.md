# List Module

Work with lists (arrays) and collections. Lists are ordered, zero-indexed, and can hold any type of value.

## Actions

### add

Add an item to a list. Creates the list if it doesn't exist.

```plang
/ Build a list
- add 'apple' to %fruits%, write to %fruits%
- add 'banana' to %fruits%, write to %fruits%

/ Add at specific index
- add 'cherry' to %fruits% at index 0, write to %fruits%

/ Add objects
- add {name: "Product1", price: 111} to list, write to %products%
- add {name: "Product2", price: 222} to list, write to %products%
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| ListName | `to %fruits%` | variable | yes | — | Variable name of the list |
| Value | the item, inline (`add 'apple' …`) | object | no | — | Item to add |
| AtIndex | `at index 0` / `at position 0` | int | no | -1 | Insert at this index (-1 = append) |

### remove

Remove an item from a list by value or index.

```plang
/ Remove by value
- remove 'banana' from %fruits%

/ Remove by index
- remove item at index 0 from %fruits%
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| ListName | `from %fruits%` | variable | yes | — | Variable name of the list |
| Value | the value, inline (`remove 'banana' …`) | object | no | — | Value to remove |
| AtIndex | `item at index 0` | int | no | -1 | Index to remove (-1 = use Value) |

### get

Get an item at a specific index.

```plang
- get item at index 2 from %fruits%, write to %third%
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| ListName | `from %fruits%` | variable | yes | Variable name of the list |
| Index | `item at index 2` | int | yes | Zero-based index |

### set

Replace an item at a specific index.

```plang
- set item at index 0 in %fruits% to 'mango'
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| ListName | `in %fruits%` | variable | yes | Variable name of the list |
| Index | `item at index 0` | int | yes | Zero-based index |
| Value | `to 'mango'` | object | no | New value |

### count

Get the number of items in a list or dictionary.

```plang
- count items in %fruits%, write to %total%
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| ListName | `count items in %fruits%` | variable | yes | Variable name of the collection |

**Returns:** Integer count. Returns 0 if not a collection.

### first

Get the first item.

```plang
- get first item from %fruits%, write to %firstFruit%
```

### last

Get the last item.

```plang
- get last item from %fruits%, write to %lastFruit%
```

### contains

Check if a list or dictionary contains a value.

```plang
- check if %fruits% contains 'apple', write to %hasApple%
```

**Returns:** `true` or `false`.

### indexof

Find the index of a value.

```plang
- get index of 'banana' in %fruits%, write to %idx%
```

**Returns:** Zero-based index, or -1 if not found.

### sort

Sort a list in place.

```plang
/ Ascending (default)
- sort %numbers%

/ Descending
- sort %numbers% descending
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| ListName | the list, inline (`sort %numbers%`) | variable | yes | — | Variable name of the list |
| Descending | `descending` | bool | no | false | Sort in descending order |

### reverse

Reverse a list in place.

```plang
- reverse %items%
```

### join

Join list items into a string.

```plang
- join %fruits% with ', ', write to %fruitString%
/ "apple, banana, cherry"

- join %words% with ' ', write to %sentence%
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| ListName | the list, inline (`join %fruits% …`) | variable | yes | — | Variable name of the list |
| Separator | `with ', '` | string | no | "," | Separator between items |

### split

Split a string into a list.

```plang
- split 'apple,banana,cherry' by ',', write to %fruits%
- split 'hello world' by ' ', write to %words%
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| Value | the string, inline (`split 'a,b,c' …`) | string | yes | — | String to split |
| Separator | `by ','` | string | no | "," | Delimiter |
| RemoveEmpty | drop empty entries | bool | no | false | Remove empty entries |

### flatten

Flatten nested lists into a single list.

```plang
- set %nested% = [[1, 2], [3, [4, 5]]]
- flatten %nested%, write to %flat%
/ [1, 2, 3, 4, 5]
```

### unique

Remove duplicate items from a list.

```plang
- get unique items from %items%, write to %distinct%
```

### range

Generate a list of numbers.

```plang
/ 1 to 10
- create range from 1 to 10, write to %numbers%

/ Even numbers: 0, 2, 4, 6, 8, 10
- create range from 0 to 10 step 2, write to %evens%
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| Start | `from 1` | int | yes | — | Start value (inclusive) |
| End | `to 10` | int | yes | — | End value (inclusive) |
| Step | `step 2` | int | no | 1 | Increment between values |

## Examples

### Build and Process a List

```plang
Start
- add {name: "Product1", price: 111} to list, write to %products%
- add {name: "Product2", price: 222} to list, write to %products%
- count items in %products%, write to %total%
- write out 'Total products: %total%'
- foreach %products%, call ShowProduct item=%product%

ShowProduct
- write out '%product.name% - $%product.price%'
```

### Dictionary Operations

```plang
Start
- add 'key1', 'Hello', write to %dict%
- add 'key2', 'World', write to %dict%
- check if %dict% contains 'key1', write to %hasKey%
- count items in %dict%, write to %size%
```
