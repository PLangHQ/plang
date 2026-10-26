# Variables

For someone writing a program. The question this page answers: how do I keep a value, and read a part of it?

## Set and read

`set` keeps a value. `%name%` reads it, in a step or inside a quoted text.

```plang
- set %name% = "Ada"
- set %n% = 3
- write out "Hello %name%"
```

Output: `Hello Ada`.

## Lists and dicts

A dict is `{key: value}`. A list is `[a, b]`. They nest.

```plang
- set %person% = {name:"Ada", langs:["en","is"]}
- write out %person.name%
- write out %person.langs[1]%
```

Output: `Ada`, then `is`. A dot reaches a field. `[n]` reaches the item at position `n`, counting from 0.

The index can be a variable, written by its name:

```plang
- set %i% = 0
- write out %person.langs[i]%
```

Output: `en`.

## Arithmetic

```plang
- set %attempts% = %attempts% + 1
```

The builder turns this into two actions, `math.add` then `variable.set`. It may print `Properties rejected — retrying` while it does. That is the builder correcting itself, not a failure.

## Add to a list

```plang
- add "alpha" to list %items%
```

(From `test/module/variable/scoping`, which passes.)

## A variable that was never set

It reads as null. `assert %x% is null` passes for a name nothing set, and `is empty` is true for it. A field a dict does not have is also empty.

## The result of the last step

A step that gives back a value leaves it in `%!data%`. `write to %answer%` keeps it under a name you choose:

```plang
- call Double n=21, write to %answer%
```

## Where a variable lives

- A called goal sees the caller's variables.
- A name the called goal sets for the first time lands with the caller.
- A parameter you pass (`n=21`) is the callee's own. It ends with the call, and the caller's variable of that name is its own again.
- A loop's `item` ends with the loop.

These are tests that pass: `test/module/variable/scoping`, `test/module/goal/call/ParameterEndsWithTheCall.test.goal`, `test/module/loop/foreach/LoopItemEndsWithTheLoop.test.goal`.

## System variables

A name that starts with `!` belongs to PLang: `%!data%` is the last result, `%!error%` is the error being handled (`%!error.message%` is its text).

For the actions that work on variables, see `os/system/modules/variable/`.
