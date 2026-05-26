# security — purge-systemio-from-actions

## Version

v1.

## What this is

The branch's stated goal IS a security hardening: purge `System.IO` from
action handlers, force every disk touch through a typed `path.@this`
surface that calls `AuthGate(verb)`. My job was to verify the hardening
is real and look for new attack surface introduced by the typing flip.

## What was done

Blue + red audit, four passes:

1. **Gate surface.** Read `path.@this.Authorize`, `IsInRoot`,
   `AuthGate`, the new Derivation/Operations partials, the new
   `permission.verb.Execute`, and the new `PathJsonConverter`.
2. **Single-site verification.** Grep across `PLang/` confirmed
   `System.Reflection.Assembly.LoadFrom` has exactly one call site —
   inside `FilePath.LoadAssemblyAsync`, after `AuthGate(Execute)`.
   `Execute` is genuinely distinct from `Read` in the `Covers` chain.
3. **Wire-deserialization audit.** `PathJsonConverter.Read` routes
   through `@this.Resolve(raw, context)` when Context is wired; falls
   back to no-Context stub paths that explode at Authorize otherwise.
4. **Red team / mutation test.** Constructed and ran a mutation test
   inside `PLang.Tests` to verify a suspected path-traversal bypass.
   Confirmed reproducible in ~5s. Mutation file deleted; production
   source untouched.

## Verdict

**FAIL** — one HIGH severity finding (F1) in the gate the branch was
built to enforce.

### F1 (HIGH) — IsInRoot prefix-match allows `..` traversal past AuthGate

`IsInRoot()` uses textual `StartsWith` of an un-canonicalized
`_absolutePath` against the app root. A relative path with `..` resolved
against a goal-anchored `runtimeDir` inside root via `Path.Combine`
produces a string that *textually* prefix-matches root but *OS-resolves*
outside it. AuthGate auto-grants on IsInRoot=true; `System.IO.File.*`
resolves the `..` segments and reads outside root with no prompt and no
permission lookup.

Reproduced via mutation test:

```
Literal Absolute: <root>/subdir/../../SECRET-OUTSIDE.txt
Starts with fixturesDir? True
Success: True
Value: if-you-can-read-me-the-gate-was-bypassed-<uuid>
```

The bypass pre-existed (same flaw lived in the deleted
`PLangFileSystem.ValidatePath`), but it was masked while handlers
bypassed AuthGate entirely. Now that AuthGate is the single chokepoint,
the chokepoint's hole is the only thing that matters.

Files: `PLang/app/types/path/this.Authorize.cs`,
`PLang/app/types/path/file/this.Validate.cs`,
`PLang/app/types/path/file/this.cs`.

**Fix (preferred):** canonicalize `_absolutePath` at FilePath
construction via `Path.GetFullPath` — in `file.@this`'s ctor or in
`file.Resolve` after the `Path.Combine`. Derivation verbs (Combine,
WithName, InFolder, Parent) inherit the fix for free. Add a regression
test matching the mutation shape.

Fix (surgical, smaller): canonicalize inside
`path.@this.IsUnder` once before the `StartsWith` check.

See `security-report.json` and `v1/result.md` for the full finding,
exploit walkthrough, and proposed-fix sketch.

## What's clean (verified)

- `Assembly.LoadFrom` is single-sited and gated by `Verb { Execute }`,
  which is genuinely distinct from `Verb { Read }`. The `Covers` chain
  treats each sub-verb independently. `Verb.AllowAll()` does include
  Execute, but `AllowAll` is referenced only from tests; production
  grants flow through `BuildRequest(actor, verb)` with the narrow
  verb. Clean factoring.
- `PathJsonConverter` Context-bound routes through `Resolve` → scheme
  registry. Stub paths from no-Context construction explode at
  Authorize.
- Codeanalyzer's verified-clean items hold up under security re-read:
  D13 `.Absolute` + Authorize discipline observed at every reach site;
  System.IO purge real outside the documented exempts; PLNG002 at
  error severity locks the door at the syntactic layer.

## Code example (the bypass shape)

```csharp
// Goal whose .pr lives under root.
var goal = new global::app.goals.goal.@this
{
    Name = "Probe",
    Path = "/subdir/probe.goal",
    LoadedFromPrPath = path.@this.Resolve("<root>/subdir/.build/probe.fake", ctx),
};
ctx.Goal = goal;

// .. segments climb past root in the OS resolver, but the literal
// _absolutePath string still starts with root → IsInRoot true → auto-grant.
var p = path.@this.Resolve("../../SECRET-OUTSIDE.txt", ctx);
var result = await p.ReadText();  // No prompt. File outside root is read.
```

## Follow-up proposals (non-blocking)

- **F2** — Migrate `PLang/app/modules/MarkdownTeaching.cs` to `path.@this` verbs (`List`, `ReadText`). Closes the only whole-file PLNG002 exemption that hides real ungated IO. Architect's bootstrap-timing rationale doesn't hold — `Describe()` runs after App is up.
- **F3 / PathHelper** — Introduce `app.Utils.PathHelper` as a typed forwarder for pure path-string-math (`Combine`/`GetDirectoryName`/`GetFullPath`/`GetFileName`/`ChangeExtension`/`Join` + separator constants). Strictly **no IO**. Replaces the analyzer's invisible `AllowedSystemIoPathMembers` string-name allowlist with a type-name allowlist. Retires the whole-file `app/this.cs` exemption. Full proposal: `v1/pathhelper-proposal.md`.

Both interlock cleanly with the F1 fix: F1's canonicalization site (`file.@this` ctor) calls `PathHelper.GetFullPath`. Recommend coder lands F1 first, folds PathHelper in as a clean-up before merge.

## Next

```
VERDICT: FAIL
Issues: F1 HIGH — IsInRoot prefix-match allows .. traversal past AuthGate.
Next: run.ps1 coder purge-systemio-from-actions "Fix the following issues found by security: canonicalize Path._absolutePath at construction (file.@this ctor or file.Resolve) so IsInRoot()'s textual StartsWith can't be bypassed with .. segments. Add a regression test matching the mutation shape in security-report.json F1." -b purge-systemio-from-actions
```
