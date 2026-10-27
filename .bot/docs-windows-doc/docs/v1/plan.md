# docs — windows (W2 and the pages a reader meets)

Reader-facing changes on `windows` (architect's list) against the pages that state them. Two branches: `windows` (Documentation/, docs/, one XML comment) and `docs/windows-doc` (windows + runtime2, for `doc/`, which `windows` was cut before).

- W2: `debug.md` says what a system goal named by address shows (its own steps only).
- Grants, case, separators, `/` path form, store folder mode: `filesystem-permission.md`.
- `[IsNotNull]` answers `ValueRequired`: `module-reference-generation.md`.
- llm.query renders its messages: `docs/modules/llm.md`.
- `doc/`: events, keys, identity-and-signing, what-leaves-the-machine, what-plang-does-not-guarantee, verify rows 69, 132, 139.
- Found on the way (stale, code wins): CLAUDE.md "Seven engine concepts" names and the two renames (`environment.run`, `builder.load` do not exist; the module is `build`, the action `build.load`); `app-tree.md` module list; `io-channels.md`, `data-internals.md`, `architecture.md` mentions of `environment.run`/`mock.verify`/`builder/`.
