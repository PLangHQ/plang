# Conditions

For someone writing a program. The question this page answers: how do I make a step run only when something is true?

## if

```plang
- set %n% = 3
- if %n% is 3, call Three
- if %n% is 4, call Four
```

Only `Three` runs.

Compare with `is`, `>`, `<`, `==` and the other operators of the `condition` module. See [the condition module](../os/system/modules/condition/start.md) for the full list.

```plang
- if %n% > 2, call Big
```

## and, or

```plang
- if %a% > 1 and %b% < 10, call DoThing
- if %a% == 1 or %b% == 2, call DoThing
```

(From `test/module/condition/compound`, which passes.)

## else

```plang
- if %x% > 100, call WhenBig, else call WhenSmall
```

(`test/module/condition/if/elsebranch`, passes.)

## A body of steps

Indent steps under the `if`. They run only when it holds:

```plang
- if %flag%
    - set %innerRan% = "yes"
```

(`test/module/condition/if/substepsfalse`, passes: a false `%flag%` skips them.)

## Empty and null

`is empty` is true for a variable that was never set, an empty list, an empty dict, and a field the value does not have. It is false for a value that is set. See `test/module/condition/if/MissingIsEmpty.test.goal`.

## Return from an if

```plang
Early
- if %done% is true, return "early"
- return "late"
```

The `return` is inside the `if`. With `done=true` the goal answers `early`, with `done=false` it answers `late`. A run of this goal in a scratch app printed `early`.
