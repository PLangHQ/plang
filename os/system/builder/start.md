# The PLang Builder — start here

This folder *is* the PLang builder: the part of PLang that turns `.goal` files
(natural language) into `.pr` files (JSON the runtime executes). The builder is
itself written in PLang (`*.goal` here), so building it is PLang building PLang.

These docs live next to the code they describe (the `.code.md` convention), so a
change to a goal and a change to its doc land in the same commit. Read them in this
order:

| Doc | Read it when you want to… |
|---|---|
| [`builder.code.md`](builder.code.md) | **Start here.** The whole mental model: the Decide → Properties → match pipeline, the call chain, recovery loops, caching, traces. |
| [`BuildGoal/Decide.code.md`](BuildGoal/Decide.code.md) | Understand the **decide stage** in depth — the two decider exchanges, `step.Pick`, the templates, and how `decider.json` feeds them. |
| [`pr-format.code.md`](pr-format.code.md) | Know the **`.pr` file format** the builder produces (`step`/`code`/`property`/`child`, typed values, condition bodies, sub-goals). |
| [`bootstrap.code.md`](bootstrap.code.md) | **Rebuild the builder itself** — the bootstrap recipe (cwd, file order, path-qualified filters) and the cardinal rule. |
| [`debugging.code.md`](debugging.code.md) | **Diagnose a wrong `.pr`** — the triage runbook: is it the decider, the writer, or the rendered prompt? |
| [`../modules/catalog.md`](../modules/catalog.md) | Edit the **action catalog / teaching** the builder sends the LLM — how `os/system/modules/<module>/*.md` flows into the prompts; the main quality lever. |

Related docs that stay in `Documentation/`:

- [`Documentation/v0.2/build.md`](../../../Documentation/v0.2/build.md) — the `plang build` CLI (`--build={…}`, cache).
- [`Documentation/v0.2/debug.md`](../../../Documentation/v0.2/debug.md) — the `--debug` property bag (LLM tracing, variable watches).

## The one rule that bites everyone

The running builder executes from its **compiled `.pr`** (`**/.build/`), not from the
`.goal` source. Editing a builder `.goal` does **nothing** until you rebuild the builder —
and that rebuild runs on the *old* `.pr`. The `.llm`, `.template`, and `.json` files *are*
read fresh every build, so prompt/template tuning takes effect immediately. Details and
the rebuild recipe: [`bootstrap.code.md`](bootstrap.code.md).

## The files in this folder

```
Build.goal              build entry: channel, build.load, build.goals, foreach → BuildGoal
BuildGoal.goal          shim → BuildGoal/Start
BuildGoal/Start.goal    per goal: Compile (Decide → Properties → build.match) + sub-goals
                        + build.fold + build.goalsSave; recovery goals
BuildGoal/Decide.goal   the two decider exchanges (+ Decide.code.md)
BuildGoal/Properties.goal  the writer llm.query
BuilderChannel.goal     the redirectable "builder" output channel sink
EmitBuildEvent.goal     one-call build-time output
llm/                    Properties.llm, SourceFix.llm, decider.json, and templates/
                        (the rendered prompts)
templates/output/       build-output.template (the progress/error lines)
web/                    the trace viewer (index.html + server.py)
```
