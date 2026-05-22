# codeanalyzer v3 — filesystem-permission

**Scope:** one coder commit since v2 (`3234b5254`), +298 / −12 across 4
files. C# changes are small (3 files, ~15 lines net); the bulk is the
polymorphic-Path handoff plan (`Documentation/v0.2/path-polymorphism-plan.md`).

**Verdict: PASS (with one follow-up).** Coder closed v2 #2 cleanly with the
exact helper shape v2 prescribed, and made a defensible scope call on v2 #1
by handing it to the architect on a new branch instead of patching it
tactically here. The polymorphic-Path plan is the right level of fix —
absorbing the handler copy-paste into a `Path.From(scheme)` factory + virtual
verb surface is structurally cleaner than either of v2's (a)/(b) options.
One follow-up: the new `RootComparison` helper should reach two more sites
that share the same Linux-case bug (`Path.cs:125,127` and
`PLangFileSystem.cs:254`). Branch is mergeable; the follow-up belongs on
the next pass or rides along with the polymorphic-Path branch.

---

## v2 follow-up status

| v2 finding | Status |
|---|---|
| #1 handler-layer authorize copy-paste (`PLang/App/modules/file/*.cs`, 7 sites) | **Deferred to new branch** — polymorphic-Path plan (see below) |
| #2 `PLangFileSystem.ValidatePath:227` Linux case-comparison | **Fixed** — `Path.RootComparison` helper hoisted to a single home |
| #3 copy/move two-prompt UX (`copy.cs:35`, `move.cs:31`) | Untouched — folds into polymorphic-Path branch |
| #4 empty-if-body at `PLangFileSystem.cs:184–188` | Untouched — low priority |
| #5 `Stream/this.cs:108` silent fallthrough | Untouched — low priority |
| #6 `test/run.cs` regression test for goal-channel recursion | Untouched — low priority |
| #7 `Verb.ReadOnly()` / `WriteOnly()` factories | Untouched — low priority |

Only #2 was promised for this pass; #3–#7 were always "lower-priority
follow-ups."

---

## v2 #2 verification — clean

`Path.cs:14–25` (the new home):

```csharp
internal static StringComparison RootComparison =>
    OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
```

- One-line getter; single source of truth; no drift possible.
- `internal` is the right scope — both `Path.Authorize.IsInRoot`
  (sibling) and `PLangFileSystem.ValidatePath` (sibling assembly, same
  project) can reach it without a public API surface change.
- Doc-comment names the bug it prevents and points at both consumers by
  name. Future readers see *why* the helper exists.

Call sites:

- `Path.Authorize.cs:95–96` — `IsUnder(fs.RootDirectory, RootComparison)`
  and `IsUnder(fs.OsDirectory, RootComparison)`. The v2-era inline switch
  is gone.
- `PLangFileSystem.cs:227` —
  `!path.StartsWith(RootDirectory, App.FileSystem.Path.RootComparison)`.
  Fully-qualified — minor noise but harmless.

Clean fix.

---

## v2 #1 deferral — defensible scope call

Codeanalyzer v2 offered two tactical fixes — (a) promote `AuthGate` to
`public`, (b) handlers degenerate to one-liners on `Path.Operations`.
Coder declined both and handed the architect a third option: **`Path`
becomes polymorphic across schemes** (`file://`, `http://`, `s3://`, ...).
Handlers become true one-liners (`return await Path.Value!.ReadText();`)
that don't know which scheme they're talking to.

This is structurally cleaner than (b) — `Path.Operations` was already
the "file://" implementation; making that explicit and pluggable absorbs
the handler boilerplate AND opens a feature surface PLang programs will
want (`read %url%` going through HTTP is a frequent request). The plan
calls out the right tradeoffs: scheme registry via source generator
attribute (same ergonomics as `[Action]`), base `CopyTo`/`MoveTo`
defaults that work cross-scheme, `Save` on HTTP **is** POST not
"unsupported" (server's job to refuse, surface via `Data.Fail(405)`).

**This is a legitimate scope call.** The (a)/(b) options I gave in v2
were tactical; the polymorphic plan is the real fix one floor up. The
right venue is a fresh branch with architect input, not a 7-handler
patch on this branch. The branch closure here is honest:
filesystem-permission's *scope* (consent-gated FS access) is complete;
the handler-layer copy-paste is the seam where the next branch picks up.

The plan itself reads well — `Documentation/v0.2/path-polymorphism-plan.md`
is ~80 lines, cites the codeanalyzer finding it answers, lists tradeoffs,
hands the architect a starting point. Good handoff hygiene.

---

## New finding — `RootComparison` should reach two more sites

The helper exists "so `IsUnder` and `PLangFileSystem.ValidatePath` can't
drift apart again" (per the doc-comment). Two more sites use the **exact
same root-prefix pattern with `OrdinalIgnoreCase`** and would drift on
the same Linux false-match bug:

### 3.1 `PLang/App/FileSystem/Path.cs:125–127` — `Relative` getter

```csharp
if (_absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
    _relative = _absolutePath[root.Length..];
else if (string.Equals(_absolutePath, Fs.RootDirectory, StringComparison.OrdinalIgnoreCase))
    _relative = ".";
```

`root` is `Fs.RootDirectory + sep`. On Linux, `/srv/myApp/file.txt`
versus a `RootDirectory` of `/srv/MyApp` would falsely match → returns
`file.txt` as the "relative" path of a file that isn't actually under
the root. Observable to PLang programs through `%path.Relative%`.

**Fix:** `StringComparison.OrdinalIgnoreCase` → `RootComparison` at both
lines (`StartsWith` and the `Equals` next to it).

### 3.2 `PLang/App/FileSystem/Default/PLangFileSystem.cs:254` — system/ fallback

```csharp
var rootSystemDir = RootDirectory + Path.DirectorySeparatorChar + "system" + Path.DirectorySeparatorChar;
if (path.StartsWith(rootSystemDir, StringComparison.OrdinalIgnoreCase)
    && !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
```

Same `RootDirectory`-prefix pattern. On Linux, a path with mismatched
casing against `RootDirectory` won't trigger the `OsDirectory/system/`
fallback, silently breaking the disk-layout invariant the comment
explains. Low-impact (the fallback is for system-goal lookup, not
permission-gated user content), but the same drift risk the helper was
created to eliminate.

**Fix:** `StringComparison.OrdinalIgnoreCase` → `RootComparison`.

### Note — `PLangFileSystem.cs:200` (`/system/` prefix) is intentional

```csharp
if (path.AdjustPathToOs().StartsWith(sysPrefix, StringComparison.OrdinalIgnoreCase) ...)
```

`sysPrefix` is `"/system/"` (the PLang logical convention), not a
filesystem root. Case-insensitivity is correct here — PLang users
writing `/System/` or `/SYSTEM/` should resolve to the same logical
prefix. Leave this one alone.

---

## Pass 5 — Deletion test (v3 scope)

| Lines | What | Verdict |
|---|---|---|
| `Path.cs:23–25` `RootComparison` getter | unifies two homes; doc-comment is the WHY | **Keep — load-bearing** |
| `Path.Authorize.cs:95–96` use of `RootComparison` | gates auto-grant | **Keep** |
| `PLangFileSystem.cs:227` use of `RootComparison` | gate consistency | **Keep** |
| `Documentation/v0.2/path-polymorphism-plan.md` | architect handoff | **Keep — branch-closure artefact** |
| `Path.cs:125,127` and `PLangFileSystem.cs:254` `OrdinalIgnoreCase` | drift risk for the helper's stated invariant | **Migrate to `RootComparison`** (finding 3) |

---

## Build verification

- `PLang.Tests`: **2846/2846 passed** (14.99s on a clean rebuild after
  pull).
- Branch is fast-forward to `origin/filesystem-permission` after rebase.

---

**Verdict: PASS** — v2 #2 is fixed cleanly; v2 #1 deferral is a
defensible scope call backed by a credible plan. The two newly-spotted
`OrdinalIgnoreCase` sites (`Path.cs:125,127` + `PLangFileSystem.cs:254`)
should pick up `RootComparison` on the next pass or ride along with the
polymorphic-Path branch — they're the same drift risk the helper was
created to eliminate.
