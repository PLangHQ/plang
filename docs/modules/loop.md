# Loop Module

Iterate over lists and dictionaries. Each iteration calls a goal with the current item.

## Actions

### foreach

Loop through a collection, calling a goal for each item.

```plang
/ Basic list iteration
- set %items% = ["apple", "banana", "cherry"]
- foreach %items%, call ProcessItem item=%item%

ProcessItem
- write out %item%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Collection | object | yes | List or dictionary to iterate |
| GoalName | goal | yes | Goal to call for each item |
| ItemName | string | no | Variable name for the current item (default: `%item%`) |
| KeyName | string | no | Variable name for the current key/index |

## Syntax

The foreach step always calls a goal — it does not support inline sub-steps:

```plang
/ Correct — call a goal
- foreach %items%, call DoSomething item=%item%

/ Wrong — no inline steps
- foreach %items%
    - write out %item%    ← won't work
```

Use `item=%variableName%` to name the loop variable:

```plang
- foreach %products%, call ShowProduct item=%product%
```

## Loop Variables

Inside the called goal, these variables are available:

| Variable | Description |
|----------|-------------|
| `%item%` (or custom name) | Current item value |
| `%key%` (or custom name) | Current key (dictionary) or index (list) |
| `%position%` | Current 1-based position |
| `%listCount%` | Total items in the collection |

## Examples

### List of Objects

```plang
Start
- add {name: "Product1", price: 111} to list, write to %products%
- add {name: "Product2", price: 222} to list, write to %products%
- foreach %products%, call ShowProduct item=%product%

ShowProduct
- write out '%product.name% - $%product.price%'
```

Output:
```
Product1 - $111
Product2 - $222
```

### Dictionary Iteration

```plang
Start
- add 'name', 'John', write to %person%
- add 'age', '30', write to %person%
- foreach %person%, call PrintEntry

PrintEntry
- write out '%item.Key%: %item.Value%'
```

Output:
```
name: John
age: 30
```

### Counting in a Loop

```plang
Start
- set %items% = ["apple", "banana", "cherry"]
- set %count% = 0
- foreach %items%, call CountItem item=%item%
- write out 'Total: %count%'

CountItem
- set %count% = %count% + 1
```

**Returns:** A loop result with `itemCount` (number of items iterated) and `completed` (whether the loop finished).

## Strings are atomic

Strings are not iterated character-by-character. `foreach` over a string runs the body **once**, with the string itself as `%item%`:

```plang
- set %greeting% = "hello"
- foreach %greeting%, call Show item=%word%

Show
- write out %word%
```

Output:
```
hello
```

(Not `h`, `e`, `l`, `l`, `o`.) Same goes for any single value — a non-collection always runs the body once with the value bound to `%item%`. Use `string.split` first if you actually want characters or words.
