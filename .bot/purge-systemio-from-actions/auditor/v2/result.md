# auditor v2 — result

**Verdict: PASS.** v1 finding F1 closed; no new findings.

## Verification I ran

Clean rebuild (`rm -rf {Plang*,PLang*,PLang.Generators}/{bin,obj}`):

```
dotnet build PlangConsole          → 0 errors, 0 PLNG002 diagnostics
dotnet run --project PLang.Tests   → 3031 / 3031 pass
cd Tests && plang --test           → 206 / 206 pass        ← v1 regression closed
```

Same numbers as runtime2 baseline. My v1 finding F1 (CompileLlmNotes
two-test regression) is genuinely gone.

## How the F1 follow-up works — and why it doesn't reopen the security F1

The auditor v1 finding pointed at the F1 canonicalization (commit
`064724fda`) as the introducing change. Coder addressed it in
`bfb34bca4` by widening `IsInRoot`'s root set to walk the parent chain:

```csharp
// path/this.Authorize.cs:102-116
protected bool IsInRoot()
{
    var app = Context?.App;
    if (app == null) return false;
    const int MaxDepth = 16;
    for (int depth = 0; app != null && depth < MaxDepth; depth++)
    {
        if (IsUnder(app.AbsolutePath, RootComparison)
            || IsUnder(app.OsDirectory, RootComparison)
            || IsUnder(app.OsAbsolutePath, RootComparison))
            return true;
        app = app.Parent;
    }
    return false;
}
```

Plus a new settable `app.@this.Parent` and one wire-up in `test/run.cs`
(`childApp.Parent = parentApp`).

**Does this reopen security F1 (the IsInRoot `..` bypass)?** No.
The `IsUnder` checks still run against `Absolute` — the canonicalized
`_absolutePath` — so any `..` segments in the original raw path were
resolved at the FilePath ctor before the walk ever started. Widening
the *set of roots* doesn't change *what gets compared*. A path like
`/Tests/Builder/CompileLlmNotes/.build/../../Simple/...` canonicalizes
to `/Tests/Simple/...` before reaching `IsInRoot`, then matches under
the parent (`/Tests/`). That's the intended behavior, and it's the
same `Absolute` form a textual attacker can't fabricate.

**Can an attacker manipulate `App.Parent` to elevate scope?** `Parent`
is publicly settable, but an action handler's only reachable `app.@this`
instances are `ctx.App` and (if set) its `Parent` chain — both
constructed by trusted code (`PlangConsole/Program.cs` or `test/run.cs`).
Setting `ctx.App.Parent = something_attacker_chose` requires a ref to
an `app.@this` the attacker doesn't have. Across a single process,
there's no path from action code to a sibling app's instance. Standing
watch, not an active risk.

**Cycle safety.** `MaxDepth = 16` returns false on a `a.Parent = b;
b.Parent = a` cycle (codeanalyzer v2 N1 closure). Any legitimate
nesting is 1 hop today.

## Assessment of intervening reviews

| Bot | My read |
| --- | --- |
| codeanalyzer v2 | **agree** — 5 LOW findings well-spotted; N1 (Parent cycle) and N2 (bare catch) addressed by coder in `ecdd0de4f`; N3 (IsUnder string-alloc), N4 (ValidatePath probes), N5 (ScanOrphans inverted filter) deferred as documented latency/cosmetic. |
| coder bfb34bca4 + ecdd0de4f | **agree** — Parent chain is the minimal correct fix; the N2 follow-up actually went *stronger* than codeanalyzer suggested (removed the try/catch entirely in `Canonicalize` rather than filtering — any `GetFullPath` exception now escapes loud, with a comment naming the expected throw set). |

## PLNG002 suppression re-audit

Grepped again for `pragma warning disable PLNG`, `[SuppressMessage(...,
"PLNG002")]`, `NoWarn` containing PLNG, and `.editorconfig`
`dotnet_diagnostic.PLNG002` — **zero hits**. Exactly two file-scope
carve-outs in `Plng002.cs`:

- `IsPathHelperFile` at `Plng002.cs:99-100` — `PLang/app/Utils/PathHelper.cs`,
  scoped to `Path.*` members only.
- `IsPathTypeSurface` at `Plng002.cs:91-92` — `PLang/app/types/path/`,
  scoped to `File/Directory/FileInfo/...` only.

Per Ingi's explicit ask: the analyzer's only exemptions are the two
visible at the use site inside the analyzer itself. Tight.

## Verdict

```
status: pass
summary: v1 F1 closed via App.Parent chain in IsInRoot; clean rebuild
         206/206 + 3031/3031; PLNG002 carve-outs unchanged (exactly 2,
         both inside Plng002.cs).
```
