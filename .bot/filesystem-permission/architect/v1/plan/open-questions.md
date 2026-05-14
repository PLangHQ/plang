# Open Questions

Things settled enough to plan around but not pinned. Each blocks a specific stage.

## 1. App-side cascade for requested verbs (blocks stage 3 polish)

A grant says what the user *allowed*. App-side config says what the app *asks for* on each call. They're different layers, and the cascade lives only on the second.

Example: the Messages app might declare globally "all my writes are append-only," then a specific goal overrides "this one needs overwrite." Where does that declaration live?

- App-level: `Start.goal` setup, or a settings variable
- Goal-level: goal frontmatter, or per-step

At runtime, the call resolves: app-declared intent ⊆ user-granted permission. If a goal-level config asks for `delete.recursive` and the grant only has `delete{recursive:false}`, the call denies and the prompt fires asking to widen the grant.

Need to settle before stage 3 because the FS surface methods need to know whether the *caller passes the verb* or whether the *FS layer reads it from app config*. Two-axis question:

| | Caller-passes verb | FS reads from config |
|---|---|---|
| Explicit | `Write(Path, Verb.@this requested)` | Just `Write(Path)`, layer reads `app.config.write` |
| Tradeoff | Honest, flexible, ergonomic if defaults are sane | Hidden control flow, but goals can declare once |

Probably both — explicit overrides config. But the default behavior matters.

## 2. Variable name and API (blocks stage 2)

Sketched `filesystem.permission` in storage.md. Stage 2 confirms by reading `PLang/App/Variables/` and the system-actor wiring. Whatever the existing convention is, follow it.

## 3. Lazy vs eager grant load (minor, blocks stage 2)

Leaning eager (load on Permission/@this construction). If variables already load on actor construction, this costs zero. Confirm in stage 2.

## 4. Glob library choice (minor, blocks stage 1)

`Microsoft.Extensions.FileSystemGlobbing` is the BCL-family option. Alternatives: hand-roll a simple `*`/`?`/`**` matcher (probably 20 lines). Glob semantics for permission don't need every edge case — leaning `FileSystemGlobbing` for free correctness.

## 5. Per-process vs persisted grant distinction (blocks stage 4)

Do we keep "y, this once" as a code path distinct from "always," or collapse them (always persist, immediate revoke for "this once")? Leaning keep distinct because intent matters.

## 6. Content vs metadata in Read sub-options

The current `Read(Recursive, Metadata)` doesn't distinguish "may stat" from "may read content." For a backup-style app that should see filenames but not contents, we'd need `Read(Recursive, Metadata, Content)`. Worth adding now, or YAGNI?

Leaning add — `Content=true` default keeps the simple case unchanged, and "stat-only" is a recognizable real use case (file indexers, dedupe tools).

## 7. Goal-mapped FS code (parked — not v1)

Filesystem operations routed to a goal call instead of disk. Parked for a later pass. Mentioned here only so it doesn't get lost: when this comes back, it slots in via a Code-registry alongside Default. The Permission layer above doesn't change.

## 8. The `mkdir = Write` subtlety

`mkdir` is `Write.Mkdir`. But `Write.Create` also exists. Are they the same? Different?

- `Create` — may create a new file at this path
- `Mkdir` — may create a new directory at this path

Both are "make a new thing exist." Could merge. Could split because directory creation and file creation are semantically distinct (a directory has no content; a file does). Leaning keep split for clarity.

---

None of these block stage 1, which is pure types. Most settle in stage 2-3 by reading existing code. The ones that need a conversation: #1 (cascade), #5 (process-only), #6 (content vs metadata).
