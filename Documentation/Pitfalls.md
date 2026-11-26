# Pitfalls

Things that cost people time when writing plang, each with the wording that works. Every entry was built and run on the
current runtime. When a fix lands, its entry is removed.

## First: check what a step became

A step is mapped to a module and a function when it is built, and the step text does not show which. After every build of a
new or changed step, open `.build/<Goal>/NN. step.pr` and read `ModuleType` and `Function.Name`. A step that "works once" may
be a different module than you meant: an http get instead of a goal call, a math expression instead of a date difference.

## Steps that build to the wrong thing

| You write | What happens | Write instead |
|---|---|---|
| `- if %x% is empty, set %x% = 1` | does not build: a one-line condition can only call a goal, end the goal or throw | the action as an indented step under `- if %x% is empty then`, or `- set default value %x% = 1` |
| `- read file "/x.jsonl", write to %t%` | read as JSON lines, not as text | `- read text file "/x.jsonl", write to %t%` |

## Values and methods

- **A method argument is a literal.** Write it in double quotes: `%s.Split(";")%`, `%lines.Split("\n")%` (a new line).
  In single quotes `'\n'` is not a new line. Calls can be chained: `%line.Trim().Split(" ")%`. A variable or another call
  cannot be an argument: `%p.Substring(p.LastIndexOf("/"))%` does not work; work it out in its own step first.
- **Terminal output has one line per line.** The line break after the last line is not part of the value, so it can go into
  a path, a name or a comparison as it is; `Split("\n")` gives the lines:
  ```plang
  - terminal ls /srv/app/releases, write to %out%
  - go through %out.Split("\n")%, call Release name=%item%
  ```
- **A select with one column and `return 1 row` gives the value itself.** `select task as name ..., return 1 row, write to %v%`:
  `%v%` is the task, `%v.name%` is empty. The same goes for `select count(*) as n`.

## Variables between goals

- **A goal you call and wait for shares the caller's variables.** What it sets, new or existing, is there when it returns.
  A goal started with `dont wait` gets a copy: what it sets is not seen by the caller.
- **`load %x%, set as "y" if empty` replaces a value the caller passed in.** `load` reads `%x%` from the app's data store and sets
  what it finds, or the default, whatever the caller gave. For a
  default on a parameter, write `- set default value %model% = "haiku"`; it keeps a value that was given.
- **`on error call goal X` goes on after the failed step.** X runs, then the next step. To stop, have X end the goal or throw
  ([Error handling](./ErrorHandler.md)).

## See also

- [Writing goals that behave](./WritingGoals.md)
- [Building plang code](./Building.md)
- [Settings](./Settings.md): a missing setting when no one is there.
