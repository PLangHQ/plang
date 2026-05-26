# codeanalyzer — purge-systemio-from-actions (summary)

## Version

v2 — latest. (v1 = PASS on the original System.IO purge; v2 = PASS on coder's response-to-security-and-auditor delta.)

## What this is

The branch removes every `System.IO.*` reach from PLang's action handlers and engine code, routing all filesystem touches through `app.types.path.@this` verbs gated by `AuthGate`. PLNG002 source-gen analyzer enforces the ban at compile-time (error severity).

Between v1 and v2, coder addressed:
- **security v1 F1** (HIGH): canonicalize FilePath in ctor so `IsInRoot`'s textual prefix-match can't be bypassed with un-resolved `..` segments. Introduced `PathHelper` as the single allowed bridge to `System.IO.Path` (pure name math, no IO).
- **security v1 F2** (MED): lift `MarkdownTeaching` disk reads to `path.@this` verbs — attacker-controlled `MarkdownTeachingRoot` can no longer side-channel reads past `AuthGate`.
- **security v1 F3** (LOW): drop the `app/this.cs` whole-file `#pragma warning disable PLNG002` — `OsAbsolutePath` now routes through `PathHelper`.
- **auditor v1 F1** (MAJOR): add `App.Parent` so per-test child apps inherit parent filesystem scope; `path.@this.IsInRoot()` walks the parent chain. Closes the 206→204 PLang test regression that F1 introduced.

## What was done (v2 review)

5-pass review on the delta only (v1's accepted-clean code wasn't re-reviewed — auditor v1 already corroborated it). Verified:

- v1's N1 (`Json.cs` dead alloc) is **closed** (`channels/serializers/serializer/Json.cs:24–48`).
- v1's N4/N5 (`AppGoals` indexing) are **closed (documented design)** with inline rationale.
- v1's N3 (implicit `string→path` operator footgun) **stands** — documented contract holds.
- PLNG002 carve-outs are exactly two narrow predicates (`IsPathHelperFile` exact-suffix, `IsPathTypeSurface` under `app/types/path/`) at the use site of each finding — no whole-file exemption, no smuggled pragma.
- `PathHelper` is a pure forwarder with explicit no-IO contract and named exclusions (`GetTempPath`/`GetTempFileName` deliberately omitted).
- `Canonicalize` in `file/this.cs` correctly skips relative inputs (would change identity) and `//` prefixes (would break `ValidatePath`'s idempotency).
- The parent-chain walk in `IsInRoot` earns its place (deletion-test would re-fail auditor's 2 tests).

Files modified: `PLang/app/Utils/PathHelper.cs` (new), `PLang/app/types/path/file/this.{cs,Derivation,Operations,Validate}.cs`, `PLang/app/types/path/this.{cs,Authorize.cs}`, `PLang/app/this.cs`, `PLang/app/modules/{MarkdownTeaching,this,test/run,builder/code/{Default,IBuilder},builder/types}.cs`, `PLang/app/Utils/PathExtension.cs`, `PLang.Generators/Diagnostics/Plng002.cs`.

**Verdict: PASS (NEEDS WORK — low only).** 5 LOW-severity follow-ups, none blocking merge. All are "tighten the screw," not "the screw is loose":

- **N1** — `app.@this.Parent` setter is public; no cycle guard. One in-tree wire-up today, but future drift could create an infinite loop in `IsInRoot`. Cap walk depth at 16, or make Parent ctor-supplied.
- **N2** — `Canonicalize` (`file/this.cs:46`) uses bare `catch`. Filter to `ArgumentException | PathTooLongException | NotSupportedException | SecurityException`.
- **N3** — `IsUnder` concats `root + separator` on every Authorize call. Cache the separator-suffixed roots on `app.@this`.
- **N4** — `ValidatePath` does 4 ungated `File.Exists`/`Directory.Exists` probes for `/system/*` fallback routing. Observation only; not a security leak.
- **N5** — `MarkdownTeaching.ScanOrphans` inverts list-then-filter where the original code did per-module direct enumeration. Cosmetic; revisit when `path.@this` gains a folder-children verb.

## Code example

The shape of the F1 fix — centralized canonicalization in the FilePath ctor, so every code path (Resolve, derivation verbs, scheme registry, implicit `string→path` operator) inherits it for free:

```csharp
// PLang/app/types/path/file/this.cs
public @this(string absolutePath, actor.context.@this? context = null, ...)
    : base(Canonicalize(absolutePath), context, content, source) { }

private static string Canonicalize(string absolutePath)
{
    if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
    if (!PathHelper.IsPathRooted(absolutePath)) return absolutePath;   // skip relatives
    if (absolutePath.StartsWith("//")) return absolutePath;            // preserve // idempotency
    try { return PathHelper.GetFullPath(absolutePath); }
    catch { return absolutePath; }                                     // N2: bare catch
}
```

PLNG002's two carve-out predicates — visible at the use site, no whole-file exemption:

```csharp
// PLang.Generators/Diagnostics/Plng002.cs
private static bool IsPathHelperFile(string p)
    => p.EndsWith("/PLang/app/Utils/PathHelper.cs");
private static bool IsPathTypeSurface(string p)
    => p.Contains("/PLang/app/types/path/");

// Per-finding split:
//   System.IO.Path.* → PathHelper.cs only
//   System.IO.File/Directory/FileInfo/... → app/types/path/** only
```

## For v2 after review

This *is* the v2 review. Coder's delta addressed v1's N1/N4/N5 and security/auditor v1 findings. No reviewer-flagged regressions remain. v2 ships 5 net-new LOW findings, listed above, all optional.
