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
| `- if %x% is empty, set %x% = 1` | only the test is built; the `set` is dropped without a warning | two steps, or `- set default value %x% = 1` |
| `- end goal and 3 last goals` | ends only this goal (`levels` 0) | `- end goal and 3 levels up` |
| `retry 3 times over 1 minute` | the wait between retries varies between builds, or is none | `retry 3 times, wait 20 seconds between retries` |
| `- call Chat message=%m%` (a goal named like a module action: Chat, Tell, Send, Read, Write) | does not build | `- call goal Chat message=%m%` |
| `- call /tools/Fetch url="http://..."` | built as an http get of the url; the goal never runs | `- call goal /tools/Fetch url=%url%` |
| `- read file "/x.jsonl", write to %t%` | read as JSON lines, not as text | `- read text file "/x.jsonl", write to %t%` |
| `- connect to %key%, on incoming call Allow, on closed call Again` | may be built as a websocket connect, or with Allow as the step's error handler | `- connect to iroh peer %key%, when a command comes in run Allow, when the connection closes run Again` ([Iroh](./Iroh.md)) |
| `- tell %subscriber% to Ingest` where `%subscriber%` holds a key | built for a connection object; fails at run time | `- tell peer id %subscriber% to Ingest` |
| `- select ... where task like 'r%'` | a `%` in a step starts a variable; the step does not build | `- select task from Todos where task like 'r' \|\| char(37), write to %rows%` |
| a step that reads `%Settings.X%`, built with no one at the console | the build reads the setting, asks for it, and the step fails when there is no answer | set the setting before a headless build |

## Values and methods

- **A method argument is a literal.** Write it in double quotes: `%s.Split(";")%`, `%lines.Split("\n")%` (a new line).
  In single quotes `'\n'` is not a new line. Calls can be chained: `%line.Trim().Split(" ")%`. A variable or another call
  cannot be an argument: `%p.Substring(p.LastIndexOf("/"))%` does not work; work it out in its own step first.
- **Terminal output ends with a new line.** Trim it before it goes into a path, a name or a comparison, and before splitting it
  into lines:
  ```plang
  - terminal ls /srv/app/releases, write to %out%
  - set %list% = %out.Trim()%
  - go through %list.Split("\n")%, call Release name=%item%
  ```
- **A select with one column and `return 1 row` gives the value itself.** `select task as name ..., return 1 row, write to %v%`:
  `%v%` is the task, `%v.name%` is empty. The same goes for `select count(*) as n`.
- **A command line argument cannot contain `=`.** `plang startArgs=a=b` gives `startArgs` the value `a`.

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
