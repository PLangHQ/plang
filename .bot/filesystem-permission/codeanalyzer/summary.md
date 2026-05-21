# codeanalyzer — filesystem-permission

## Version
v1

## What this is
The filesystem-permission branch landed five stages from the architect plan
(permission types, snapshot-resume engine, Path.Authorize gate,
Actor.Permission storage binding, IPLangFileSystem v2 surface) plus
Stage 5 Messages-end-to-end fixtures. ~6,200 lines added, ~1,800 removed
across 129 files. Coder reports 2830/2830 tests green.

This codeanalyzer review walks the diff with five passes (OBP rules + shape
smells; simplification; readability; behavioral reasoning; deletion test)
and produces a line-cited report.

## What was done

- Read `report.json` (4 prior sessions: 3 architect, 1 test-designer; no
  coder session entry — coder posted commits and `summary.md` but didn't
  write a session block) and appended my session.
- Wrote `v1/plan.md`; ran the five passes against the 43 modified C#
  files under `PLang/App/`.
- Wrote `v1/report.md` with line-cited findings.
- Wrote `v1/verdict.json`: **fail** with a one-line summary.

## Verdict: NEEDS WORK

Shape is correct — Permission record family, Actor.Permission home,
Snapshot.Resume continuation, and the `IExitsGoal` marker all hold their
OBP shape cleanly. Findings are about choreography copy-paste, sync-over-
async stitching, fix-introduced dead parameters, and one unbounded
recursion. None are structural; all are follow-up-pass fodder.

## Top findings (cite list — full detail in `v1/report.md`)

**Behavioral (Pass 4) — fix first:**

1. `PLang/App/Actor/Permission/this.cs:55,86,117,145` — four sync-over-async
   call sites (`.GetAwaiter().GetResult()`). Promote Find/Add/Revoke to
   `Task<…>`; Path.Authorize is already async, migration is mechanical.
2. `PLang/App/FileSystem/Path.Authorize.cs:56` — unbounded recursion on
   invalid answer. Same pattern at `Path.Operations.cs:210`. Use a loop.
3. `PLang/App/Actor/Permission/this.cs:148` — bare `catch { return false; }`
   masks NRE/OCE as "denied". Filter exceptions like other catch sites.
4. `PLang/App/FileSystem/Path.Authorize.cs:82` — `IsInRoot` uses
   `OrdinalIgnoreCase` everywhere. On Linux, paths are case-sensitive;
   case-insensitive comparison lets `/SRV/myapp` slip past the gate.

**Simplification (Pass 2):**

5. `PLang/App/FileSystem/Path.Operations.cs:28-145` — same three-line
   `Authorize` preamble repeated nine times. Single helper collapses it.
6. `PLang/App/FileSystem/Path.Operations.cs:73,82,88` — `Stat` returns
   `Dictionary<string, object?>`. Should be a `Stat` record.
7. `PLang/App/modules/error/handle.cs:154,170` + `RunRecovery` —
   `cause`/`erroredCall` parameters are dead. Delete or restore threading.
8. `PLang/App/CallStack/Call/this.cs:46,56-58,131` + Errors/CallChainRenderer
   Cause branches — `_ownCause` is always null. Coder's deferred item;
   today's state is two dead halves of a feature.

**Deletion (Pass 5):**

9. `PLang/App/FileSystem/Path.Authorize.cs:25` — `AlwaysExpiry` constant
   unused; either thread or delete.
10. `PLang/App/FileSystem/Path.Operations.cs:236` — `await Task.FromResult(…)`
    redundant inside an async method.

## Code example — the most representative finding

The `Authorize` preamble repeats nine times in `Path.Operations.cs`:

```csharp
public async Task<Data.@this> ReadText()
{
    var auth = await Authorize(new Verb { Read = new ReadVerb() });
    if (auth.Type?.ClrType.Exit() == true) return auth;
    if (!auth.Success) return auth;
    var text = await System.IO.File.ReadAllTextAsync(Absolute);
    return Data.@this.Ok(text);
}
```

Three similar lines is better than a premature abstraction. **Nine
identical three-line preambles is exactly when the abstraction earns its
place.** Each operation should shrink to one preamble line:

```csharp
public async Task<Data.@this> ReadText()
{
    if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
    var text = await System.IO.File.ReadAllTextAsync(Absolute);
    return Data.@this.Ok(text);
}
```

## What's next

```
VERDICT: FAIL
Issues: Sync-over-async at Actor.Permission, unbounded recursion in
Path.Authorize/BundledTransfer, silent catch in VerifySignature,
case-insensitive IsInRoot on Linux, 9× authorize preamble copy-paste,
dead cause/Cause params after stage 2a.5.
Next: run.ps1 coder filesystem-permission "Fix the issues found by codeanalyzer: promote Actor.Permission Find/Add/Revoke to async, replace recursive bad-input handling in Path.Authorize and BundledTransfer with a loop, filter the catch in VerifySignature, fix case-sensitive IsInRoot on Linux, extract the 9× authorize preamble into a helper, and resolve the dead Cause/cause parameters (delete or restore)." -b filesystem-permission
```
