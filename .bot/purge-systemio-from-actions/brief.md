# Brief — purge System.IO from action handlers

**Branch:** `purge-systemio-from-actions` (off `runtime2`)
**Author:** Ingi (via Claude) — 2026-05-25
**For:** architect bot

## One-line

Action handlers must not call `System.IO` directly. Each such call is a
security hole: it bypasses `FilePath.AuthGate`, which is the only thing
stopping an action from reading/writing arbitrary disk locations regardless
of actor permission.

## The rule (Ingi)

> "A lot of System.IO usage. This is something that is absolutely forbidden,
> and the reason is security. If we don't use our internal file reader, the
> developer can read any file on disk … `System.IO.Directory`, `FileInfo`,
> all of `System.IO`. So we MUST use our internal file/dir/... reader/writer/
> deleter."

In other words: in production C# under `PLang/app/modules/**`, the only
allowed filesystem surface is the `app.types.path.@this` verb set
(`ReadText`, `ReadBytes`, `WriteText`, `WriteBytes`, `Append`, `Mkdir`,
`Delete`, `List`, `Stat`, `MoveTo`, `CopyTo`, `ExistsAsync`,
`AsBooleanAsync`). Every one of those routes through `AuthGate(verb)`
inside `app.types.path.file.@this.Operations` before touching `System.IO`.

A handler reaching for `System.IO.*` is reaching *under* the auth gate.

## What kicked it off — `PLang/app/modules/test/discover.cs`

Concrete instance. The handler:

1. **Walks the disk** with `System.IO.Directory.EnumerateFiles(absRoot, pattern, option)` — no `AuthGate`.
2. **Probes existence** with `System.IO.Directory.Exists` / `System.IO.File.Exists` — no `AuthGate`.
3. **Reads files** with `System.IO.File.ReadAllText(absPrPath)` and `…(absGoalPath)` — no `AuthGate`.
4. **Re-implements containment** with `Path.GetFullPath` + manual `StartsWith(rootPrefix, RootComparison)`.
   This is a homemade auth check because the handler knows it bypassed the real one.
5. Carries `string absRoot`, `string absPrPath`, `string absGoalPath`
   everywhere instead of `FilePath` instances.

The pure-string path arithmetic in the same file (`GetFileName`,
`ChangeExtension`, `Combine`, `GetRelativePath`, `GetDirectoryName`) is not
itself an IO call, but it's the symptom — the handler is treating paths as
`string` because it never lifts them into `FilePath`.

## Proposed shape (subject to architect review)

Two structural changes:

1. **Parameter type.** `Path` becomes `data.@this<path>` (same shape as
   `file.read.cs:18`). Scheme resolution and root containment happen at
   construction; subsequent verb calls each pass through `AuthGate`. The
   handler stops carrying `string absRoot`; the hand-rolled
   `StartsWith(rootPrefix)` check is deleted, because out-of-root reach is
   now rejected by `AuthGate(Read)` — not by a parallel check that can
   drift.

2. **All IO via `FilePath` verbs.**
   - Walk → `rootPath.List(pattern, recursive)` (already gated; already
     returns `Data<List<path>>`, each `Context`-wired).
   - Pr/goal existence + read → for each match `FilePath`, derive the
     `.pr` sibling as a `FilePath` and call `ReadText()`. Discriminate
     on `.Success` and `.Type` for the Stale / corrupt / ok branches.
     No `File.Exists` + `ReadAllText` pair.
   - Goal text read → `match.ReadText()` on the `.test.goal` `FilePath`.

The handler shrinks. Tag extraction (`ExtractUserTags`, `ExtractAutoTags`)
and branch-chain seeding (`SeedBranchChains`) stay as-is — they don't
touch IO.

## Open questions for the architect

1. **Sibling derivation.** The handler needs the `.pr` sibling at
   `<dir>/.build/<name>.pr` of each matched `.test.goal`. Does
   `app.types.path.@this` already expose a way to derive a sibling path
   without dropping back to `string`? Quick scan of
   `path/file/this.Operations.cs` shows internal `new @this(absolute, Context, …)`
   constructions but no public `Sibling` / `Combine` verb. Options:
   - (a) Add `path.@this.Sibling(string relative)` (or `Combine(...)`) as a
     reusable verb. Preferred — same trick will surface in other handlers.
   - (b) Accept that this handler reaches for the parent `FilePath`'s
     `Absolute` and re-wraps as a new `FilePath`. The *read* still gates,
     but the path arithmetic is local.
   Architect call.

2. **`List(pattern, recursive)` semantics for in-root.** Today
   `Operations.cs:128–145` gates `List` with `Verb{Read}`. `IsInRoot()`
   appears to be auto-pass elsewhere (line 380). Need to confirm the
   normal test-config case (`Testing.Path: "."`) stays grant-free, and
   only an out-of-root `--test` path prompts/denies. If that holds, the
   discover.cs replacement is a clean win: silent "return empty list" for
   out-of-root becomes a proper permission prompt or denial.

3. **Scope of this branch.** Just `discover.cs`, or every action handler
   under `PLang/app/modules/**`? A quick sweep is warranted — likely
   several handlers have the same shape. The branch name was chosen broad
   on purpose. If broad, an audit pass (grep for `System.IO\.` under
   `PLang/app/modules/`) belongs in stage 1.

## Reading list

- `PLang/app/modules/test/discover.cs` — the concrete offender. Read in full.
- `PLang/app/modules/file/read.cs` — canonical "handler done right" pattern.
- `PLang/app/types/path/file/this.Operations.cs` — the gated verb surface.
  Note `AuthGate(verb)` as the first line of every method, and `IsInRoot()`
  fast-path at `:380`.
- `Documentation/v0.2/good_to_know.md` — already bans `Console.*` in
  production; this is the filesystem analogue and likely deserves its own
  section under the same heading style.

## Status

- Branch created, pushed to `origin/purge-systemio-from-actions`.
- No code changes yet. Awaiting architect plan before coder picks it up.
