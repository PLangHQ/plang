# Building plang code

A `.goal` file is natural language. Building turns each step into a `.pr` file that says which
method to call with which parameters. The llm runs at build time only; the app runs on the `.pr`
files.

Each step is answered in two parts:

- **Which module and which method.** A decision engine picks from a finite list, one request per
  goal for the modules and one for the methods.
- **The parameters.** The llm writes them, one request for the whole goal. A value cannot be
  chosen from a list, it has to be written: `set %ble% to the number ten` needs `10`.

A module may build its own step instead, for sql, c# and regex, where what the step needs is
written rather than chosen.

## Commands

```
plang build --llmservice=openai              whole app
plang build --goal=admin/crm/Vendor.goal     one file
plang build --rebuild                        build again even if nothing changed
plang build --buildparallel=6                steps and goals at once, default 6
```

Build order is fixed: `events/`, then `Setup/`, then `Start.goal`, then everything else.

## Writing steps that build first time

**Look the method up first.** Most failed steps are a method that already exists under a name you
did not guess. The [module reference](./modules/reference/README.md) lists every method with its
signature. Reading four lines is faster than one failed build.

**Force the module when you know it.** `[file]`, `[list]`, `[db]`, `[ui]`. The hint matches a
substring of the module name, so `[list]` selects ListDictionaryModule. Use it when the wording is
ambiguous, and always for `[code]`.

**One action per step.** A step is a sentence, not a paragraph. If it needs a paragraph it needs to
be two steps or a goal.

**`[code]` is the last resort** and it cannot touch the file system: let `[code]` compute and a
`[file]` step write. Its compiled `.dll` sits next to the `.pr`; when you reword the step, delete
the stale pair or you have two implementations side by side.

**Database clauses have an order:** `ds:` then `return 1 row` then `write to`. Out of order the
builder invents sql. A scalar needs `return 1 row` explicitly, or you get a list of one row and
every comparison against it is false.

```plang
- select id, name from users where email=%email%
    ds: "data"
    return 1 row
    write to %user%
```

## Building several files at once

`--goal=` builds one file, so an agent writing several does not have to wait for each. Two builds
at once do not fight over `.build` or the llm cache.

Order is the constraint, and both halves fail hard:

- A goal calling a goal that does not exist yet fails with `GoalNotFound(404)`.
- A step using a table fails until the setup goal that creates it has been built **and run**.

So: setup first, built and run; then goals that call nothing, as many at once as you like; then
their callers; then the routes file, because every route is a reference to a goal.

Do not parallelise a goal you are still changing your mind about. Each build is a real llm request.

## Verifying

A failed build exits 1 and prints the file, the line, the step, the cause and the fix. Read that
output. Working out which step failed from a gap in the `.pr` numbering throws it away.

A build that exits 0 proves less. It does not say the step built into the method you meant. Three
checks do:

1. No unbuilt steps: every step in the goal has a numbered `.pr` file that exists on disk.
2. The templates you render still parse.
3. Read the `.pr` of the step you care about and confirm the method name and parameters.

The third is the one that catches a step built into a different method than you meant, which is the
failure that costs the most later.

Never delete `.build`. The builder detects changed goals itself, and deleting it throws away every
mapping decision and can produce different choices than before.

## Building from a running app

A running app builds a file or a folder through `PlangModule`, which is what a dev agent or an in
app editor wants:

```plang
- [plang] get goals in "/admin/crm/Vendor.goal", parser: "goal", visibility: "public_and_private"
    write to %goals%
- [plang] build plang code %goals%, on error call HandleError, write to %buildErrors%
```

`parser: "goal"` reads what is on disk, so it sees a file that did not exist when the app started;
`parser: "pr"` reads what is already built.

A targeted build does not build other files including setup goals, does not run setup, does not
delete orphan `.pr` folders, and does not register routes. A new route takes effect when the goal
that adds it runs again, e.g. `- call goal routes/AdminRoutes`; adding the same path and methods
replaces the earlier route, so a routes goal is safe to run again.

## Hooking into the build

An `EventsBuild.goal` in `events/` can run a goal before or after any goal or step is built:

```plang
EventsBuild
- after step is built, call !CreateTest
```

## See also

- [Writing goals that behave](./WritingGoals.md), how goals run once built
- [Module reference](./modules/reference/README.md)
