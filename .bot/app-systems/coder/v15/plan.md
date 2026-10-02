# v10 — `.data` slice 1: shape (report, no code yet)

## What moves, and where it is today

| Today | Becomes | Who | Where it is decided |
|---|---|---|---|
| `/.db/system.sqlite` (the app's store) | `/.data/setting/data.sqlite` | coder | `PLang/app/this.cs:344` — the one string; `app.store` is born with it |
| `.db` in error text | `.data/setting/` | coder | `PLang/app/error/SettingsError.cs:44,48` |
| `.build/traces/{id}/…` | `/.data/debug/trace/{id}/…` | builder bot (goals) | `os/system/builder/Build.goal:16`, `BuildGoal/Start.goal:21,89` |
| `.browser` | `/.data/browser/` | os bot | no C# reference |
| sockets | `/.data/run/` (cleared at start; >108 chars → system runtime folder) | os bot | no C# reference |
| goal `save … to file` | `/.data/file/` | builder bot teaches it | — |

No C# reads the trace files — only a doc comment (`actor/context/trace/this.cs:6`). So "C# readers look under
`/.data/debug/trace/`" means that comment changes; there is no code reader to move.

## Coder's diff (small)

```csharp
// app/this.cs:344
global::app.type.item.path.@this.Resolve("/.data/setting/data.sqlite", actor.list.System.Context),
```
- `app/this.cs:178` summary: `.data/setting/data.sqlite`.
- `SettingsError.cs:44,48`: "Delete .data/setting/data.sqlite …", "… permissions on the .data/setting directory."
- `actor/context/trace/this.cs:6`: `.data/debug/trace/{Id}/...`.
- `.gitignore`: add `.data/` (keeps `*.db`; the `/Documentation/.db/system.sqlite` line goes).
- Docs: `Documentation/v0.2/build.md:64`, `building_plang_tests.md:210`, `os/system/builder/builder.code.md:206`
  (store path); trace paths in `debug.md:183`, `trace.md`, `builder.code.md:249-264`, `debugging.code.md:138`,
  `path/this.code.md:22` — the trace docs only once the builder bot moves its goals.
- Tests that name the path: `AppFactsTests.cs:46` (asserts no file), `DataSourceTests.cs:52,373-382`,
  `ActorSettingsStoreTests.cs:10,32` (comments). `Stage5MessagesEndToEndTests` is a foreign-file permission test —
  leave it.

## Questions
1. Does the store file name `data.sqlite` stand? (It holds settings, setup steps, the LLM cache, identity — more than
   setting.) Proposed: as decided, `setting/data.sqlite`.
2. Trace docs: change with my commit, or with the builder bot's goal move? Proposed: with theirs.

No migration of existing `.db` content (decided). An existing `.db/` is left on disk, untouched.
