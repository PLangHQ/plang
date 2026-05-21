# codeanalyzer — filesystem-permission

## Version
v2

## What this is
Second pass after the coder addressed v1's ten findings and shipped seven
new functional commits (verb-null-by-default, channel.set goal-channel
wiring, `%__data__% → %!data%` rename, Stream EOF fail-fast, `//tmp/X`
out-of-root, action handlers wired through `Path.Authorize`, dropped
stale plang tests). +226 / -109 across 33 C# files since v1.

This v2 review re-verifies the v1 fixes and walks the new commits with the
same five passes (OBP rules + shape smells; simplification; readability;
behavioral reasoning; deletion test).

## What was done

- Re-read `v1/report.md` and verified all ten v1 findings are properly
  fixed in commits `af32f3ece..f543e19ca` (Actor.Permission async sweep,
  filtered catch, loop-not-recursion in Authorize/BundledTransfer,
  OS-aware IsInRoot, AuthGate helper, StatInfo record, dead Cause/cause
  removal, AlwaysExpiry deletion, redundant await drop).
- Ran the five passes on the new commits (`bd730d39f..dc3a17890`).
- Wrote `v2/report.md` with line-cited findings.
- Wrote `v2/verdict.json`: **fail** — two regressions of v1 findings at
  sibling sites.

## Verdict: NEEDS WORK

All v1 fixes hold. **Two regressions in the new commits:**

1. **v1 #1 reintroduced one floor up.** `AuthGate` was added to
   `Path.Operations` as v1 prescribed, but it's `private`. The seven new
   file action handlers (`PLang/App/modules/file/{read,save,copy,move,
   delete,exists,list}.cs`) each copy-paste the same two-line
   authorize preamble instead — exactly the v1 #1 shape, one layer up.
2. **v1 #4 at a sibling site.** The OS-aware case-comparison fix landed
   in `Path.Authorize.IsInRoot`, but
   `PLangFileSystem.ValidatePath:227` still uses
   `OrdinalIgnoreCase` `StartsWith`. Same Linux false-match bug,
   different file.

Other new code is clean: `Verb` defaults flipped to null (correct — fixes
JSON signature round-trip), `Stream.AskCore` EOF fail-fast is sound,
`test.run.FreezeFoundational` is the right fix in the right place, the
`%!data%` rename is mechanical and consistent.

## Top findings (line-cited; full detail in `v2/report.md`)

**Must fix:**

1. `PLang/App/modules/file/{read,save,copy,move,delete,exists,list}.cs:32`
   (and matching lines in siblings) — 7 sites, 9 calls of the identical
   two-line authorize preamble. Either promote `Path.AuthGate` to public
   and call it (`if (await Path.Value!.AuthGate(verb) is { } early)
   return early;`) OR route handlers through `Path.Operations` and delete
   the `IFile.X` surface. The `Path.Operations` doc-comment already
   advertises (b) — wiring stopped short.
2. `PLang/App/FileSystem/Default/PLangFileSystem.cs:227` —
   `path.StartsWith(RootDirectory, OrdinalIgnoreCase)` — Linux case-
   sensitive paths get false matches. Hoist the v1 `IsInRoot` OS-switch
   into a `RootComparison` helper and use it at both sites.

**Should fix:**

3. `PLang/App/modules/file/{copy,move}.cs:35` — two prompts on a fresh
   out-of-root pair; `Path.Operations.BundledTransfer` already does the
   single combined prompt for the same operation. Route action handlers
   through it. (Resolves both 2.2 and 4.4.)
4. `PLang/App/FileSystem/Default/PLangFileSystem.cs:184–188` — empty `if`
   body with a tail-of-chain `else`. Restructure to invert/extract; carry
   the load-bearing comment with it.

**Nice to have:**

5. `PLang/App/modules/test/run.cs:79–86` — `FreezeFoundational` is the
   correct fix, but no test pins the goal-channel-recursion-on-self
   failure mode. Add a regression test so a future contributor moving
   the freeze later doesn't silently reintroduce the stack overflow.
6. `PLang/App/Channels/Channel/Stream/this.cs:108` — "the prompt is just
   lost" silent fallthrough when neither output nor self can write. Log
   or hard-fail.
7. `PLang/App/FileSystem/Permission/Verb/this.cs` — given 16 sites of
   `new Verb { Read = new ReadVerb() }` across `Path.Operations` and the
   action handlers, add `Verb.ReadOnly()` / `Verb.WriteOnly()` /
   `Verb.ReadWrite()` factories mirroring `AllowAll()`.

## Code example — the most representative finding

The v1 #1 regression. `PLang/App/modules/file/read.cs:30–43`:

```csharp
public async Task<Data.@this> Run()
{
    var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
    if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;

    var result = Files.Read(this);
    // … ResolveVariables branch …
    return result;
}
```

`Path.Operations.cs:39` already has:

```csharp
private async Task<Data.@this?> AuthGate(Verb verb)
{
    var auth = await Authorize(verb);
    if (auth.Type?.ClrType.Exit() == true) return auth;
    if (!auth.Success) return auth;
    return null;
}
```

Promoting it to `public` would let `read.cs:32–33` collapse to:

```csharp
if (await Path.Value!.AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
```

— one line per handler instead of two. Same logic as v1 #1.

## What's next

```
VERDICT: FAIL
Issues: v1 #1 regressed at handler layer (PLang/App/modules/file/*.cs,
7 sites of duplicated authorize preamble because Path.AuthGate is
private); v1 #4 regressed at sibling site (PLangFileSystem.ValidatePath:227
Linux case-insensitive StartsWith). Plus copy/move using two-prompt path
when BundledTransfer exists, empty-if-body in ValidatePath, missing
regression test for goal-channel-recursion fix.
Next: run.ps1 coder filesystem-permission "Fix the two regressions from
codeanalyzer v2: (1) promote Path.AuthGate to public (or route the seven
file action handlers through Path.Operations) so the 9-site copy-paste
preamble doesn't live at the handler layer; (2) hoist the OS-aware
case-comparison from Path.Authorize.IsInRoot into a shared helper and
use it at PLangFileSystem.ValidatePath:227. Then route copy/move handlers
through Path.Operations.BundledTransfer to get the single-prompt UX."
-b filesystem-permission
```
