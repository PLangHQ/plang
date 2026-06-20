# List

A list is an ordered collection of values. You can add to it, read from it, and loop over it.

```plang
Start
- set %colors% = ["red", "green", "blue"]
- write out %colors[0]%          <-- red
- foreach %colors%, call PrintIt
```

## Reading items

Items are loaded as you access them, not all at once. A list with a million rows from a database query doesn't slow things down until you actually read each row.

## Writing items

When you set an item in the list, that slot is updated immediately.

```plang
Start
- set %colors[1]% = "yellow"   <-- green becomes yellow
```

## Assigning a list

When you assign an existing list to a variable, PLang holds a reference to the same data — it doesn't copy everything. This makes it fast to pass large lists around.
