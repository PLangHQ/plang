# codeanalyzer — filesystem-permission

## Version
v3

## What this is
Third (and likely final) pass. Coder closed v2 #2 with the helper shape
v2 prescribed, deferred v2 #1 to a new branch via a polymorphic-Path
plan handed to the architect, and declared the branch closed.

This v3 review verifies the v2 #2 fix, evaluates the v2 #1 deferral as
a scope call, and audits the one new commit (`3234b5254`) for any
incidental issues.

## What was done

- Verified `Path.RootComparison` helper (`Path.cs:23–25`) is well-shaped:
  single-line getter, `internal` scope, doc-comment names both consumers
  by name, used at `Path.Authorize.IsInRoot` (95–96) and
  `PLangFileSystem.ValidatePath:227`.
- Read `Documentation/v0.2/path-polymorphism-plan.md` (the v2 #1
  deferral artefact). The plan is structurally cleaner than v2's
  tactical (a)/(b) options — `Path.From(scheme)` factory + virtual verb
  surface absorbs the handler boilerplate AND opens a feature surface
  (`read %url%` → HTTP).
- Grepped for `OrdinalIgnoreCase` + `StartsWith`/`Equals` patterns in
  `PLang/App/FileSystem/`. Found two more sites with the same root-
  prefix-compared-case-insensitively bug the helper was created to
  prevent.
- Rebuilt + reran `PLang.Tests`: 2846/2846 passed.
- Wrote `v3/report.md` + `v3/verdict.json`: **pass**, with one new
  follow-up finding.

## Verdict: PASS

v2 #2 is fixed cleanly. v2 #1 deferral is a defensible scope call —
the (a)/(b) options I gave in v2 were tactical; coder's polymorphic-Path
plan is the real fix one floor up, and the right venue is a fresh branch
with architect input. Branch closure is honest: filesystem-permission's
scope (consent-gated FS access) is complete; the handler-layer
copy-paste is the seam where the next branch picks up.

## One new finding (v3 §3)

The `RootComparison` helper exists "so `IsUnder` and
`PLangFileSystem.ValidatePath` can't drift apart again." Two more sites
share the exact same root-prefix-with-`OrdinalIgnoreCase` pattern and
would drift on the same Linux false-match bug:

- `PLang/App/FileSystem/Path.cs:125,127` — `Relative` getter
  (`StartsWith` and `Equals` against `Fs.RootDirectory`)
- `PLang/App/FileSystem/Default/PLangFileSystem.cs:254` — system/
  fallback (`StartsWith` against `RootDirectory + "/system/"`)

Both should swap to `App.FileSystem.Path.RootComparison`. Low impact —
neither is a security gate (`Relative` is observability, `system/`
fallback is disk-layout) — but the same drift the helper was created
to eliminate.

**`PLangFileSystem.cs:200`** (`sysPrefix = "/system/"`) is a PLang
logical convention, not a filesystem root — case-insensitivity is
intentional there. Leave it.

## Code example — v2 #2 fix shape

`Path.cs:23–25` (the new single home):

```csharp
internal static StringComparison RootComparison =>
    OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
```

Doc-comment explicitly names the bug it prevents and lists both
consumers by name. One-line getter, no drift possible. The kind of
helper that prevents the next regression by construction.

Then `Path.Authorize.cs:95–96`:
```csharp
return IsUnder(fs.RootDirectory, RootComparison)
    || IsUnder(fs.OsDirectory, RootComparison);
```

And `PLangFileSystem.cs:227`:
```csharp
if (!path.StartsWith(RootDirectory, App.FileSystem.Path.RootComparison))
```

Two sites, one source of truth. Codeanalyzer's verbatim prescription.

## Open items for the next branch / pass

```
1. Migrate Path.cs:125,127 (Relative getter) and PLangFileSystem.cs:254
   (system/ fallback) to App.FileSystem.Path.RootComparison.
2. Polymorphic-Path branch (architect): per
   Documentation/v0.2/path-polymorphism-plan.md — absorbs the v2 #1
   handler copy-paste AND v2 #3 copy/move two-prompt UX.
3. v2 lower-priority follow-ups (#4 empty-if-body, #5 Stream
   fallthrough, #6 test/run regression test, #7 Verb factories) — pick
   up opportunistically or roll into polymorphic-Path branch.
```

## What's next

```
VERDICT: PASS
Branch ready to merge. One v3 follow-up (RootComparison should reach
two more sites in PLang/App/FileSystem/) is low-priority and folds
naturally into the polymorphic-Path branch.
```
