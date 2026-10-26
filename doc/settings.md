# Settings

For someone writing goals. The question this page answers: how does a program read a setting, change it, keep it, and set it from the command line?

A setting is a value that configures a part of PLang: `%!llm.setting.cache%` is whether the model's earlier answers are used, `%!build.setting.cache%` is the same for the builder. A setting's path is its owner's name and then `.setting`.

## Read

A setting reads like a variable:

```plang
Start
- write out "cache: %!llm.setting.cache%"
```

Output: `cache: use`.

## Change it for this run

```plang
- set %!llm.setting.cache% = "skip"
- write out "cache now: %!llm.setting.cache%"
```

Output: `cache now: skip`. A `set` lasts for the run. Running the goal again printed `cache: use`: nothing was kept.

## Keep it

`save` writes a setting whole as the actor's row. `remove` takes the row away:

```plang
Keep
- set %!build.setting.cache% = "skip"
- save %!build.setting%
- write out "kept"

Show
- write out "build cache: %!build.setting.cache%"

Drop
- remove %!build.setting%
- write out "dropped"
```

`Show` printed `build cache: use`. After `Keep` it printed `build cache: skip`, in a new run. After `Drop` it printed `use` again: the setting went back to its default.

The row is kept in the app's `.data/setting/data.sqlite`. A saved key is kept the same way, in plain text; see [keys](keys.md). Save only the settings you mean to keep: `save %!llm.setting%` saves the whole llm setting, which includes the model key.

The actions: `os/system/modules/setting/save.description.md` and `remove.description.md`.

## From the command line

A flag is a setting's path: `--<path>=<value>` sets `%!<path>%` for that run.

```bash
plang Start --llm.setting.cache=skip
```

printed `cache: skip` on the first line, where a plain `plang Start` printed `cache: use`. Several options at once are a dict at the setting's path: `--debug.setting={"goal":"Start","step":3}`.

A flag that names one of the mode words' settings (`--build.setting…`, `--test.setting…`, `--debug.setting…`) also switches that mode on. `plang Show --build.setting.cache=use` ran a build, not the goal `Show`.

## What the command line refuses

The line is refused with `ArgumentRefused`, or an `UnknownSetting` or `ChoiceInvalid` error, and nothing runs:

```
[ArgumentRefused] '--debug=Start' gives --debug a value it takes none of: it is on (--debug) or off (--debug=false). To debug one goal, --debug.setting.goal=Start.
[UnknownSetting] 'nosuch.setting.x' names no setting — a setting's path is its owner's and then .setting (channel.setting.format).
[ChoiceInvalid] 'bogus' is not a cache option. Valid: use, skip
[ArgumentRefused] 'Show' is a second goal: the line names one goal, 'Start'. A value is Show=…, a setting --Show=….
[ArgumentRefused] 'llm.setting.cache' is no goal: a setting is written --llm.setting.cache=…, a value llm.setting.cache=….
```

- A value given to a mode word (`--debug=Start`) is refused.
- A path no setting has is `UnknownSetting`.
- A value outside a closed set is `ChoiceInvalid`, naming the valid values.
- A second bare argument is a second goal and is refused.
- A setting's path written without `--` is refused as a goal.

## Not tried

Precedence between a saved row and a flag for the same setting was not tested: the one run that tried it ran a build instead. A setting of another actor than the user, and the system's row, were not tried.
