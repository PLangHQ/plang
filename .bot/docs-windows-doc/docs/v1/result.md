# docs — windows: result

## Changed
- `Documentation/v0.2/debug.md` (W2), `PLang/app/module/debug/setting/this.cs` (XML comment: "the app's goals it calls").
- `Documentation/v0.2/filesystem-permission.md`: grant patterns (`*`, `**`, `?`, either separator on Windows, case follows the disk for files and never for URLs), the path's `/` form, the store folder's 0700; the Match enum is `exact | glob`.
- `Documentation/v0.2/module-reference-generation.md`: `ValueRequired` for a required variable holding nothing.
- `docs/modules/llm.md`: messages render their variables.
- `doc/events.md`, `keys.md`, `identity-and-signing.md`, `what-leaves-the-machine.md`, `what-plang-does-not-guarantee.md`, `verify.md` (rows 69, 132, 139).
- Stale, fixed: `CLAUDE.md` (seven concepts by their folder names; the two renames cut), `app-tree.md` modules block, `io-channels.md`, `data-internals.md`, `architecture.md`.

## Checked (run)
- `./dev.sh test` MessageRenderProbeTests 2/2, RequiredVariableTests 4/4, GrantMatchTests 15 (Linux), StoreFolderModeProbeTests 1/1, PlangFormTests 7/7.
- `EachGoalIsTheAppsOwn` built test run in a scratch copy: 1 of 1 pass.
- A scratch app with `.data` 755 / `.data/setting` 755 / `data.sqlite` 644, opened with this build: `.data/setting` 700, others unchanged.

## For the architect
- Only the store's folder (`.data/setting`) is 0700, not `.data/`; the identity's folder was not checked.
- `app-tree.md` still has more stale parts (PascalCase `Permission`, "no longer" history lines) beyond the modules block; a separate pass.
- Not run: Windows (no host), a live llm request.

## CHANGELOG
- A user's "each goal/step/action" no longer hears plang's own goals; debug names a system goal by address.
- A required parameter given as a variable holding nothing answers `ValueRequired` (400).
- `llm.query` messages render their variables.
- A grant's `*` stops at either separator; file grants ignore case on Windows, URL grants never.
- The settings store's folder opens as 0700 on Linux/macOS.
