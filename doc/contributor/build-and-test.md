# Build and test

For someone changing the runtime. The question this page answers: which commands build and test it, and what goes wrong if I use the wrong one?

## Build

- `./dev.sh build` builds the test projects and `PlangConsole` incrementally, with the general analyzers off (`dev.sh:5`, `:31`). The plang diagnostics `PLNG001` and `PLNG002` still fail it ([the rules](rules.md)). The program is `PlangConsole/bin/Debug/net10.0/plang`.
- `dotnet build PlangConsole/PLangConsole.csproj -c Debug` builds just the program.
- `./dev.sh full` is the pre-commit gate: all analyzers on, in its own `Gate` configuration, every C# suite, and the plang tests (`dev.sh:11`, `:308-316`). It is slow.

Every `dev.sh` subcommand builds first. Never run `./dev.sh build && ./dev.sh test` (`dev.sh:13`).

## Test the C# code

- `./dev.sh test <ClassName>` builds the project that holds the class, runs that test binary with a filter, and prints `total:` and `failed:` (`dev.sh:6`). `./dev.sh test SurfaceTests` ran in this guide: 3 total, 0 failed.
- `./dev.sh test` with no class runs the suites the changes since the last complete sweep touch (`dev.sh:7-9`). `--force` runs all of them.
- Do not use `dotnet run --project PLang.Tests`: the aggregate project is not runnable and a per-project run takes 90 s or more (`dev.sh:21`).
- The C# tests are in six suites: Modules, Types, Wire, Data, Generator, Runtime (`dev.sh:32`). A suite's whole output is in `/tmp/devsh_<Suite>.log`.
- The pass/fail is read from the summary line, not the exit code: the runner can crash at teardown after printing (`dev.sh:100-102`).

## Test the plang code

The plang tests are `*.test.goal` files in the folder `test/`, at the path of what they test. Run them from `test/`:

```bash
./dev.sh build
cd test && ../PlangConsole/bin/Debug/net10.0/plang --test
```

or a folder or a file: `plang --test module/loop`. Starting in `test/` keeps discovery out of other folders. See [testing your goals](../testing-your-goals.md) for what the run prints.

A test that asks a model reads its answers from a file beside it, `X.test.llm.json`, so `plang --test` needs no key and no network. A changed prompt is a new question: record it once with a key and commit the file.

## The stale-binary trap

`plang --test` uses the program already built at `PlangConsole/bin/Debug/net10.0/plang`. It is not rebuilt per run. `./dev.sh test` rebuilds the project it runs; `plang --test` does not. A phantom failure such as `Action '<module>.<action>' not found`, for an action that exists in the source, means a stale binary. Run `./dev.sh build` before you claim a plang test result.

## After you change teaching markdown

A change under `os/system/modules/**/*.md` or `os/system/type/**/*.md` can move the decider's request, so it re-runs the decider fixtures and the page goldens: `PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`, `ModulePageTests`, `TypePageTests`. A fixture that moved is re-pinned with its Explicit `AcceptTheFixture`; read the diff before you commit it. In this guide, adding one action with its teaching moved `PickListTests.TheStageOneRequest_IsThePinnedOne` and the surface golden, and no page golden.

## Before you push

Work on a branch cut off `runtime2` ([how a bot works](how-a-bot-works.md)). Run the class you changed, then `./dev.sh full` before you hand off. A change to a locked part needs its yes first ([the locked core](the-locked-core.md)).
