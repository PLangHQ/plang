# CLI Changes (v0.2, `cli-app-property-override`)

The CLI flags are now uniform: every flag is a walk onto the matching app-tree node
(`--app` → `app`, `--build` → `app.Build`, `--test` → `app.Test`, `--debug` → `app.Debug`,
`--callstack` → each actor's `CallStack`). JSON keys are **property names**; the walk is
**strict** — an unknown key or a value outside a closed type (`choice`) is rejected.

## Breaking changes

- **`--builder` and `--tester` are gone.** Use `--build` and `--test` (canonical). The
  `plang build` subcommand still works (it's an alias for `--build`).
- **`--debug` no longer carries callstack config.** Callstack capture is its own flag:
  `--callstack={"timing":true,"diff":true,...}`. There is no shorthand (`callstack:true` is gone) —
  name each knob. It applies to the run's startup actors (System + User).
- **`--debug`'s `variables` bare-string shorthand is gone.** Write objects:
  `--debug={"variables":[{"name":"x"}]}`, not `["x"]`.
- **`--test` config is validated by the types, and unknown keys are rejected.** `format` is a
  closed set (`json`/`junit` — anything else errors); `timeoutSeconds`/`parallel` accept any
  number, with `≤ 0` read as a sentinel (`timeoutSeconds ≤ 0` = no timeout, `parallel ≤ 0` =
  auto/ProcessorCount) rather than a config error. The `"timeout"` alias is gone — use
  `"timeoutSeconds"`. Unknown keys (previously ignored for forward-compat) now error.
- **`--debug`'s `level` is a closed set** (`step`/`action`); any other value is rejected.

## Internal (no user-facing flag change)

- Each actor owns its own `CallStack` (was app-level). A cross-actor call starts a separate
  tree — see `conventions.md` "Actor Owns Its CallStack".
- Run-state app properties (Id/Name/Created/Version/OsDirectory/Culture/Cache/CurrentActor/…)
  are `internal set` — not settable via `--app`. Only `create`/`environment` are `--app`-overridable.
