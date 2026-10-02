# Build Mode

PLang transforms `.goal` files (natural language) into `.pr` files (JSON) using an LLM.
Build mode is activated via `plang build` or `--build`.

All build options are passed as JSON via `--build={...}`. The properties map to the
`build` settings class (`build.setting`: `cache`, `files`).

> **How the builder works internally** (the Decide → Properties → match pipeline, the
> `.pr` format, the recovery loops) lives next to the builder's source:
> [`os/system/builder/start.md`](../../os/system/builder/start.md) is the index, and
> [`builder.code.md`](../../os/system/builder/builder.code.md) is the overview. This doc
> is just the `plang build` CLI.
>
> **Rebuilding the builder itself?** That's the bootstrap case — specific cwd and
> file-order requirements:
> [`bootstrap.code.md`](../../os/system/builder/bootstrap.code.md).

## Usage

```bash
# Build all .goal files under the current directory
plang build

# Build a specific file
plang build '--build={"files":"myfile.goal"}'

# Build multiple files (built in the order given)
plang build '--build={"files":["file1.goal","file2.goal"]}'

# Build without the LLM cache (forces fresh LLM calls)
plang build '--build={"cache":false}'

# Combine options
plang build '--build={"files":"myfile.goal","cache":false}'
```

`plang build` scopes to the current working directory, so `cd` into the project (or the
`os/` system tree) you mean to build before running it.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `files` | string or string[] | (all) | File filter. Single string or array; only matching `.goal` files are built, in the order given. Each entry is matched against the `.goal` path — path-qualify it (`system/builder/Build.goal`) to avoid fanning out across same-named files. |
| `cache` | bool | true | Whether to use the LLM response cache. `false` forces fresh LLM calls. |

## How it works

1. `os/system/builder/Build.goal` is the entry point: it loads the app, discovers the
   `.goal` files, and builds each one.
2. For each goal, the builder runs three stages (whole-goal, not per-step):
   **Decide** (the decider names which actions each step may use), **Properties** (one
   `llm.query` writes each step's actions in formal), and **match** (`build.match` reads
   the formal into each step's `code` — no LLM).
3. The result is saved as a `.pr` file — one per `.goal` file, holding the public goal
   plus its private sub-goals.

Full detail: [`builder.code.md`](../../os/system/builder/builder.code.md).

## Cache

There is **one** cache: the LLM layer hashes each request (messages + model + temperature
+ schema + format) and stores the result in `.data/data.sqlite` (`LlmCache`). A hit returns
the stored result without calling the provider — that's the `[≡]` marker in build output,
versus `[✓]` for a fresh call. (`llm.decider` is not cached.)

`cache:false` bypasses the lookup. **Always use `cache:false` when validating a prompt,
template, or catalog change** — a stale hit hides whether your fix worked. Separately, a
step whose source is unchanged since its last build is *cached* in a different sense: its
saved `code` stands and the LLM never sees it (decided before any LLM call).

## Combining with debug

```bash
# Build one file, watch a builder variable
plang build '--build={"files":"myfile.goal","cache":false}' '--debug={"variables":["answer","goal"]}'

# See the exact LLM system/user messages and raw response
plang build '--build={"cache":false}' '--debug={"llm":{"system":true,"user":true,"response":true},"length":{"max":50000}}'
```

The `llm` debug object's flags (`system`, `user`, `response`, `schema`) each emit their own
`=== LLM … ===` block to stderr. Scope to a stage with `{"goal":"Decide"}` or
`{"goal":"Properties"}`. See [debug.md](debug.md) for the full property bag.

## Diagnosing a wrong `.pr`

If a step compiles to the wrong action or wrong parameters, don't guess — the triage
runbook pins the failure to the decider, the writer, or the rendered prompt before you
touch anything: [`debugging.code.md`](../../os/system/builder/debugging.code.md).

The short version: capture what the LLM actually received and returned with
`--debug={"llm":{"system":true,"user":true,"response":true}}` (scoped to `Decide` or
`Properties`). If your newly added action is missing from the rendered catalog entirely,
the problem is catalog discovery, not the model — common causes:

- the teaching file's name doesn't match the action (`<action>.description.md`);
- it's in the wrong folder (must be `os/system/modules/<module>/`);
- the C# handler isn't registered (check it appears in `app.Module` / `Describe()`).

Catalog authoring: [`catalog.md`](../../os/system/modules/catalog.md).

## Builder output routing

Build-time output does not call `Console.WriteLine` and does not write directly to the
`output` channel. `Build.goal` registers its own named channel, `"builder"`, backed by
`BuilderChannel.goal`, and routes every line through it.

```
os/system/builder/
  Build.goal               // - set channel "builder" call BuilderChannel
  BuilderChannel.goal      // condition.if(Left=%message%, "isnotempty") { output.write(Data=%message%) }
  EmitBuildEvent.goal      // render templates/output/build-output.template → write to "builder"
  templates/output/build-output.template   // Liquid case-block per event kind
```

Every progress/error line is a `call EmitBuildEvent kind="...", <fields>`; the `kind`
discriminator selects a branch of the template (`build-path`, `goals-found`, `goalHeader`,
`subGoalHeader`, `subGoalDone`, `goal-decided`, `goal-properties`, `properties-rejected`,
`goalError`, `step-cached`, `step-fresh`). The point of the indirection is the redirection
seam: to send build output to a file logger, a JSON-Lines stream, or a TUI, change the
channel's backing goal (`BuilderChannel.goal`) only — no call-site, template, or other goal
changes.
