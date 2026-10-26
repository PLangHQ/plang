# Verification: contributor guide and decisions

Each claim in `doc/contributor/` and `doc/decisions/`, how it was checked, and the result. Branch `docs/doc-contributor`, cut off runtime2 `14ef2a06a`; the CLI and tests built on it. Line numbers are of that commit. `git grep`/`grep` results are counts from the working tree.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| C1 | shape | `app.@this` is the root, at `PLang/app/this.cs:19`, with lowercase members goal 167, actor 262, type 250, module 153, variable 219, error 212, test 205, shortcut 227, filesystem 176, store 190, parent 255 | read those lines | pass |
| C2 | shape | a concept is a folder with an `@this` class in `this.cs`: goal `goal/this.cs:23`, step `goal/step/this.cs:11`, goal list `goal/list/this.cs:12` | `grep -n "class @this"` | pass |
| C3 | shape | a value type has `[PlangType("text")]` at `type/item/text/this.cs:29`; an action is `[Action("add")]` at `module/math/add.cs:5` | read | pass |
| C4 | shape | global aliases `Error` (`GlobalUsings.cs:3`) and `Step` (`:6`) | read | pass |
| C5 | shape | `type.node.@this<T,L>` at `type/node/this.cs:20`; `node.current.@this<T,L>` at `current/this.cs:9`; the root node at `root/this.cs:7`; `app.variable` and `app.shortcut` use the plain node, `app.goal`/`actor`/`test`/`error` the current one | read | pass |
| C6 | shape | PascalCase plumbing members `Code` 159, `Cache` 181, `Debug` 197, `Build` 234; `@event` 126; verb `Name(context)` 76 | read | pass |
| C7 | shape | infrastructure folders in PascalCase under `PLang/app/` are `Attributes` and `Utils` | `ls PLang/app`; `CLAUDE.md` also names `Diagnostics`, `Services`, `Statics`, which are not there | pass for the two; the CLAUDE.md names are **false** here (reported) |
| C8 | shape | handler classes are named for the action (`Add`, `Set`, `Call`), not `AddHandler` | `module/math/add.cs:6`, `variable/set.cs:16`, `goal/call.cs:12` | pass; `CLAUDE.md` "Handler naming" says `SetHandler`: **false** (reported) |
| S1 | how-a-step-runs | goal `Start` at `goal/this.cs:400`: broken goal refused `GoalBroken` (`:407`, `:430-438`), frame (`:415`), `Enter` (`:441`) starts steps (`:444`) | read | pass |
| S2 | how-a-step-runs | step `Start` at `step/this.cs:130`, `Code.Start` at `:150`; action `Start` at `action/this.cs:177`, `Attempt` `:215`, `DispatchAsync` `:303`, `Resolve` `:318`, `real.Start()` `:330` | read | pass |
| S3 | how-a-step-runs | the action's catch excludes `NullReferenceException` (`:334`); the step's catch takes all but OOM, stack overflow, cancellation (`step/this.cs:154`) | read | pass |
| S4 | how-a-step-runs | a non-`Data<T>`/`[Code]` action property fails the build with `PLNG001`, an error | `PLang.Generators/Discovery/this.cs:43-49` | pass |
| S5 | how-a-step-runs | `Start()` return type names the action's type; bare `Data` or `Data<object>` is `item` | `goal/step/action/this.Schema.cs:65-69` | pass |
| S6 | how-a-step-runs | `ICodeGenerated` has `Resolve`, `Attach`, `Start`; generated handlers implement it explicitly | `module/ICodeGenerated.cs` | pass |
| T1 | type-system | `item` is the apex (`type/item/this.cs:29`), `item.Create` at `:41`; a bare Data throws at `:45-48` | read | pass |
| T2 | type-system | `type.@this` carries `{Name, Kind, Strict}` and is an item (`type/this.cs:9-19`, `:27-31`, `:32`) | read | pass |
| T3 | type-system | `Output` is non-virtual at `item/this.cs:848`; `Write(IWriter)` at `type/this.cs:54`; `Write(text.Writer)` at `:63` | read | pass |
| T4 | type-system | views: Store, Out (sensitive left out), Debug (sensitive masked) | `PLang/app/View.cs:7-14` | pass |
| T5 | type-system | `IsTruthy` virtual `item/this.cs:653`; `Data.ToBoolean` asks it `data/this.cs:696`; `path` answers `AsBooleanAsync` | read | pass |
| T6 | type-system | `duration` has `short` and `iso` kinds; `size` has `iec` | `ls type/item/duration type/item/size` | pass |
| R1 | rules | `AuthGate` at `type/item/path/this.Operations.cs:48`; PLNG002 error at `Plng002.cs:48`; `Plng002SystemIoBanTests` 11 pass | read; `./dev.sh test Plng002SystemIoBanTests` | pass |
| R2 | rules | PLNG002 (and PLNG001) fire only under `./dev.sh full`, because analyzers are off in `./dev.sh build` | first written from `dev.sh:31`, `:312-314`; ran a throwaway action with `System.IO.File.ReadAllText` and a `string` property under `./dev.sh build`: `error PLNG002` and `error PLNG001` | **fail** (the cold reader doubted it; the pages now say both fail `./dev.sh build`) |
| R3 | rules | no `Console.Write`/`Console.Error` call in `PLang/app` | `grep -rn` returns one string literal in `type/code/this.Parse.cs:22` | pass |
| R4 | rules | `AppException` at `error/Exceptions.cs:8`; `Error.FromException` at `error/this.cs:274` | read | pass |
| R5 | rules | no `[Default(true)]` in `PLang/app/module/` | `grep -rn` returns nothing | pass |
| R6 | rules | `SharedEntryWritesTests` passes (1 test) | `./dev.sh test SharedEntryWritesTests` | pass |
| R7 | rules | the attributes `[Description]`, `[ModuleDescription]`, `[Example]` do not exist | `ls PLang/app/Attributes`; `grep` for the classes | pass |
| R8 | rules | a missing prose file gives no prose for that slot | `os/system/modules/catalog.md:80` | pass (read) |
| R9 | rules | `app.type.item.variable.parser` exists | `ls PLang/app/type/item/variable/parser` | pass |
| R10 | rules | a changed `.goal` with no rebuild is `Stale` | ran in a scratch app: edited a test's `.goal` without a rebuild, `plang --test` printed `[Stale]` and exited 1 | pass |
| R11 | rules | the Data<T> implicit-operator double-wrap | `Documentation/v0.2/data-internals.md:290-300` | unverified (read, not run) |
| L1 | locked-core | the surface test pins namespaces `app`, `app.module`, `app.module.list` exactly and `app.type`, `app.data`, `app.channel`, `app.call`, `app.event` and below | `SurfaceTests.cs:12-18` | pass |
| L2 | locked-core | it also pins every type's members and every module's actions with properties | `SurfaceTests.cs:39-56` (first written as 44-61; the cold reader corrected it); `surface_golden.txt` 4,569 lines, 142 `action` entries | pass |
| L3 | locked-core | the failure message and the Explicit `AcceptTheSurface` | `SurfaceTests.cs:136`, `:190-191` | pass |
| L4 | locked-core | adding an action fails `TheCoresSurface_IsThePinnedOne` (1 of 3) | ran with the throwaway module `greeter` | pass |
| L5 | locked-core | `./dev.sh test SurfaceTests` is 3 of 3 with the throwaway removed | ran | pass |
| L6 | locked-core | the process to ask: a proposal to the architect, who brings it to the owner | `CLAUDE.md` "The locked core" | pass (read); a person outside the project has no one to ask |
| B1 | build-and-test | the dev.sh subcommands and their comments at the cited lines (5, 6, 7-9, 11, 13, 21, 32, 100-102, 312-314) | read | pass |
| B2 | build-and-test | `./dev.sh test <Class>` prints `total:`/`failed:` | ran several | pass |
| B3 | build-and-test | plang tests run from `test/` with `plang --test` | ran `cd test && plang --test <folder>` for permission, signing, crypto, goal, loop, on and condition folders: all passed | pass |
| B4 | build-and-test | teaching-edit rule: decider fixtures and page goldens re-run | `CLAUDE.md` "Build"; ran `PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`, `ModulePageTests` with the throwaway module: only `PickListTests` failed | pass |
| B5 | build-and-test | the stale-binary trap | `CLAUDE.md` "Stale-binary trap"; not reproduced | unverified (read, not run) |
| A1 | add-an-action | the handler compiles with no error; first build 112 s, second 52 s | `./dev.sh build` | pass |
| A2 | add-an-action | the action is picked from teaching alone: steps built to `greeter.hello` | built `ga` | pass |
| A3 | add-an-action | `Name` with one example misbuilt (`Name="%greeting%"`, output `Hello, !`); `Who` with two examples built right (`Hello, Ada!`) | ran both | pass |
| A4 | add-an-action | the trips: `SurfaceTests` 1 of 3, `PickListTests` 1 of 8; `ModulePageTests` 8, `ConfirmTemplateTests` 2, `LineTwinTests` 1, `PickOptionTests` 18 pass | ran | pass |
| A5 | add-an-action | the experiment left the tree clean | `git status`; `./dev.sh test SurfaceTests` 3 of 3 | pass |
| A6 | add-an-action | `math.add` uses a `[Code]` provider (`module/math/add.cs:19-22`) | read | pass |
| H1 | how-a-bot-works | role lines in `characters/<role>/character.md`; docs is "the last stop before merge" | read | pass |
| H2 | how-a-bot-works | `b4a549d1c` message ends "Line: codeanalyzer, tester, security, auditor PASS, docs satisfied." and carries `Ingi 2026-10-26: "1. yes"` | `git log` | pass |
| H3 | how-a-bot-works | `.bot/decider-key/` layout; `report.json` session keys | `find`; `jq` | pass |
| H4 | how-a-bot-works | 31 branches hold a `claude-md-proposals.md` | `ls .bot/*/claude-md-proposals.md | wc -l` | pass |
| H5 | how-a-bot-works | the pipeline order between roles (test-designer before coder, and so on) | not stated in the repository files read | unverified (the page lists roles, not an order) |
| D1 | decisions | quotes and line numbers of `bans.md:56-58`, `:135`, `conventions.md:82-95`, `obp-smells.md:95`, `catalog.md:25-39`, `:80`, `apps.md`, `error/Exceptions.cs:3-7`, `error/this.cs:269-278`, `SurfaceTests.cs:7-9`, `Plng002.cs:23-50` | read each | pass |
| D2 | decisions | `scripts/verify-decisions.sh` run: 7 PASS, `variable-ref-binds-instance` FAIL (stale path and token) | ran | pass (the FAIL is the finding) |
| D3 | decisions | `core.hooksPath` is unset here, so `.githooks/pre-commit` is not wired | `git config core.hooksPath` is empty | pass |
| D4 | decisions | where the repository records no reason, the record says so | records 3, 4, 6, 8, 9 | by construction |

## Cold reader, run 5 (a fresh agent given doc/, the contributor guide)

The agent was asked to plan a `greeter.hello` action from the guide alone, writing the files into a scratch folder and touching nothing in the repository. It produced the files, the commands, the expected failures and the question of who to ask, and checked about 50 line citations against the source.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| K1 | add-an-action | the files, commands and the two expected test failures can be planned from the guide | agent answers 1-3 matched the real run | pass |
| K2 | the-locked-core | the "what a plang program sees" code is at `SurfaceTests.cs:44-61` | agent found it at `:38-56`; read: `:39-56` | **fail** (line drift; corrected) |
| K3 | rules, build-and-test, decisions | `System.IO` compiles under `./dev.sh build` | see R2 | **fail** (corrected) |
| K4 | add-an-action | the guide says nothing about registration, the key or where to run | agent stuck | page now says: nothing is registered by hand (`catalog.md:38-39`), a build needs the key, run in a scratch folder |
| K5 | how-a-step-runs | where the generated dispatcher is | agent found `PLang.Generators/Emission/Action/this.cs:414` | page now cites it |
| K6 | the-locked-core | who the architect is for a person | agent stuck | page now says |
| K7 | add-an-action | a first-time reader can write `hello.description.md` | agent invented the wording | page now gives the line used |
