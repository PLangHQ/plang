# codeanalyzer v2 — purge-systemio-from-actions

**Branch:** `purge-systemio-from-actions`
**Diff base:** `f4e75a72` (codeanalyzer v1 commit) → `bfb34bca4` (HEAD)
**Scope:** delta only (5 coder commits since v1; ~470 +/-300 LOC under PLang/ and PLang.Generators/)
**Verdict:** **PASS (NEEDS WORK — low only)**

## Method

Five-pass review on the delta only. v1's accepted-clean files (the bulk of the System.IO purge, JsonConverter wiring, AppGoals dual indexing, Execute verb, D13 discipline) are not re-reviewed — auditor v1 already corroborated them. I focused on what coder *added* in the v1 → HEAD window.

## Verification of v1 findings

| v1 finding | Status in v2 | Where |
| --- | --- | --- |
| N1 — `Json.cs` unused `pathConverter` alloc | **closed** | `channels/serializers/serializer/Json.cs:24–48` — alloc moved inside the `??` branch with an explicit docstring naming the finding. |
| N2 — `ContextualReadOptions` per-call allocation | **deferred (correct call)** | Standing; design choice carried forward; no regression. |
| N3 — implicit `string → path` operator footgun | **standing** | Operator still at `path/this.cs:204–205`; documented contract holds; no in-tree producer in the attack path. |
| N4 — `AppGoals.Add` silent name-collision overwrite | **closed (documented design)** | `app/goals/this.cs:47–56` — comment names the finding and the design choice. |
| N5 — `AppGoals.TryLoadPr` user-name alias writes | **closed (documented design)** | `app/goals/this.cs:188–194` — same. |

Auditor v1 already confirmed N1/N4/N5 closures; spot-checked Json.cs:20–48 myself. Clean.

## Pass 1 — OBP

**1a. Rules.** Nothing new in the delta violates any OBP rule I can find.

- `PathHelper` is a pure forwarder type (`internal static`). No collection, no state, no lifecycle — Rule C "stateless options/policies live as `static readonly` or `static`" applies; passes.
- The new `App.Parent` (`app/this.cs:83`) is a single nullable field, not a missing collection type — not an OBP shape smell.
- MarkdownTeaching is still `static class` (helper) but every disk touch now routes through `path.@this` verbs. `ScanOrphans`/`Load` take `path? modulesRoot` parameters — the caller (`modules.@this.ResolveMarkdownTeachingRoot`) owns the gate.

**1b. Shape smells (yes/no per checklist).**

1. Public mutable collection with rules enforced from outside? **No.** `AppModules._modules` and `AppGoals` internals stayed clean.
2. Cross-file `lock (other.X)`? **No.** None added in delta.
3. Same logical thing stored twice across types? **No.** PathHelper is the single forwarder.
4. Allocate-here / mutate-there / clean-up-elsewhere? **No.** Canonicalize is colocated with the FilePath ctor; PathHelper is a leaf type with no lifecycle.

One borderline observation, called out as N1 below (not an OBP violation, but it sits in the same "discipline lives outside the type" neighborhood).

## Pass 2 — Simplification

Nothing screams. The PathHelper extraction is the right shape — net LOC removed at use sites, intent clearer.

## Pass 3 — Readability

PathHelper is well-documented (no-IO contract, named exclusions like `GetTempPath`/`GetTempFileName`). PLNG002's two carve-out predicates (`IsPathHelperFile`, `IsPathTypeSurface`) are named at the use site (`Plng002.cs:155–163`) — a reader doesn't have to guess what the exemption is for.

`Canonicalize` (`file/this.cs:30–47`) has a tight rationale-comment for every branch (the `//` prefix preservation, the relative-path skip). Reads well.

## Findings (LOW severity, optional)

### N1 — `app.@this.Parent` is a publicly settable cross-file knob (Pass 4, latent)

```csharp
// app/this.cs:83
public app.@this? Parent { get; set; }

// path/this.Authorize.cs:96-113
protected bool IsInRoot()
{
    var app = Context?.App;
    if (app == null) return false;
    while (app != null)
    {
        if (IsUnder(app.AbsolutePath, RootComparison) || ...)
            return true;
        app = app.Parent;
    }
    return false;
}

// modules/test/run.cs:75
childApp.Parent = parentApp;
```

The `Parent` setter is public — anyone with an `app.@this` reference can mutate the parent chain. There's exactly one wire-up site today (`test/run.cs`), but nothing prevents a future caller from creating a cycle (`a.Parent = b; b.Parent = a`) and infinite-looping every `IsInRoot()` call (and thus every `Authorize`).

Two cheap defenses, either is fine:

- Make `Parent` an init-only or constructor-supplied parameter. Pushes the "child app has a parent" notion into the type's ctor contract instead of a settable.
- Cap the walk at a small N (8–16) and `return false` on overflow.

If neither, at least put a `Debug.Assert(IsAcyclic)` in `Parent`'s setter — cheap insurance during the time before this surface settles.

**Severity:** LOW. One in-tree wire-up, one well-behaved caller. The risk is future drift.

### N2 — `Canonicalize` catches everything (Pass 5, defensive but blunt)

```csharp
// file/this.cs:45-46
try { return PathHelper.GetFullPath(absolutePath); }
catch { return absolutePath; }
```

Bare `catch` swallows `OutOfMemoryException`, `StackOverflowException` (where catchable), `ThreadAbortException`, anything else the runtime throws. `Path.GetFullPath` can throw `ArgumentException`, `PathTooLongException`, `NotSupportedException`, `SecurityException` — that's the actual list to filter.

**Deletion test:** swap to `catch (System.Exception ex) when (ex is System.ArgumentException or System.IO.PathTooLongException or System.NotSupportedException or System.Security.SecurityException)` — any other exception escapes, which is what you want (something genuinely unexpected should fail loud, not be silently coerced to the pre-canonical string and then handed to AuthGate).

**Severity:** LOW. The bare-catch pattern is hostile to debugging future weirdness on this hot path.

### N3 — `IsInRoot` does string concat on every Authorize (Pass 2, latency-not-correctness)

```csharp
// path/this.Authorize.cs:121-129
private bool IsUnder(string? rootCandidate, StringComparison cmp)
{
    if (string.IsNullOrEmpty(rootCandidate)) return false;
    var rootWithSeparator = rootCandidate.EndsWith(PathHelper.DirectorySeparatorChar)
        ? rootCandidate
        : rootCandidate + PathHelper.DirectorySeparatorChar;
    return Absolute.StartsWith(rootWithSeparator, cmp)
        || string.Equals(Absolute, rootCandidate, cmp);
}
```

`IsInRoot` calls this 3× per app in the parent chain, and `IsInRoot` itself fires on **every** `Authorize` (and `Authorize` fires on every disk verb, including the no-op in-root case). The string concat (`rootCandidate + sep`) allocates each call. For long-running PLang apps with hot read paths this adds up. Trivial cache (compute once per app, store on `app.@this` as `AbsolutePathWithSeparator`/`OsDirectoryWithSeparator`/`OsAbsolutePathWithSeparator`) drops the allocation to zero on the in-root fast path.

**Severity:** LOW. Hot-path latency, not correctness.

### N4 — `ValidatePath` does ungated `File.Exists` / `Directory.Exists` probes to decide routing (Pass 4, observation)

```csharp
// file/this.Validate.cs:53,57,75,79
&& !System.IO.File.Exists(resolved) && !System.IO.Directory.Exists(resolved))
...
if (System.IO.File.Exists(osResolved) || System.IO.Directory.Exists(osResolved))
...
&& !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
...
if (System.IO.File.Exists(osFallback) || System.IO.Directory.Exists(osFallback))
```

`ValidatePath` is documented as **"not a security gate"** — that's correct: AuthGate is the gate. But these four `File.Exists`/`Directory.Exists` probes do touch the filesystem to decide whether to route `/system/*` to the os-folder fallback. The probes happen *before* AuthGate gets a chance to ask. For an actor that hasn't been granted read access to either location, four stat-shaped syscalls fire silently on every path resolution. This isn't a security leak (existence-vs-permission is a fine distinction users won't notice for their own root + os/), but it does mean `path.Resolve("/system/x", ctx)` is heavier than it reads.

**Severity:** LOW. Observation only — the carve-out is documented (`PathHelper` exempt for path surface). Worth noting in `good_to_know.md` as a "Validate has side-effect probes" footnote.

### N5 — `MarkdownTeaching.ScanOrphans` walks `Parent.Parent.Absolute` to detect "one level below root" (Pass 3, readability)

```csharp
// MarkdownTeaching.cs:90-96
foreach (var file in listResult.Value)
{
    var moduleDir = file.Parent;
    if (moduleDir == null) continue;
    var grand = moduleDir.Parent;
    if (grand == null) continue;
    if (!string.Equals(grand.Absolute, rootAbs, ...)) continue;
```

The pre-lift code did a per-folder enumeration; the post-lift code lists everything recursively then filters back to "files whose grandparent equals modulesRoot." That works but it's inverted — you scan the world, then filter to the per-module slice. If `path.@this` gains a "direct-children" verb (or `List(recursive: false)` is composed once per module folder), this filter goes away.

Not a bug; just code that reads as "list-everything-then-filter" where the intent is "scan each module's direct children."

**Severity:** LOW. Cosmetic; revisit when a `Children`/`ListFolders` verb lands.

## What I'm not flagging

- **PLNG002 carve-out logic.** `IsPathHelperFile` (exact-suffix match on `/PLang/app/Utils/PathHelper.cs`) + `IsPathTypeSurface` (`/PLang/app/types/path/`). The split — `Path.*` → PathHelper only; `File/Directory/FileInfo/...` → path-types only — is enforced at `Plng002.cs:155–163` per-finding. A hostile `File.Exists` smuggled into PathHelper would still fire (PathHelper is only exempt for `Path.*`, not for `File/Directory`). Tight.
- **`PathHelper` API surface.** Pure name math, explicit exclusion comments on `GetTempPath`/`GetTempFileName`, no IO. Reads exactly as the contract claims.
- **The parent-chain walk in `IsInRoot`.** Deletion test: revert to the v1 shape and the two auditor-flagged tests re-fail. Earns its place. (The N1 concern is the *setter*, not the walk.)
- **`OsAbsolutePath` PathHelper route.** `app/this.cs:96` — the `#pragma warning disable PLNG002` is gone; the route through PathHelper makes the carve-out invisible to the analyzer (correct, since PathHelper's body is the only carve-out). Documented at `app/this.cs:91–94`.
- **MarkdownTeaching's path-verb lift.** Every `Load`/`ScanOrphans` disk touch now flows through `path.ReadText` / `path.List` / `path.ExistsAsync` — `AuthGate` is the chokepoint, no string-path side channels remain in the file. The renderer's logic is unchanged.
- **Async ripple in `builder/code/Default.cs` and `builder/types.cs`.** `Describe()` is now async (it `await`s `MarkdownTeaching.Load` per action). Three call sites were updated atomically: builder/code/Default.Actions, Default.Types (signature flipped from sync to async on IBuilder too), and builder/types.cs's Run. Consistent. The IBuilder interface change is the architecturally honest move; signing off.
- **`Canonicalize`'s skip-on-relative branch.** Correct: anchoring relative strings to CWD here would change identity unrelated to the F1 fix. F1 only requires canonicalization of rooted inputs (which is what `Path.Combine(rootedRuntimeDir, "..")` produces).
- **`Canonicalize`'s skip-on-`//` branch.** Correct: `Path.GetFullPath("//tmp/x")` collapses to `/tmp/x` on Linux, breaking the idempotency that `ValidatePath` deliberately preserves. Out-of-root paths are gated by Authorize regardless, so the F1 attack vector doesn't apply.

## Verdict

**PASS (NEEDS WORK — low only).** The security F1 fix (centralized canonicalization in `file.@this` ctor) is the right shape — every code path inherits it. The PLNG002 carve-out narrowing is auditor-tight. The MarkdownTeaching path-verb lift closes F2 cleanly. The auditor F1 follow-up (`App.Parent` parent-chain walk in `IsInRoot`) is small and works, but the public settable surface is worth tightening (N1).

Five LOW-severity findings, none blocking merge. All are "tighten the screw" — not "the screw is loose."

```
VERDICT: PASS
Issues (low, optional follow-up):
  N1 app.@this.Parent is publicly settable — no cycle guard
  N2 Canonicalize uses bare catch
  N3 IsUnder allocates separator-suffixed root on every Authorize
  N4 ValidatePath does ungated File.Exists/Directory.Exists probes for /system/ routing
  N5 ScanOrphans inverts list-then-filter where per-module direct enumeration was the original shape
Next: run.ps1 tester purge-systemio-from-actions "Review the code on branch purge-systemio-from-actions" -b purge-systemio-from-actions
```
