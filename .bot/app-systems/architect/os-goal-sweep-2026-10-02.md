# os/ goal reference sweep — 2026-10-02 (issue 30, pass 1)

A read-only sweep of every `os/**/*.goal` (dot-folders excluded) for who references it: C# string literals and folder conventions, `call`s from other `.goal` files, builder templates/prompts, docs. 40 files; 15 referenced, 25 not (AskSystem already deleted in 20361b06e).

## Referenced (stay)

- **C#:** `system/builder/Build.goal` (`module/build/this.cs:76`), `system/error/Show.goal` (`app/this.cs:500`), `system/test.goal` (`Executor.cs:120`).
- **Convention:** `Start.goal` (the app entry, `CommandLineParser.cs:19`), `system/shortcut/*.goal` (listed by `app/shortcut/list/this.cs:36–39`).
- **Called from the above:** `system/builder/{BuildGoal, BuildGoal/Start, BuildGoal/Decide, BuildGoal/Properties, BuilderChannel, EmitBuildEvent}.goal`.

## Unreferenced

**v0.1 helpers, delete (14):** nothing calls them; the runtime has no `ai`, `db`, `install` or `event` module, and `/system/modules` is read only for teaching `.md` files (`module/this.cs:229`).

- `system/modules/ai/Builder.goal`
- `system/modules/db/Builder/Build.goal` (no live steps)
- `system/modules/event/Modules.goal`
- `system/modules/install/InstallUrl.goal`
- `system/modules/output/AskUserLlm.goal`
- `system/modules/ui/CreateTemplateFile.goal`
- `system/modules/ui/Builder/RenderUserIntent.goal`
- `system/modules/ui/Builder/SetFrameworks.goal`
- `system/modules/ui/Builder/SetLayout.goal`
- `system/modules/ui/Builder/tests/SetFrameworkTest.goal`
- `system/services/VariableService/{DeleteVariable, InsertVariable, UpdateVariable}.goal`
- `system/ui/RenderTemplate.goal`

**The events cluster, held for the os bot (8):** no C# loads an events folder; `Events.goal` (`on app start, call SetupApp`) is itself never loaded, and its calls use `/events/Runtime/…`, which doesn't resolve to `/system/events/` (only `/system/` overlays the os). `SetupApp` isn't the setup convention (that is a goal named Setup or a `setup/` folder).

- `system/events/{Events, SetupApp, BuilderEvents}.goal`
- `system/events/Runtime/{DebugErrorInIde, HandleBadInstructionFile, PrFileNotFound, SendDebug, SendExecutionPath}.goal`

**Root entries, held for the os bot (2):** `StartWindow.goal` (a `start window` step; no window module), `Test.goal` (hello-world). A first goal can be run by name from the CLI, so the os bot is asked whether its launcher starts either.

## The os bot's answer (2026-10-02)

None of the held 10 is started from outside: it searched the plangos host scripts (`start.ps1`, `plang.cmd`, `run-goal.cmd`), the deployed `/shared/plangos` and its harness. PlangOS starts only `system/plangos/Screen`. So the events cluster (8) and the root entries (2) are deleted too: 24 files in all.

## Caveat

"Unreferenced" means nothing in the repo names it, not unrunnable: any file's first goal can be run by name.
