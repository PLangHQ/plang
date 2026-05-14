# Filesystem Permission — Plan v1

## Why this exists

`os/apps/Messages` (forthcoming) needs to read each app's `system.sqlite` to pull messages into one place. Today's filesystem layer has no way to express "this app may read that path." Permission lives in a runtime-only `FileAccessControl` record populated by a yes/no/always prompt thrown from inside `ValidatePath`. There's no signature, no expiry, no audit, no delegation, no cross-app story.

This plan does two things at once because they're entangled:

1. **Replace `FileAccessControl` with a signed `Data<Permission>` model.** Subject is `AppId`, resource is `Path`, the verb is a configurable `Read | Write | Delete` triple with sub-options, the match is `Exact | Glob | Regex`. Storage is system variables on the app, not a new on-disk file layout.

2. **Rewrite `IPLangFileSystem` to be `Path`-shaped, not `string`-shaped.** Stop inheriting `System.IO.Abstractions.IFileSystem`. Every method takes a `Path` object that carries its absolute form, its raw form, and (via Context.Goal) the calling goal that originated the request. The calling goal path is the foreign key the permission system uses for audit and the prompt UI.

Permission alone doesn't work without (2) — `ValidatePath(string)` can't tell you which verb is being requested. (2) alone doesn't deliver the user story — Messages still can't read Email's database.

## The narrative

A PLang developer writes `- read /apps/Email/system.sqlite` in the Messages app. The builder produces an action call. The runtime constructs a `Path` from the string, wiring in the current Context (which carries the calling Goal). The `file/read` handler asks the filesystem layer to read the Path. The filesystem layer asks `Permission/@this.Check(path, verb)` — verb being `Verb.@this { Read = new Read() }` for a plain read. Permission walks the app's stored grants (read from a system variable), looking for one whose `HasAccess(path, verb)` returns true. If found, the read proceeds. If not, Check returns `Data.Fail(new PermissionRequired(path, verb))`. The runtime escalates this to the user — "Messages wants to READ /apps/Email/system.sqlite, [y/n/a/days]." User answers; the response becomes a `Data<Permission>` (signed by PLang's plumbing — not our concern); the grant is added to the app's system variable; the read retries and succeeds.

That's the movie. Everything in the plan serves it.

## Cross-cutting decisions (already settled)

- **Permission record:** `Permission(string AppId, string Path, Verb.@this Verb, Match Match)`. PLang-native field names; no Subject/Resource jargon.
- **Singular OBP folders:** `Permission/`, `Verb/`. The doubled type name (`App.FileSystem.Permission.Permission`) is the accepted cost of singular OBP in C#.
- **Record lives in `this.cs`** alongside `@this`. No separate `Permission.cs`.
- **Verbs are records with default-true booleans:** `Read(Recursive=true, Metadata=true)`, `Write(Create, Overwrite, Append, Mkdir all =true)`, `Delete(Recursive=true, Permanent=true)`. Always present, never nullable. Narrowing is record copy.
- **Match is an enum** for now: `Exact | Glob | Regex`. Default-Exact. Glob is auto-issued by the prompt when the resource is naturally a pattern (e.g. `/apps/*/system.sqlite`). Regex is hand-authored only. If Match ever grows configurable variants, promote it to its own folder.
- **Coverage logic lives with the data.** `Read.Covers(Read)`, `Write.Covers(Write)`, `Delete.Covers(Delete)`, `Verb.@this.Covers(Verb.@this)`, `Permission.HasAccess(Path, Verb.@this)`. `Permission/@this.Check` is four lines because every comparison is delegated.
- **Methods take whole `Path` objects**, not `path.Absolute`. The receiver decides which field it needs.
- **Storage:** signed `Data<Permission>` JSON lives in the app's system variables (not a new file layout). Permission/@this is a typed view over that variable.
- **Drop `System.IO.Abstractions.IFileSystem` inheritance.** The Path-shaped surface is our own; the BCL stays an implementation detail of the Default code.
- **Goal-mapped FS code is parked** for a future pass. Default disk is the only code in v1.
- **`Data<T>` lives at boundaries where "no grant matches" is a real failure outcome** (Permissions.Check, FS method returns). Not inside Verb — variants are always present there.

## Stage index

| Stage | File | Status | Summary |
|-------|------|--------|---------|
| 1 | [stage-1-permission-types.md](../../../../stage-1-permission-types.md) | pending | The pure types: `Permission`, `Verb/@this`, `Read`/`Write`/`Delete` records, `Match` enum. All `Covers`/`HasAccess` methods. No filesystem dependency, no storage. C# tests pin the coverage matrix and the JSON round-trip. |
| 2 | [stage-2-storage-binding.md](../../../../stage-2-storage-binding.md) | pending | `Permission/@this` as a view over the app's system variables. `List()`, `Add(Data<Permission>)`, `Check(Path, Verb.@this)`. Reads the signed Data envelope; doesn't sign (that's PLang plumbing). |
| 3 | [stage-3-filesystem-surface.md](../../../../stage-3-filesystem-surface.md) | pending | The big mapping pass. Drop `IFileSystem` inheritance. Inventory every call site (file actions, runtime internals, Directory ops). Pin the closed list of operations. Define `IPLangFileSystem` v2 with `Path`-typed methods, `Data<T>` returns, explicit `Verb.@this` parameters where the caller's intent matters. |
| 4 | [stage-4-permission-required-error.md](../../../../stage-4-permission-required-error.md) | pending | The `PermissionRequired` error type — its fields (path, requested verb, calling goal from Path.Context), where it lives, how PLang's "ask user, permission:high" picks it up. Round-trip from action call → Check fail → escalation → grant added → retry. |
| 5 | [stage-5-messages-end-to-end.md](../../../../stage-5-messages-end-to-end.md) | pending | Walk Messages app end-to-end: install registers nothing special; first cross-app read prompts; user grants; subsequent reads succeed; grant persists in the system variable. The acceptance test for the whole branch. |

## Deep dives (topic files)

- [plan/permission-design.md](plan/permission-design.md) — The full Permission/Verb/Match design as we agreed it, with code examples. Reference for stage 1.
- [plan/filesystem-surface.md](plan/filesystem-surface.md) — Notes on the surface rewrite: what we drop from `IFileSystem`, what we keep, what we change. The signature-pinning approach.
- [plan/storage.md](plan/storage.md) — Where grants live in the variables system. The variable name, the type, how Add/List flow through.
- [plan/open-questions.md](plan/open-questions.md) — Things still genuinely unsettled. App-side cascade. Lazy vs eager load. Glob library choice. Goal-code semantics (for the parked future).

## Open before implementation

Two things still want a decision before stage 3 starts:

1. **Variable name and exact API for storing grants in the system variables.** I sketched `filesystem.permission` but haven't confirmed against the variables/actor source. Stage 2 will pin this.
2. **App-side cascade for *requested* verbs (separate from granted ones).** Where does an app declare "for this goal, my write is append-only"? At Start.goal level? In goal frontmatter? This is policy on the *consumer* side, separate from the grant. Worth one more conversation before stage 3.

Neither blocks stage 1 — the pure types can land independently.
