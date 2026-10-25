# Loops

For someone writing a program. The question this page answers: how do I do something for each item?

## foreach

`foreach` calls a goal once for each item. It does not take sub-steps; the body is a goal.

```plang
Start
- foreach %person.langs%, call Say item=%item%

Say
- write out "lang: %item%"
```

Output for `["en","is"]`: `lang: en`, then `lang: is`.

## What the body sees

- `%item%` is each element inside the body, and ends with the loop.
- A name the body sets that the loop does not bind reaches the caller, so a running total survives:

```plang
Start
- set %total% = 0
- foreach [1, 2, 3], call AddUp
- assert %total% equals 6

AddUp
- set %total% = %total% + %item%
```

(`test/module/loop/foreach/BodyWritesReachTheCaller.test.goal`, passes.)

## A dict

Each entry has a key and an item:

```plang
- foreach %dict%, call ProcessEntry item=%entry%, key=%key%
```

(`test/module/loop/foreach/dictionary`, passes with three entries.)

## An empty list

The body runs zero times and the run goes on. (`test/module/loop/foreach/empty`, passes.)

The loop module's own page: [loop](../os/system/modules/loop/start.md).
