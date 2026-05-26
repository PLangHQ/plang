# security v1 — purge-systemio-from-actions

**Branch:** `purge-systemio-from-actions`
**Phase:** blue + red
**Verdict:** **FAIL** — one HIGH path-traversal in the gate the branch was built to enforce

## TL;DR

The branch achieved its handler-discipline goal: PLNG002 at error severity
locks the syntactic door, the typed `path.@this` surface routes every disk
touch through `AuthGate`, and there is now exactly one site that calls
`System.Reflection.Assembly.LoadFrom` (gated by `Verb { Execute }`,
correctly distinct from `Read`). All of that is solid work.

But the gate itself has a soundness hole. `IsInRoot()` uses a textual
`StartsWith` of an un-canonicalized `_absolutePath` against the app root.
Combined with `file.Resolve` resolving a relative path against the active
goal's directory via `Path.Combine` (which does **not** normalize `..`),
this produces a path whose literal string lexically starts with root but
whose OS-resolved target lives outside root. AuthGate auto-grants;
`System.IO.File.ReadAllBytesAsync` reads outside root with no prompt and
no permission lookup.

Reproduced in 5 seconds as a single mutation test (created, run, deleted —
no production source touched). See finding F1 below for the full chain.

## What I verified clean

- **Assembly.LoadFrom is single-sited.** `grep -rn 'Assembly\.LoadFrom'`
  across `PLang/` returns one hit:
  `PLang/app/types/path/file/this.Operations.cs:33`, inside
  `LoadAssemblyAsync`, after `AuthGate(Execute)`. No handler reaches it
  directly anywhere else.
- **Execute is its own verb.** `Verb { Execute }` is genuinely separate
  from `Read` — `Read.Covers(Execute)` is false (the Covers chain in
  `permission/verb/this.cs:32-39` checks each sub-verb independently).
  An actor with a Read grant on a folder cannot load DLLs from it
  without a separate prompt. `Verb.AllowAll()` does include Execute,
  but `AllowAll` is referenced only from tests — production grants
  flow through `BuildRequest(actor, verb)` with the narrowly requested
  verb (`this.Authorize.cs:77-93`).
- **Wire deserialization routes through Resolve.** Per-Actor
  `Json`/`plang` serializers bake a Context-bound `PathJsonConverter`;
  stub paths (no-Context construction) explode at Authorize. The
  fall-through `Conversion.cs` builds a per-call options bag when a
  Context is available.
- **Codeanalyzer's verified-clean items hold up.** D13 `.Absolute` +
  Authorize discipline is observed at every reach site; the System.IO
  purge is real outside the documented exempts.

## Finding

### F1 — IsInRoot prefix-match allows `..` traversal past AuthGate (HIGH)

**Severity:** HIGH. Bypass of the entire path-permission model — any
verb, not just Read.

**Vector.** `path.@this.AuthGate(verb)` calls `Authorize(verb)`. `Authorize`
auto-grants when `IsInRoot()` returns true. `IsInRoot()` calls `IsUnder`
which does:

```csharp
return Absolute.StartsWith(rootWithSeparator, cmp)
    || string.Equals(Absolute, rootCandidate, cmp);
```

No canonicalization. `Absolute` is `_absolutePath` verbatim. If
`_absolutePath` contains unresolved `..` segments, the textual prefix
check succeeds; the OS resolves the `..` segments at the IO boundary and
reaches a file outside root.

`file.Resolve` is the natural production source:

```csharp
if (!rawPath.StartsWith('/') && !rawPath.StartsWith('\\') && !rawPath.Contains("://"))
{
    var goal = context.Goal;
    var runtimeDir = goal?.GetRuntimeDirectory();
    if (runtimeDir != null)
        resolved = System.IO.Path.Combine(runtimeDir.Absolute, rawPath);
    // ...
}
var p = new @this(ValidatePath(resolved, context.App), context) { Raw = rawPath };
```

`Path.Combine("<root>/subdir/.build", "../../foo.txt")` returns
`"<root>/subdir/.build/../../foo.txt"`. `ValidatePath` then *skips*
`Path.GetFullPath` because the result already lexically starts with root:

```csharp
else if (IsPlangRooted(path))
{
    if (!path.StartsWith(rootAbsolutePath) && !path.StartsWith(osAbsolutePath))
    {
        var resolved = System.IO.Path.GetFullPath(System.IO.Path.Join(rootAbsolutePath, path));
        // ... only this branch normalizes
        path = resolved;
    }
    // else: untouched, .. segments survive
}
```

Then `IsInRoot()` sees a string that starts with root and auto-grants.

**Preconditions.** A `path` whose `_absolutePath` contains unresolved
`..` and textually prefix-matches `app.AbsolutePath`. Reachable via:

- Relative `rawPath` in any handler/Variable when `ctx.Goal` is set and
  the goal has `LoadedFromPrPath` inside root → every goal loaded from
  a `.pr` in normal operation.
- Wire deserialization: an inbound JSON payload with a
  `"path": "subdir/../../etc/passwd"` field deserialized to a `Data<path>`
  via the Context-bound `PathJsonConverter` → hits the same `Resolve` →
  same bypass.
- A user PLang program that does `- read %incoming_path%` where
  `%incoming_path%` came from external input — exactly the
  "untrusted external data" case the character file says to defend against.

**Impact.** Arbitrary file read (and by symmetry write/delete/execute —
every verb auto-grants on IsInRoot=true) outside the actor's permission
scope, with no prompt and no grant lookup. Sub-actor scoping is silently
bypassed; the permission boundary the whole branch was built to enforce
is paper.

**Reproduction.** Mutation test inside `PLang.Tests` (added, run,
deleted — no production source touched). Stage `SECRET-OUTSIDE.txt` one
directory above engine root. Create a Goal with `LoadedFromPrPath` inside
root, set `ctx.Goal`, call:

```csharp
var p = global::app.types.path.@this.Resolve("../../SECRET-OUTSIDE.txt", ctx);
var result = await p.ReadText();
```

Observed output:

```
Literal Absolute: /workspace/plang/PLang.Tests/bin/Debug/net10.0/subdir/../../SECRET-OUTSIDE.txt
Starts with fixturesDir? True
Success: True
Value: if-you-can-read-me-the-gate-was-bypassed-<uuid>
```

No `Allow … to read … (y/n/a)` prompt fired. The file was read in full.

**Pre-existence.** This bypass existed before the branch — the same
`StartsWith`-without-normalization lived in the deleted
`PLangFileSystem.ValidatePath`. The branch did not introduce it. But the
branch made AuthGate the single chokepoint for every handler's filesystem
access. Now that the chokepoint is real, the chokepoint's hole is the
only thing that matters. Pre-existence is context, not exoneration.

**Proposed fix.** Two acceptable shapes — fix (b) is preferred.

(a) **Surgical**: in `path.@this.IsUnder`, normalize `Absolute` once
before the prefix check:

```csharp
private bool IsUnder(string? rootCandidate, StringComparison cmp)
{
    if (string.IsNullOrEmpty(rootCandidate)) return false;
    var canonical = System.IO.Path.GetFullPath(Absolute);  // <-- add
    var rootWithSeparator = rootCandidate.EndsWith(System.IO.Path.DirectorySeparatorChar)
        ? rootCandidate
        : rootCandidate + System.IO.Path.DirectorySeparatorChar;
    return canonical.StartsWith(rootWithSeparator, cmp)
        || string.Equals(canonical, rootCandidate, cmp);
}
```

(b) **Structural** (preferred): canonicalize at FilePath construction,
so `_absolutePath` means what its name says. Either:

- In `file.@this`'s ctor, `_absolutePath = Path.GetFullPath(absolutePath);`
- Or in `file.Resolve`, wrap the result: `ValidatePath(Path.GetFullPath(resolved), context.App)`.
- Plus: the derivation verbs (`Combine`, `WithName`, `InFolder`,
  `Parent` in `file/this.Derivation.cs`) `new @this(Path.Combine(...), Context)`
  without normalization — they'd inherit the fix for free if the base
  ctor canonicalizes.

Fix (b) also collapses a subtle correctness bug in
`Equals/GetHashCode/Relative`: those compare/hash the literal
`_absolutePath` string, so a path with `..` and the same path without
`..` hash to different buckets and compare unequal despite naming the
same file. Canonicalizing at construction makes the rule "Path identity
== filesystem identity," which is what the rest of the code assumes.

**Test the fix.** A regression test in the shape of my mutation:
LoadedFromPrPath inside root + relative rawPath with enough `..` to
climb out + assert that `AuthGate` either prompts or surfaces a denial.
This is exactly the gap test-designer's `DiscoverDotDotTraversal` slot
was supposed to cover — that scaffold needs a real body matching this
chain.

## Cross-bot follow-up

- **codeanalyzer N3** (implicit `string → path` operator) becomes
  meaningfully safer if fix (b) lands — the stub-path constructor
  starts canonicalizing too, narrowing the footgun's bite.
- **codeanalyzer N4 / N5** (AppGoals key-collision) are unaffected;
  remain LOW.
- **Test-designer `DiscoverDotDotTraversal` scaffold** needs a real
  body. The current EdgeCaseTests `Discover_PathTraversal` happens to
  pass because the test constructs the path without a Goal-context
  runtimeDir, so `file.Resolve` falls to the bare `else` branch in
  `ValidatePath` which DOES `GetFullPath`. That's the wrong shape — it
  tests the not-vulnerable case. The vulnerable case (with `ctx.Goal`
  set and `LoadedFromPrPath` inside root) needs explicit coverage.

## Standing-finding additions

This finding goes into memory as a standing rule:

> When a permission gate uses textual prefix-match for an auto-grant
> fast path, the prefix check MUST be on a canonical form of the path.
> Lexical-prefix on an un-canonicalized path is bypassable with `..`.
> Same rule applies to glob-match grants stored against literal paths
> — those should also key on canonical forms.

## Verdict

**FAIL.** Fix F1 before merging. The path-traversal bypass undermines
the entire premise of the branch.

```
VERDICT: FAIL
Issues: F1 HIGH — IsInRoot prefix-match allows .. traversal past AuthGate (no prompt, no grant lookup). Confirmed reproducible.
Next: run.ps1 coder purge-systemio-from-actions "Fix the following issues found by security: canonicalize Path._absolutePath at construction (file.@this ctor or file.Resolve) so IsInRoot()'s textual StartsWith can't be bypassed with .. segments. Add a regression test matching the mutation shape in security-report.json F1." -b purge-systemio-from-actions
```
