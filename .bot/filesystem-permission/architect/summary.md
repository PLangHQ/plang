# Filesystem permission — architect summary

PLang filesystem permission system. Six stages, each with its own file. This summary indexes them and captures the cross-cutting design decisions that any reader needs before diving in.

## Stages

| Stage | File | What it owns |
|-------|------|--------------|
| 1  | [Permission types](stage-1-permission-types.md) | Pure types: `Permission` record, `Verb` + Read/Write/Delete sub-records, `Match` enum, `Covers` semantics. No engine, no storage. |
| 2a | [Snapshot-resume](stage-2a-snapshot-resume.md) | Generic engine infrastructure: `IExitsGoal` marker, `Type.Exit()`, action-owns-Snapshot, `Snapshot.Resume(ctx)`, `Goal.RunFrom`, `Channel.Ask`, action owns its execution (drop `App.Run`/`App.RunAction`), `Data.ShouldExit`, drop the old callback classes. |
| 2b | [Permission ask via Path.Authorize](stage-2b-path-authorize.md) | `Path.Authorize(verb)` — calls `output.ask`, signs + stores on approval. Rides entirely on stage 2a. |
| 3  | [Storage binding](stage-3-storage-binding.md) | `Actor.@this.Permission` view: `Find/Add/Revoke`. In-memory + sqlite unified. JSON-filter for per-actor scoping. |
| 4  | [Filesystem surface](stage-4-filesystem-surface.md) | `IPLangFileSystem` v2: Path in, Data out, verb baked in. Permission check inside every FS method via `path.Authorize`. Drop the old surface. |
| 5  | [Messages end-to-end](stage-5-messages-end-to-end.md) | Acceptance test for the whole branch. Real apps tree, real consent flow with test-driver answers, restart round-trip. |

## Cross-cutting design decisions

### Permission shape

- One record: `Permission(AppId, Actor, Path, Verb, Match)`. Used for both grants and requests; asymmetry encoded in Match + verb sub-options.
- Verbs are records with default-true booleans (`Read(Recursive=true, Metadata=true)` etc.). `new Verb.@this()` covers everything; narrowing is record-copy with explicit false.
- Match modes: Exact / Glob / Regex. Closed enum, fail-closed on unknown.

### Suspend/resume — one mechanism (Snapshot)

- `IExitsGoal` is a marker interface. `Ask` implements it (only stage-2a kind).
- An action whose result Type is `Exit()`-true attaches a `Snapshot` to its Data before returning.
- Step loop short-circuits via `Data.ShouldExit()` (wraps the three distinct stop conditions: unhandled failure, `Returned`, Type-Exit).
- Channel decides materialization: stateful blocks in-process and never touches Snapshot; stateless serializes Snapshot to wire.
- Resume is one entry: `Data.Snapshot.Resume(ctx)`. Walks the captured chain recursively so nested goals continue past the suspended sub-goal.

### Action owns its execution

- `action.RunAsync(ctx)` is the single entry. `App.Run` and `App.RunAction` are deleted.
- `Action.Synthetic` (default true) distinguishes C#-helper invocations from PR-built actions. Source generator sets `false` on PR actions.
- Snapshot wire-serialization filters synthetic frames — they can't be restored from PR and are recreated naturally by the resumed execution.

### Storage

- `Actor.@this.Permission` is per-actor: in-memory list + a shared `permission` table in `App.SettingsStore` filtered by actor kind via `json_extract`.
- "y" (session) grants have no expiry; live in-memory; die with the App.
- "a" (always) grants are signed with a far-future expiry; persisted in sqlite.
- Two-column rule: `(key TEXT PRIMARY KEY, data TEXT)`. Key = the Permission's path. Granting the same path twice overwrites.

### Filesystem surface

- `IPLangFileSystem` v2: every method takes a `Path`, returns `Data<T>`, calls `path.Authorize(verb)` internally.
- `Move`/`Copy` bundle their two-path consent into a single question.
- Drops `System.IO.Abstractions.IFileSystem` inheritance, `ValidatePath(string)`, `FileAccessControl`.

## Followups (tracked in `Documentation/Runtime2/todos.md`)

- Add structured options to `output.ask`; refactor `Path.Authorize` to drop `BuildRequest`/`SignAndStore` reconstruction.
- Replace `!ask.answer` sentinel with explicit `Answer` parameter pattern.
- Relocate `App.Snapshot()` orchestration to `Snapshot.@this.Capture(ctx)` static factory.
- Per-channel serializer for stateless suspend (error-resume needs Errors trail; ask-resume doesn't).
- Revisit `Snapshot.ResumeChain` shape — works but clunky; cleaner abstraction may surface during implementation.
