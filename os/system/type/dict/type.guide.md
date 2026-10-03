# Dictionaries

A `dict` is a set of named values — each key holds a value of any type (text, number, a nested dict or list). You write one as JSON: `{"name":"Ada","age":36}`.

## Reading a value

Read a key with a dot: `%person.name%`, `%person.age%`. Keys nest, so `%order.customer.city%` walks in. A key that isn't a plain word reads with brackets: `%row["first name"]%`.

## Count and entries

- `%person.count%` — how many entries, a number. (A key literally named `count` wins over this.)
- `%person.entries%` — the entries in insertion order, to loop over:

```plang
Start
- foreach %person.entries%, call Show entry=%item%
```

## Building and changing

Build one inline, then read or change a key:

```plang
Start
- set %user% = {"name":"Ada","age":36}
- set %user.age% = 37
- write out "%user.name% is %user.age%"
```
