# CLI Changes (v0.2, `cli-app-property-override`)

A command-line flag is a setting's path: `--<path>=<value>` sets `%!<path>%`, and `app.` may be left out
(`--channel.setting.format=plang`, `--build.setting.cache=skip`, `--test.setting.parallel=1`, `--debug.setting.goal=Start`).
Several options at once are a dict at the setting's path (`--debug.setting={"goal":"Start","step":3}`). The walk is
**strict** — a path no setting answers, an unknown key, or a value outside a closed type (`choice`) is rejected, and the
refusal names the path. The mode words `--build`, `--test` and `--debug` alone switch their mode on (so does writing one of
their settings); `--build={...}`, `--test={...}`, `--debug={...}`, `--app={...}` and `--callstack={...}` are refused, naming the
setting path to write instead (`--debug.setting`, `--call.setting`, …).

## Breaking changes

- **`--builder` and `--tester` are gone.** Use `--build` and `--test` (canonical). The
  `plang build` subcommand still works (it's an alias for `--build`).
- **`--debug` no longer carries callstack config.** Callstack capture is its own flag:
  `--call.setting={"timing":true,"diff":true,...}`. There is no shorthand (`callstack:true` is gone) —
  name each knob. It applies to the run's startup actors (System + User).
- **`--debug`'s `variables` is a list of names:** `--debug.setting={"variables":["x","y"]}`. A watched
  variable prints at every step and logs each create, change and delete (the old per-variable
  `{"name","event"}` objects are gone).
- **`--test` config is validated by the types, and unknown keys are rejected.** `format` is a
  closed set (`json`/`junit` — anything else errors); `timeoutSeconds`/`parallel` accept any
  number, with `≤ 0` read as a sentinel (`timeoutSeconds ≤ 0` = no timeout, `parallel ≤ 0` =
  auto/ProcessorCount) rather than a config error. The `"timeout"` alias is gone — use
  `"timeoutSeconds"`. Unknown keys (previously ignored for forward-compat) now error.
- **`--debug`'s `level` is a closed set** (`step`/`action`); any other value is rejected.

## Internal (no user-facing flag change)

- Each actor owns its own `CallStack` (was app-level). A cross-actor call starts a separate
  tree — see `conventions.md` "Actor Owns Its CallStack".
- Run-state app properties (Id/Name/Created/Version/Culture/Cache/CurrentActor/…)
  are `internal set` — not settable via `--app`. Only `create`/`environment` are `--app`-overridable.
