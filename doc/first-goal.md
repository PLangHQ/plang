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

Unverified: the lines that follow for a build that succeeds. The source prints `[✓] <step>` for a step built now and `[≡] <step>` for a step kept from an earlier build. A build needs a key (see [install](install.md#the-key-a-build-needs)) and none was available.

A build writes `.build/start.pr` beside `Start.goal`, and a trace under `.build/traces/`. Never edit a `.pr` file. It is what the builder wrote.

## 3. Run

```bash
plang
```

Expected output, unverified for this exact goal:

```
Ada
```

What was checked is the run half, on goals that were already built. A built goal runs with no key and prints its `write out` lines:

```
found
2
3
```

And a built goal reading `config.json` into `%cfg%` then reads `%cfg.alpha%` as the field `alpha`.

## If it does not print

- `GoalNotBuilt (404)`: the goal was not built. Run `plang build`.
- `PrFormatOutdated (400)`: the `.pr` was written by an older builder. Rebuild.
- A build failure: see [when a step fails](when-a-step-fails.md).
