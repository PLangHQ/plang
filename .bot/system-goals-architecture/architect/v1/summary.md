# v1 Summary: Everything is Data — Revised Proposal

## What this is
A revised architectural proposal for making `Data<T>` the universal base class for all PLang runtime values. The original proposal had CRTP constraints, `__` prefix conventions, and special DataList/DataDict classes. Through a design conversation with Ingi, these were refined into a cleaner design.

## What was done
Rewrote `Documentation/App/proposals/everything_is_data.md` with these key changes from the original:

- **Dropped CRTP constraint** (`where T : Data<T>`) — too restrictive, prevents `Data<List<T>>` and other non-self-referential usages
- **Dropped `__` prefix idea** — the `!` navigation convention handles separation cleanly
- **Dropped DataList, DataDict special classes** — replaced by `Data<List<T>>`, `Data<Dictionary<...>>`
- **Added `.` vs `!` navigation convention** — `.` uses DeclaredOnly (domain properties only, empty if not found), `!` reaches Data infrastructure
- **Added registered navigators** — per-type, pluggable, on the engine. Replaces the fragile GetChildValue priority chain and static ValueNavigators
- **Scoped inheritance** — Goal/Step/Action stay as program structure. Only value-system participants (Path, Identity, Settings, Engine) become Data<T>
- **`new` keyword for collisions** — User.Name hides Data.Name, safe because navigation uses DeclaredOnly and Variables accesses the base
- **Error objects are self-describing** — no need for container to distinguish error sources

## Files modified
- `Documentation/App/proposals/everything_is_data.md` — complete rewrite

## What's next
- Test-designer should create test suites for the navigation split (`.` vs `!`) and navigator registry
- Implementation follows the 5-phase migration strategy in the proposal
