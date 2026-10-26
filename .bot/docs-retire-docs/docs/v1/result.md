# docs v1 — retire the old docs/ pages

doc/ replaces them. Deleted: `docs/getting-started/*` (4), `docs/index.md`, and the module pages `builder`, `cache`, `event`, `mock`, `settings`, `timeout`, `index` (7), 12 files.

Why those seven: each described a module that is renamed or absent (builder → build, cache/timeout/event → on, settings → setting, mock has no module or action in `PLang/app/module/`) and was last written Mar-Apr 2026, or was the index of the pages. `doc/` covers caching, timeouts, events.

Kept: `docs/modules/testing.md`. It is the reference for `plang --test`, last written 2026-10-21, and nothing replaces it yet (`building_plang_tests.md` links to it). Its link to the deleted mock page is removed. The other 16 module pages stay until their module has a generated `start.md`.

Links fixed: `Documentation/v0.2/code-modules.md` (a pointer to builder.md, which only restated the paragraph it sits in); `docs/modules/http.md` (a link to a `file.md` that never existed, now `os/system/modules/file/start.md`); `docs/modules/testing.md` (mock). No other reference to a deleted page in tracked files outside `.bot/`, `learnings/`, `characters/`.
