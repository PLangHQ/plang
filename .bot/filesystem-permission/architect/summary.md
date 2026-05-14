# Summary

## 2026-05-14 — v1: Filesystem permission system design

Designed a signed-grant-based permission system for cross-app filesystem access, plus a Path-shaped rewrite of `IPLangFileSystem` to drop `System.IO.Abstractions.IFileSystem` inheritance.

The motivating use case is the forthcoming `os/apps/Messages` app, which needs to read each app's `system.sqlite` to consolidate messages. Today's `FileAccessControl` is a runtime-only record populated by a yes/no/always prompt thrown from inside `ValidatePath` — no signature, no expiry, no audit, no delegation.

**Key decisions (all settled in conversation with Ingi):**

- Permission record: `Permission(string AppId, string Path, Verb.@this Verb, Match Match)`. PLang-native field names, not Subject/Resource jargon.
- Singular OBP folders going forward: `Permission/`, `Verb/`. The doubled type name (`App.FileSystem.Permission.Permission`) is the accepted cost.
- Record lives inside `this.cs` alongside `@this`, not in a separate file.
- Verbs are records with default-true booleans (`Read(Recursive=true, Metadata=true)`, etc.). Always present, never nullable. Narrowing is a record copy with explicit `false`s.
- Each variant owns its own `Covers` method. Permission owns `HasAccess(Path, Verb.@this)`. The manager (`Permission/@this.Check`) is four lines because every comparison is delegated to the type that owns the data.
- Methods take whole domain objects (`Path`), not pre-decomposed primitives.
- Storage: signed `Data<Permission>` lives in the app's system variables (likely `filesystem.permission` — confirm in stage 2). No new on-disk file format.
- `IPLangFileSystem` drops `IFileSystem` inheritance, methods become Path-shaped, return `Data<T>`. The BCL stays as an implementation detail of the Default code only.
- Goal-mapped FS code (a "Code" provider that routes ops to a goal call) is **parked** for a later pass. Default disk only in v1.

**OBP refinements codified into `Documentation/v0.2/good_to_know.md`:**

- Variant design pattern: folder per concept, file per variant, always-present records with default-allow, owners-do-their-own-coverage. (New section)
- Singular folder naming rule going forward.
- "Methods take whole domain objects" rule.
- "Verb-named methods are fine when they do real work" — the `GetX`/`IsX` smell is about property-shaped questions, not verbs in general.

**Plan structure:**

- `architect/v1/plan.md` — spine (this file's elder sibling).
- `architect/v1/plan/permission-design.md` — full Permission/Verb/Match design.
- `architect/v1/plan/filesystem-surface.md` — surface rewrite and signature-pinning methodology.
- `architect/v1/plan/storage.md` — system-variable binding.
- `architect/v1/plan/open-questions.md` — eight things still genuinely unsettled.
- `architect/stage-1-permission-types.md` through `stage-5-messages-end-to-end.md` — five lean stages.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [Permission types](stage-1-permission-types.md) | pending |
| 2 | [Storage binding](stage-2-storage-binding.md) | pending |
| 3 | [Filesystem surface](stage-3-filesystem-surface.md) | pending |
| 4 | [PermissionRequired & escalation](stage-4-permission-required-error.md) | pending |
| 5 | [Messages end-to-end](stage-5-messages-end-to-end.md) | pending |

**Open before implementation (from `plan/open-questions.md`):**

The architect-and-Ingi conversation gates these before stage 3:

1. App-side cascade for *requested* verbs (separate from granted ones) — where does an app declare its per-goal write/delete defaults?
2. Whether to add a `Content` sub-option to Read (so "may stat, not read content" is expressible).
3. Whether to keep process-only grants as a distinct code path from persisted ones.

None block stage 1 — pure types can land independently.
