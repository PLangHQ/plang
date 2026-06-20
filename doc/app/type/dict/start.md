# Dict

A dict is a set of named values — like a JSON object or a record with fields.

```plang
Start
- set %person% = {name: "Alice", age: 30}
- write out %person.name%          <-- Alice
- set %person.age% = 31            <-- update one field
```

## Reading and writing

Read a field with `%dict.key%`. Set a field with `set %dict.key% = value`. Keys are case-insensitive.

## Assigning a dict

Same as list — assigning a dict to a variable holds a reference to the original data, not a copy. Changes to a field are visible immediately.
