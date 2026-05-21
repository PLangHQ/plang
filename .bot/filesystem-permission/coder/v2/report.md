# Coder v2 — filesystem-permission

## Version
v2 (post-codeanalyzer follow-up — comment cleanup)

## Context

Codeanalyzer v1 (verdict: NEEDS WORK) cited 10 findings. Most were addressed
in same-day commits before this session opened:

| # | Finding | Commit | Status |
|---|---|---|---|
| 1 | Sync-over-async in Actor.Permission | `af32f3e` | fixed |
| 2 | Unbounded recursion in Path.Authorize / BundledTransfer | `82a136b` | fixed |
| 3 | Bare `catch { return false; }` in VerifySignature | `af32f3e` | fixed |
| 4 | `IsInRoot` case-insensitive on Linux | `c4cbbd3` | fixed |
| 5 | 9× Authorize preamble copy-paste | `8b22a5e` | fixed |
| 6 | `Stat` returns `Dictionary<string, object?>` | `8b22a5e` | fixed |
| 7 | Dead `cause` / `erroredCall` parameters | `1af7922` (code) + `91a7999` (this session: doc-comments) | fixed |
| 8 | Dead `Call.Cause` chain + renderer branches | `1af7922` (code) + `91a7999` (this session: doc-comments) | fixed |
| 9 | Unused `AlwaysExpiry` constant | `f543e19` | fixed |
| 10 | Redundant `await Task.FromResult(...)` | `8b22a5e` | fixed |

## What this session did

The earlier `1af7922` commit removed the actual Cause threading (the
`_ownCause` field, the `Cause` property, the renderer branches, the
parameter plumbing in `error/handle`), but five stale doc-comments
survived — including one **broken `<see cref="Cause"/>`** in
`CallStack/Call/this.cs:13`.

Files touched (1 commit, 5 files, +4/-8 lines):

- `PLang/App/this.cs` — dropped "Cause" from the structural-data list.
- `PLang/App/CallStack/this.cs` — same.
- `PLang/App/CallStack/Flags.cs` — same.
- `PLang/App/CallStack/Call/this.cs` — removed the broken cref + the
  "Cause links are NOT walked" trailing comment on `SnapshotChain`.
- `PLang/App/Errors/Error.cs` — trimmed the call-stack rendering comment.

## Verification

- `dotnet build PlangConsole` clean (0 errors; warnings unchanged).
- C# suite: **2846 / 2846 green** (`dotnet run --project PLang.Tests`).
- PLang test suite unchanged (no source code edits).

## What's not done

Nothing — every codeanalyzer v1 finding is now closed in code, comments,
and tests. No new follow-ups discovered.

## Commit
`91a7999 — coder: fix codeanalyzer #7, #8 — drop stale Cause references from doc-comments`
