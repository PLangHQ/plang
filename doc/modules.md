# Modules

For someone writing goals. The question this page answers: which modules exist, and where do I read what each does?

A module is a group of actions. A step's words become an action of one module ([how a step becomes an action](how-a-step-becomes-an-action.md)). This is the map.

## Where each module is taught

Each module's teaching is in `os/system/modules/<module>/`:

- `module.description.md` says what the module is for.
- `<action>.description.md`, `<action>.examples.md` and `<action>.notes.md` say what one action does, with step texts and the properties they build to.
- Four modules also have a generated page, `start.md`: [condition](../os/system/modules/condition/start.md), [file](../os/system/modules/file/start.md), [loop](../os/system/modules/loop/start.md) and [screen](../os/system/modules/screen/start.md). For every other module, read the action's `description.md`.
- `os/system/modules/catalog.md` explains how this teaching reaches the builder.

## The modules

These are the folders of `PLang/app/module/`, each with what it is for and the pages here that use it.

| Module | For | Pages here |
|---|---|---|
| `archive` | pack a value, file or folder into an archive and unpack one | |
| `assert` | demand that something holds; fail the step when it does not | [testing](testing-your-goals.md) |
| `browser` | run Chromium; pages as windows | |
| `build` | the builder's own steps: load, merge, validate, save goals | [first goal](first-goal.md), [when a step fails](when-a-step-fails.md) |
| `cache` | the in-memory cache that `cache for …` uses (no steps of its own; the clause is taught in `on`) | [caching](caching.md) |
| `callback` | resume execution from a snapshot held in a value | |
| `channel` | set and remove channels for an actor | |
| `code` | the registry of providers (signing, crypto, …) | |
| `condition` | ask whether something holds; branch with if / elseif / else | [conditions](conditions.md) |
| `crypto` | hashing and hash verification | [identity and signing](identity-and-signing.md) |
| `debug` | write tag metadata onto the current call frame | |
| `error` | throw an error from a step | [errors](errors.md) |
| `file` | read, write, copy, move, delete, list, check existence | [first goal](first-goal.md), [reading a file line by line](reading-a-file-line-by-line.md), [files and permissions](files-and-permissions.md) |
| `goal` | call another goal; return from the current one | [calling goals](calling-goals.md), [goals in other folders](goals-in-other-folders.md) |
| `http` | send requests; upload and download files | [http](http.md) |
| `identity` | create, list, rename, archive and export identities | [identity and signing](identity-and-signing.md) |
| `list` | make, inspect and change lists: add, remove, sort, split, join, group | [reading a file line by line](reading-a-file-line-by-line.md) |
| `llm` | send prompts to a model; ask the decision service | [what leaves the machine](what-leaves-the-machine.md) |
| `loop` | run the rest of a step once per item | [loops](loops.md) |
| `math` | arithmetic | [variables](variables.md) |
| `module` | load and unload external module DLLs | |
| `on` | events, error handling, timeouts and cache clauses | [events](events.md), [errors](errors.md), [timeouts](timeouts.md), [caching](caching.md) |
| `output` | send text or data to a channel | [goals and steps](goals-and-steps.md) |
| `screen` | a window on this machine, with its input events | |
| `setting` | save and remove an actor's settings | [settings](settings.md) |
| `signing` | sign and verify data | [identity and signing](identity-and-signing.md) |
| `snapshot` | resume a captured snapshot | |
| `task` | wait for and cancel background tasks | [calling goals](calling-goals.md) |
| `terminal` | start programs on this machine and talk to them | |
| `test` | discover, run and report tests | [testing](testing-your-goals.md) |
| `timer` | sleep and named stopwatches | [timeouts](timeouts.md) |
| `ui` | render Liquid templates | |
| `variable` | read, write and inspect variables | [variables](variables.md) |
| `window` | a page in a window of its own | |

`os/system/modules/` also holds folders that are not folders of `PLang/app/module/`: `ai`, `db`, `install`, `plang`. Their content is prompt files (`llm`) and templates, not action teaching. There is no database module; see [databases](databases.md).

## Finding the action for a step

1. Name what the step does in one word: read, call, ask, sign, wait.
2. Find the module above.
3. Open the action's `description.md` in `os/system/modules/<module>/`. It is a line or a few lines saying what the action does; for some actions (`timer/sleep`, `crypto/hash`, `error/throw`) that is one line. The step texts and the properties they build to, with the parameter names, are in `<action>.examples.md`.
4. After a build, read the step's actions in `.build/<goal>.pr`.
