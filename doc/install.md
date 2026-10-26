# Install

This page gets a `plang` command running on your machine and proves it works.

## Build it from source

You need the .NET 10 SDK and a clone of the repository.

```bash
git clone https://github.com/PLangHQ/plang.git
cd plang
dotnet build PlangConsole/PLangConsole.csproj -c Debug
```

The program is `PlangConsole/bin/Debug/net10.0/plang`. Put that folder on your `PATH`, or call the file by its full path.

The latest published release is `v0.1.17.3`, from February 2025. It was not run against these pages, so do not assume it matches them. Build from source.

## Prove it runs

There is no `plang --version`. `--version` is refused with `UnknownSetting`:

```
[UnknownSetting] 'version' names no setting — a setting's path is its owner's and then .setting (channel.setting.format).
```

To prove the install, run a goal in an empty folder. It answers that it is not built, which shows the runtime started and read the folder:

```bash
mkdir try && cd try
plang
```

```
GoalNotBuilt (404): Goal /Start.goal isn't built; run plang build
```

## What the command line takes

- `plang` runs the goal `Start`.
- `plang Name` runs the goal named `Name`. `plang Name.goal` is the same.
- `plang build` builds every goal in the folder.
- A word with a dot and no slash, such as `ReadIt.test`, is refused as a goal name (`ArgumentRefused`), because it looks like a setting path. Write the file name with `.goal`.
- `exec` is not a command. `plang exec` reads `exec` as a goal name and answers `GoalNotBuilt`.

## The key a build needs

Running needs no key. Building does.

The builder asks a language model service to choose the action for each step. That service is reached with a key kept in the environment variable `TYPESAFE_API_KEY` or saved as the setting `%!llm.decider.setting.key%`.

A build with no key asks for it once, then saves it in the app and does not ask again:

```
Building needs a TypeSafe key for the decider. Paste it:
```

With no one to answer (input is redirected and empty), it stops, and nothing is sent to the service:

```
MissingDeciderKey (401): Building needs a TypeSafe key for the decider, and no one gave one: set %!llm.decider.setting.key% or the TYPESAFE_API_KEY environment variable.
```

In a folder that is not an app yet, `plang build` asks to create the app first. `--app.setting.create=true` skips that question. See [your first goal](first-goal.md#2-build).

An `OPENAI_API_KEY` is a different key. It is read by steps that ask a model at run time. It does not unlock the build.

Unverified: how to get a `TYPESAFE_API_KEY`, what a build costs, and what a first build asks about payment. A key was supplied to the run, so none of these was seen.

## Next

[Your first goal](first-goal.md).
