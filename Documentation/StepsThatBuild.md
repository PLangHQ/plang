# Steps that build the first time

A step is natural language that has to land on one method with the right parameters. When it lands
somewhere else you get a build error, or worse, a step that builds into something that does not do
what you meant. These are the habits that raise the hit rate.

## Look the method up before you write the step

Most of my failed steps were a method that already existed under a name I had not guessed. I wrote a
`[code]` step to decode base64 to a file; `WriteBase64ToFile` was already there. I reached for
`ToJson()` on a dictionary; it exists on a list only.

The [module reference](./modules/reference/README.md) lists every module and every method the
builder can map a step onto, with signatures and defaults, generated from the code itself. Reading
the four lines about the module you are about to use is faster than one failed build.

## Force the module when you know it

```plang
- [list] get value of key "body-plain" from dictionary %request.body%
- [output] ask user template "/ui/signIn.html"
- [file] write %content% to /tmp/out.txt
```

The hint is matched as a **substring of the module name**, so `[list]` selects ListDictionaryModule
and `[file]` selects FileModule. A hint that matches several modules narrows the field to those and
leaves the final choice to the llm, which is why `[app]` is weaker than it looks: it matches both
AppModule and WindowAppModule.

Use a hint when the wording is ambiguous, when a step reads like something another module also
does, and always for `[code]`.

## Write one action per step, and keep it short

A step is a sentence, not a paragraph. The `[code]` step that failed four times said:

> remove script, style, head, iframe and object elements with their content from %html%, then remove
> every tag that is not b, strong, i, em, u, br, p, div, span, ul, ol, li, blockquote or a while
> keeping the text inside, remove every attribute except href on a, and remove href unless it starts
> with http, https or mailto

The one that built said:

> strip every html tag and attribute from %html% except the tags b, strong, i, em, u, br, p, ul, ol,
> li and blockquote, and drop script and style elements with their content

Same intent, half the clauses. If a step needs a paragraph, it needs to be two steps or a goal.

## `[code]` is the last resort, and it cannot touch the file system

Use it when no module does the job. Two things to know:

- A `[code]` step that writes a file does not build. The generated method takes an injected
  `fileSystem` parameter that the builder's own parameter check does not know about, and it fails
  with `Parameters in json does not match parameter count in method`. Split it: let `[code]`
  compute and a `[file]` step write.
- The compiled result is a `.dll` next to the `.pr`. When you reword the step, the old dll stays
  behind under its old name. Delete the stale `.dll` and `.pr` pair, otherwise you have two
  implementations of step 01 sitting side by side.

## Clauses on a database step have an order

```plang
- select id, name from users where email=%email%
    ds: "data"
    return 1 row
    write to %user%
```

`ds:` then `return 1 row` then `write to`. Out of order, the builder invents sql. And a scalar
result needs `return 1 row` explicitly:

```plang
- select count(*) as %openCount% from threads where status="open"
    ds: "mail"
    return 1 row
```

Without it you get a list with one row in it and every comparison against it is false.

## Build fast, then verify

```
plang build --llmservice=openai --buildparallel=6
```

Parallel building is much faster and safe for ordinary goals; setup and event goals, and anything
mentioning `inject`, stay sequential on their own. The flag is documented in
`RegisterStartupParameters.cs`.

**A build that exits 0 still proves less than you think.** The exit code itself is now reliable,
measured on the current build: a clean build exits 0, and a step calling a goal that does not exist
or holding invalid sql exits 1. A failed step also leaves no `.pr` at all, so the gap in the
numbering says which step it was.

What a green build does not tell you is whether it built the thing you meant. Never pipe the build
through grep and read only the last line. Three checks actually prove a change works:

1. No unbuilt steps: every step in the goal has a numbered `.pr` file.
2. The templates you render still parse.
3. Read the `.pr` of the step you care about, and confirm the method name and parameters are the
   ones you intended.

That third check is the one that catches a step which built into a different method than you meant,
which is the failure mode that costs the most time later.

## Building one file or a folder from a running app

`plang build` from the command line builds the whole app. A running app can build a single goal
file, or a folder of them, through `PlangModule`, which is what a dev agent or an in app editor
wants:

```plang
- [plang] get goals in "/admin/crm/Vendor.goal", parser: "goal", visibility: "public_and_private"
    write to %goals%
- [plang] build plang code %goals%, on error call HandleError, write to %buildErrors%
- if %buildErrors% is empty then
    - write out "built"
```

Point `get goals` at a `.goal` file for one, or at a folder to build every `.goal` under it. Pass
the whole `%goals%` list to `build plang code`; it compiles each distinct file among them. `parser:
"goal"` parses what is on disk, so it also sees a file that did not exist when the app started.
`parser: "pr"` reads what is already built, so it returns nothing for a new file.

What a targeted build deliberately does not do:

- It does not build the other goal files, including setup goals. A setup file is built only when
  it is the file you named.
- It does not run the orphan sweep that deletes `.pr` folders whose `.goal` file is gone. That
  sweep needs a view of the whole repo, and a running app does not have one.
- It does not run setup, and it does not register routes. A new table still has to be created by
  running its setup goal. A new route takes effect when the goal that adds it runs again, e.g.
  `- call goal routes/AdminRoutes` after that file is built. `add route` works from any goal once
  the webserver is running, and a route added with the same path and methods replaces the earlier
  one, so a routes goal is safe to run again. A route removed from the file stays registered until
  the webserver restarts.

## Building goals in parallel while you write the next one

`plang build --goal=path/To.goal` builds one file, so an agent writing several goal files does not
have to wait for each one. Write a file, start its build in the background, and carry on writing
the next. Two builds running at once were measured on separate goals and both produced correct
`.pr` files: they do not fight over `.build` or the llm cache.

Order is the constraint, and both halves of it fail hard:

- A goal that calls a goal which does not exist yet fails with `GoalNotFound(404)` and the calling
  step gets no `.pr`. Build the callee first.
- A step using a table fails until the setup goal that creates it has been **built and run**. A
  targeted build never builds setup goals it was not asked for.

So: setup first, built and run; then goals with no outgoing `call goal`, as many at once as you
like; then their callers; then the routes file, because every route is a reference to a goal.

**Read the build's output, not the directory listing.** Give each background build its own log and
read the log of any build that exits non zero. The build says exactly what went wrong:

```
❌ BUILD FAILED - 1 step(s) did not build
   /tests/FailBuild.goal:3
       select nosuchcolumn from nosuchtable where id=1 ds: "crm" write to %y%
       → Could not find table(s) nosuchtable in datasource(s) 'crm' ... a table is created by a
         goal in Setup/, and a build that skips the setup goals never creates it.
```

File, line, the step, the cause and the fix. Working out which step failed from a gap in the `.pr`
numbering throws all of that away.

One thing not worth parallelising: a goal you are still changing your mind about. Each build is a
real llm request, so writing the file properly and building it once is cheaper than building three
drafts at the same time.

## Do not delete `.build`

The builder detects changed goals itself. Deleting `.build` throws away every mapping decision and
rebuilds the whole app, which costs money and time and can produce different choices than before.
The one exception is the stale `.dll` from a reworded `[code]` step.

## See also

- [Writing goals that behave](./WritingGoals.md)
- [Talking to an api](./TalkingToAnApi.md)
- [Module reference](./modules/reference/README.md)
