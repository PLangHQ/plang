# Proposal — `PathHelper` static class as the single PLNG002 carve-out

**From:** security (v1)
**For:** coder / architect
**Status:** proposal, not blocking F1

## Motivation

PLNG002 today has five file-level exemptions in
`PLang.Generators/Diagnostics/Plng002.cs::IsScannedFile`:

1. `PLang/app/types/path/**` — the gated verb surface. Legitimately owns
   `System.IO`. **Keep.**
2. `PLang/app/modules/MarkdownTeaching.cs` — whole-file. Hides real
   ungated IO. Addressed separately by F2 (lift to `path.@this` verbs).
3. `PLang/app/this.cs` — whole-file. Hides one pure-name-math expression
   (`OsAbsolutePath`). Inline `#pragma` brackets the right line but the
   pragma is ignored — real exemption is whole-file. F3.
4. `PLang.Generators/**` — meta, not runtime.
5. `obj/` and `.g.cs` — generated.

(1), (4), (5) are structural and correct. (2) gets fixed by routing
through the verb surface (F2). (3) is the one where a `PathHelper`
static class fits cleanly.

There is also a code-internal name-allowlist on `System.IO.Path` members
(`AllowedSystemIoPathMembers` — currently four separator constants).
Codeanalyzer flagged a doc/code mismatch: `good_to_know.md` reads as if
`Path.Combine` etc are allowed, but the analyzer rejects them. The
allowlist is *also* invisible at use sites — a reader of `app/this.cs`
can't tell which `Path.*` calls would pass without checking the
generator config.

`PathHelper` collapses both problems: a typed forwarder where the
allowlist becomes "type `PathHelper`" — visible at the use site, drift-
proof, no hidden generator string-list.

## What `PathHelper` is

A static class in `PLang/app/Utils/PathHelper.cs` (or similar) that
forwards a curated set of **pure path-string-math** members from
`System.IO.Path`. Strictly no IO.

```csharp
namespace app.Utils;

internal static class PathHelper
{
    // Separator constants
    public static char DirectorySeparatorChar => System.IO.Path.DirectorySeparatorChar;
    public static char AltDirectorySeparatorChar => System.IO.Path.AltDirectorySeparatorChar;
    public static char PathSeparator => System.IO.Path.PathSeparator;
    public static char VolumeSeparatorChar => System.IO.Path.VolumeSeparatorChar;

    // Pure name math — no filesystem access, no canonicalization side effects
    public static string Combine(string a, string b) => System.IO.Path.Combine(a, b);
    public static string Combine(string a, string b, string c) => System.IO.Path.Combine(a, b, c);
    public static string Combine(params string[] parts) => System.IO.Path.Combine(parts);
    public static string Join(string a, string b) => System.IO.Path.Join(a, b);

    public static string? GetDirectoryName(string path) => System.IO.Path.GetDirectoryName(path);
    public static string GetFileName(string path) => System.IO.Path.GetFileName(path);
    public static string GetFileNameWithoutExtension(string path) => System.IO.Path.GetFileNameWithoutExtension(path);
    public static string GetExtension(string path) => System.IO.Path.GetExtension(path);
    public static string ChangeExtension(string path, string? extension) => System.IO.Path.ChangeExtension(path, extension);

    // `GetFullPath` IS pure string normalization (no IO) — it just resolves
    // `..` and `.` segments against the current working directory or an
    // explicit base. Belongs here. Use sites: app construction-time anchor
    // computation (OsAbsolutePath), the F1 canonicalization fix.
    public static string GetFullPath(string path) => System.IO.Path.GetFullPath(path);
    public static string GetFullPath(string path, string basePath) => System.IO.Path.GetFullPath(path, basePath);
}
```

**What does NOT belong in PathHelper, ever:**

- `File.*`, `Directory.*`, `FileInfo`, `DirectoryInfo`, `FileStream`,
  `StreamReader/Writer`. These are IO and belong on `path.@this` verbs
  (gated by `AuthGate`).
- `Path.GetTempFileName`, `Path.GetTempPath` — these read environment /
  touch the disk for temp-file allocation. IO, not name math.

The line is: **does this method touch the filesystem or only manipulate
strings?** If filesystem → `path.@this` verb. If strings only → fair
game for `PathHelper`.

## Analyzer changes

In `Plng002.cs`:

1. Drop `IsScannedFile`'s whole-file exemption for `PLang/app/this.cs`
   (line 87).
2. Drop the `AllowedSystemIoPathMembers` allowlist entirely. Anyone
   needing those goes through `PathHelper`.
3. Add a positive allowlist for `PathHelper` itself — *type-name based*,
   not file-path-based. The check in `Analyze` returns null when
   `containingType` is `app.Utils.PathHelper`.
4. Exempt `PLang/app/Utils/PathHelper.cs` itself from the System.IO ban
   (the forwarder file is the one legitimate `System.IO.Path` consumer
   outside the verb surface).

Result: one type, one file exempt, one place to audit.

## Migration

Three call-site categories to rewrite (grep `System.IO.Path` under
`PLang/app/**` minus the path-types namespace):

1. **`PLang/app/this.cs::OsAbsolutePath`**:

   ```csharp
   // before
   public string OsAbsolutePath =>
       global::System.IO.Path.GetFullPath(global::System.IO.Path.Combine(AppContext.BaseDirectory, "os"));

   // after
   public string OsAbsolutePath =>
       PathHelper.GetFullPath(PathHelper.Combine(AppContext.BaseDirectory, "os"));
   ```

   Drop the inline `#pragma` directive and the surrounding rationale
   comments. Drop the whole-file `IsScannedFile` exemption.

2. **Separator-constant readers** under non-path-types namespaces (the
   four currently allowlisted): rewrite `Path.DirectorySeparatorChar`
   → `PathHelper.DirectorySeparatorChar` etc. Sweep with a grep.

3. **The F1 canonicalization fix** — when `file.@this`'s ctor (or
   `file.Resolve`) starts canonicalizing `_absolutePath`, that call is
   `PathHelper.GetFullPath(absolutePath)`. The path-types namespace is
   still exempt, so it could call `System.IO.Path.GetFullPath` directly
   too — but using `PathHelper` keeps "all pure-name-math goes through
   one type" as a uniform rule.

## What this doesn't solve

- **F1 (path-traversal)** — the canonicalization needs to happen in
  FilePath construction; `PathHelper` is the helper it uses but isn't
  the fix itself. F1 is independent and higher priority.
- **F2 (MarkdownTeaching)** — needs migration to `path.@this` verbs
  (gated IO), not to `PathHelper` (non-IO). PathHelper is the wrong
  tool for that file.
- The path-types namespace exemption (1) is still file-path-based.
  That's fine — the namespace IS the gated surface. The only place
  where file-path exemption is structurally honest.

## Why this is worth doing now vs. later

It's cheap (one small static class + sed pass on a handful of call
sites) and it interlocks with the F1 fix: F1 wants to call
`Path.GetFullPath` from `file.@this`'s ctor, and the PathHelper
proposal answers "via what surface?" cleanly. If coder lands F1 first
and then this later, the F1 fix uses raw `System.IO.Path.GetFullPath`
inside the path-types namespace (legit but inconsistent); if both land
together, the canonicalization site is uniformly `PathHelper`.

Recommend: coder lands F1 first (critical), then folds PathHelper into
the same branch as a clean-up before merge.

## Risks / things to watch

- **Junk-drawer drift.** The reviewer rule is: every new method on
  `PathHelper` must be pure string math, no IO. If a PR adds
  `PathHelper.ReadAllText` it's wrong at the type, not at the analyzer
  config — that's the whole point of this proposal. Add a class-level
  XML doc that says "no IO" so it's a documented contract.
- **Tempting to add `Path.IsPathRooted`, `Path.IsPathFullyQualified`.**
  These are pure-predicate name math; they're fine on `PathHelper`. The
  test is "does it touch the filesystem?" — predicates that only read
  the string don't.
- **`PathHelper.GetTempPath` / `GetTempFileName`** — would-be additions
  to watch for. Both touch the environment / filesystem. **Reject.**
  Temp-file allocation is an action and belongs on a gated surface.
