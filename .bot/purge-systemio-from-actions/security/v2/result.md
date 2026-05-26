# v2 result — purge-systemio-from-actions

Verdict: **PASS**. All three v1 findings closed; no new ones surfaced.

## F1 (HIGH) — IsInRoot bypass via `..` — CLOSED

**Fix shape (064724fda).** `file.@this`'s ctor canonicalizes
`_absolutePath` through `PathHelper.GetFullPath` before passing to the
base. The dispatch is in `PLang/app/types/path/file/this.cs:25–47`:

```csharp
public @this(string absolutePath, ...) : base(Canonicalize(absolutePath), ...)
{
}

private static string Canonicalize(string absolutePath)
{
    if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
    if (!PathHelper.IsPathRooted(absolutePath)) return absolutePath;
    if (absolutePath.StartsWith("//")) return absolutePath;
    try { return PathHelper.GetFullPath(absolutePath); }
    catch { return absolutePath; }
}
```

**Coverage of each FilePath entry point** — verified by grep across
`PLang/app/`:

| Entry | Path |
| --- | --- |
| `file.Resolve` (scheme registry, bare paths) | `Resolve → ValidatePath(rooted out) → new @this(rooted)` → canonicalize ✓ |
| `App.Load/Save`, builder, settings store | Call `path.Resolve` → same path ✓ |
| `path.JsonConverter.Read` (wire deserialize) | `new file.@this(raw)` — see below |
| Implicit `string → path` operator | `new file.@this(raw)` — see below |
| Derivation verbs (Combine, WithName, InFolder, Parent) | Build a new string and call `new @this(...)` → canonicalize ✓ |

**Are the three skips safe?**

- **`IsNullOrEmpty`** — defensive; can't produce an attack vector.
- **`!IsPathRooted`** — non-rooted strings stay literal. The F1 attack
  requires `_absolutePath` to *textually* prefix-match root; a relative
  string with `..` does not prefix-match root, so AuthGate's `IsInRoot`
  correctly returns false and the prompt fires. Confirmed by reasoning
  and by the regression test `FilePath_Resolve_RelativeWithDotDot_…`
  whose pre-canonicalization shape was rooted-with-`..` (after
  `Path.Combine(runtimeDir, raw)`), which is the exact case the skip
  does *not* apply to.
- **`StartsWith("//")`** — preserves the OS-rooted prefix that
  ValidatePath keeps intact for idempotency. Those paths are out of
  root and gated by `Authorize` regardless.

**Mutation test.** Mutated `Canonicalize` to `return absolutePath`
(skip GetFullPath entirely), rebuilt, ran the F1 suite:

```
failed ReadText_RelativeDotDot_OutOfRoot_DeniedByAuthGate (130ms)
  AssertionException: Expected to be false
failed FilePath_Resolve_RelativeWithDotDot_FromGoalRuntimeDir_LeavesRoot (109ms)
  AssertionException: Expected to not contain ".."
failed FilePath_Ctor_Canonicalizes_RemovesDotDot (107ms)
  AssertionException: Expected to not contain ".."
```

Three of three caught. Mutation reverted (`git status` clean).

## F2 (LOW, latent-Medium) — MarkdownTeaching ungated System.IO — CLOSED

**Fix shape (987a5148e).** Every disk touch in `MarkdownTeaching.cs`
is now a `path.@this` verb:

- `Load(...)` — `folder.ExistsAsync()` + `folder.Combine(...)` +
  `ReadOrNull` (→ `path.ReadText()`).
- `ScanOrphans(...)` — `modulesRoot.ExistsAsync()`, then
  `modulesRoot.List("*", recursive: true)`, filtered to direct
  grandchildren by `RootComparison` equality on `grand.Absolute`. The
  filter restores the original loader's "non-recursive per-module scan"
  semantics. No `EnumerateFiles`, no `Directory.*`.
- `ReadOrNull` / `ReadParagraphs` — operate on `path`, await
  `ReadText`.

**MarkdownTeachingRoot override** — `ResolveMarkdownTeachingRoot()`
returns `path?`, routing the publicly settable `string` through
`path.@this.Resolve(MarkdownTeachingRoot, App.System.Context)`. Any
out-of-root override hits AuthGate at the first verb call (the
`System` actor's channels prompt). The pre-fix attack — set
MarkdownTeachingRoot from config/wire to enumerate `/etc/` — now
surfaces as an AuthGate denial.

**PLNG002 carve-out for MarkdownTeaching is removed.** Confirmed by
reading `Plng002.cs`: only `PathHelper.cs` is exempt for `Path.*`,
only `app/types/path/**` is exempt for `File/Directory/FileInfo/...`.

## F3 (LOW) — PLNG002 whole-file exemption for `app/this.cs` — CLOSED

**Fix shape (bundled with 064724fda).** `OsAbsolutePath` in
`PLang/app/this.cs:84–85` now reads:

```csharp
public string OsAbsolutePath =>
    PathHelper.GetFullPath(PathHelper.Combine(AppContext.BaseDirectory, "os"));
```

`#pragma warning disable` is gone. The whole-file exemption is gone
from `Plng002.cs`'s scanner. App.Load/Save go through `path.Resolve`
already (lines 368, 423). Confirmed by re-reading `Plng002.cs` —
`IsScannedFile` no longer special-cases `app/this.cs`.

## PathHelper contract — verified

`PLang/app/Utils/PathHelper.cs` body is pure name math:

- Combine / Join overloads
- Get*Name / ChangeExtension / IsPathRooted / IsPathFullyQualified
- GetFullPath (with/without basePath)
- Separator constants

No `File.*`, no `Directory.*`, no `FileInfo`, no `FileStream`, no
`GetTempPath`/`GetTempFileName` (the doc explicitly forbids the
last two as IO-touching). Single-line forwarders. The class is
`internal static`, scoped to the PLang assembly.

PLNG002's narrowing pins this — `Path.*` is allowed only from
`PLang/app/Utils/PathHelper.cs` (`IsPathHelperFile`); anything else
fires the diagnostic at error severity. Two-pronged check (the
member-access predicate + the file scope predicate) means a hostile
addition of `File.*` to PathHelper would still fire because
PathHelper.cs only satisfies the `Path.*` carve-out, not the
`File/Directory` one.

## PLNG002 — verified clean

`PLang.Generators/Diagnostics/Plng002.cs:64–82` (the only `IsScannedFile`):

```csharp
public static bool IsScannedFile(string? filePath)
{
    ...
    if (!p.Contains("/PLang/app/") && !p.Contains("/PLang.Generators/")) return false;
    if (p.Contains("/PLang.Generators/")) return false;   // generators are meta
    if (p.Contains("/obj/") || p.EndsWith(".g.cs")) return false;  // generated
    return true;
}
```

No `MarkdownTeaching.cs`, no `app/this.cs`, no
`AllowedSystemIoPathMembers` allowlist. The narrowing happens in
`Analyze`:

```csharp
var isPathMember = containingType.Name == "Path";
if (isPathMember)  { if (IsPathHelperFile(normalized)) return null; }
else               { if (IsPathTypeSurface(normalized)) return null; }
```

Both carve-outs are visible at the use site and not bypassable from
elsewhere. No file-level exemption smuggles past.

## What's still standing

No new findings on the branch. The standing findings outside this
branch's scope (e.g. `Variables.Snapshot` not honouring `[Sensitive]`,
unbounded image reads, etc — see `memory/MEMORY.md`) are unchanged
and not in scope here.

## Summary

The branch's stated security goal — purge System.IO from action
handlers and make `path.@this` verbs the single chokepoint — is
realised. The chokepoint's previously-bypassable prefix check is
sealed by canonicalization at the FilePath ctor (single site, every
code path inherits). PLNG002 is narrowed to two visible carve-outs
with no whole-file exemptions left.

Recommend merging.
