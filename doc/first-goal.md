# Your first goal

This page writes a goal that reads a JSON file and prints one field, builds it and runs it. You need [a `plang` command and a build key](install.md).

## 1. Make a folder with two files

`person.json`:

```json
{"name":"Ada","age":36}
```

`Start.goal`:

```plang
Start
- read person.json, write to %person%
- write out %person.name%
```

`read person.json, write to %person%` reads the file and keeps what it holds in `%person%`. A JSON file is read into a dict, so `%person.name%` is its `name` field. `write out` prints.

## 2. Build

```bash
plang build
```

The first build in a folder with no `.build/app.pr` asks `No app found at <folder>. Create new app? (y/n)`. Answer `y`. To skip the question, pass `--app.setting.create=true`. When there is no one to answer (input is redirected), it stops with `NoAppFound (400)` instead of asking.

A build prints the folder, how many goals it found, and a line per goal:

```
Building path: <folder>
Found 1 goals
Building goal: Start
```

A build prints more as the goal grows. A goal with sub-goals adds `Building sub-goal: <name>` and `Sub-goal <name> done (…ms)` lines. `Properties rejected — retrying: …` lines are the builder correcting itself. With the key piped in, `Building needs a TypeSafe key for the decider. Paste it:` still prints.

A build that works ends with one line per goal:

```
  Saved Start (3.4s)
```

The builder's output template also defines `[✓] <step>` and `[≡] <step>` lines. They did not appear in any build run for these pages.

Build again without changing anything and nothing is built, so nothing is asked of the model and nothing is charged. The build prints only `Building path:` and `Found N goals`.

A build writes `.build/start.pr` beside `Start.goal`, and a trace under `.build/traces/`. Never edit a `.pr` file. It is what the builder wrote.

## 3. Run

```bash
plang
```

```
Ada
```

A built goal runs with no key. Add a third step, `- write out %person.age%`, build again and run, and the output is:

```
Ada
36
```

## If it does not print

- `GoalNotBuilt (404)`: the goal was not built. Run `plang build`.
- `PrFormatOutdated (400)`: the `.pr` was written by an older builder. Rebuild.
- A build failure: see [when a step fails](when-a-step-fails.md).
