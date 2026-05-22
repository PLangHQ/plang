# codeanalyzer v1 — filesystem-permission

**Scope:** diff `runtime2..filesystem-permission`, +6195 / -1822 across 129
files. 43 C# files under `PLang/`. Five stages from the architect plan; all
2830 tests green per the coder summary.

**Verdict: NEEDS WORK.** Shape is correct overall — the Permission record
family, Actor.Permission home, Snapshot.Resume continuation, and the
`IExitsGoal` marker all hold their OBP shape cleanly. The concerns below are
about choreography copy-paste, sync-over-async stitching, fix-introduced dead
parameters, and one unbounded recursion. None are structural bombs; all are
the kind of thing that the coder can address in a follow-up pass without
re-opening any architect decision.

---

## Pass 1a — OBP rules (per-file)

### `PLang/App/FileSystem/Permission/this.cs` — CLEAN
Pure record + verb-named methods (`Covers`, `PathMatches`) doing real work.
No collections, no locks, no cross-file owners. Match-mode `_ => false`
fail-closed dispatch is correct.

### `PLang/App/FileSystem/Permission/Verb/{this,Read,Write,Delete}.cs` — CLEAN
Variant family with default-allow init values, per-variant `Covers` —
matches the "OBP Variant Design" pattern the architect just codified into
`good_to_know.md`. Singular folder name `Verb/` (vs `Verbs/`) is correct
per that section.

### `PLang/App/Actor/Permission/this.cs` — CLEAN shape, behavior issues below
Owns `_inMemory` + `_lock` privately; offers Find/Add/Revoke; routes
persisted grants to `App.SettingsStore` (the shared store owns its own
discipline). This is the right OBP shape — two homes unified behind one
type. Concerns are sync-over-async and a `catch {}` (see Pass 4).

### `PLang/App/FileSystem/Path.Authorize.cs` + `Path.Operations.cs` — CLEAN shape, simplification needed
`partial class Path` splits permission gate from operations; the `Path`
type owns its own authorization. No cross-file mutation. Concerns are
copy-paste pre-amble and an unbounded recursion (Pass 2/4).

### `PLang/App/Snapshot/this.Resume.cs` — CLEAN
Recursive resume chain — clunky but acknowledged in the doc-comment and
todos.md. The recursion is bounded by `chain.Count` and uses
`await using` on call frames so dispose order is correct.

### `PLang/App/Goals/Goal/this.RunFrom.cs` + `Step/this.RunFrom.cs` — CLEAN
Partial-class extension methods on Goal and Step. No shared mutable state.
The continuation owns its loop, mirrors the canonical RunAsync.

### `PLang/App/Data/this.Snapshot.cs` + `ShouldExit.cs` + `Types/Exit.cs` — CLEAN
Marker interface + verb predicate is the right shape per the file comment.
No decomposition of `Data.Value` anywhere — Type-based dispatch only.

### `PLang/App/Channels/Channel/Message/this.cs` — CLEAN
Abstract base, AskCore returns single-layer `Data<Ask>`. No state.

### `PLang/App/modules/output/ask.cs` + `modules/callback/run.cs` — CLEAN
Both ~10 lines as advertised. Delegate-only.

### `PLang/App/Errors/PermissionDenied.cs` — CLEAN
Carries the constructed Permission record; one ctor; no logic.

### `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — CLEAN shape
`PreboundHandler` is a clean `init`-only nullable property; `DispatchAsync`
absorbs former `App.Run` body; the comments explain dispose-order
intent. The class is long but that's the action entity, not bloat.

---

## Pass 1b — Shape smells (4-item checklist)

For every new/modified file in scope:

1. **Public mutable collection with rules enforced from outside?** No
   instance found. `_inMemory` in `Actor/Permission/this.cs` is private
   with private `_lock` and an internal `Add/Revoke` surface.
2. **Cross-file lock target?** No instance found.
3. **Same logical thing stored twice across types?** The Actor.Permission
   class **explicitly unifies** in-memory + sqlite grants behind one
   Find — this is *the OBP-correct form* (one owner, two stores).
   The unification predates this branch; no smell introduced.
4. **Allocate/mutate/clean-up split across three files?** No instance
   found. `Path` allocates and stores grants via `actor.Permission.Add`;
   `Path` reads them via `actor.Permission.Find`; the storage discipline
   lives inside Actor.Permission. One owner.

**Clean on Pass 1b.**

---

## Pass 2 — Simplification

### `PLang/App/FileSystem/Path.Operations.cs:28–145` — copy-paste authorize preamble (9 sites)

Every operation opens with the same three-line ritual:

```csharp
var auth = await Authorize(new Verb { Read = new ReadVerb() });
if (auth.Type?.ClrType.Exit() == true) return auth;
if (!auth.Success) return auth;
```

9 occurrences. A single helper

```csharp
private async Task<Data.@this?> AuthGate(Verb verb)
{
    var auth = await Authorize(verb);
    if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
    return null;
}
```

(or a `RequireAuth(verb, async () => …)` higher-order) shrinks each
operation to one line of plumbing. The shape-test is "if I delete lines
30–32 of `ReadText`, would any test fail?" — yes, those three lines
matter, **but** they are the same three lines repeated nine times.
Three similar lines is better than a premature abstraction; **nine
identical three-line preambles is exactly when the abstraction earns
its place.**

### `PLang/App/FileSystem/Path.Operations.cs:73,82,88` — `Stat` returns `Dictionary<string, object?>`

```csharp
return Data.@this.Ok(new Dictionary<string, object?> {
    ["exists"] = true, ["isFile"] = true,
    ["length"] = info.Length, ["modified"] = info.LastWriteTimeUtc,
});
```

Three siblings with overlapping keys. CLAUDE.md OBP doctrine ("string
manipulation for things that should be types"): this should be a `Stat`
record with `bool Exists`, `bool? IsFile`, `long? Length`, `DateTime?
Modified`. Three callers, three different key sets — typo-prone today.
**This is a public-mutable-dictionary leak from the actor across `Path`
into every Stat consumer.**

### `PLang/App/FileSystem/Path.Operations.cs:236` — `await Task.FromResult(Data.@this.Ok())`

```csharp
return await Task.FromResult(Data.@this.Ok());
```

The enclosing method is already `async`, so `return Data.@this.Ok()`
compiles identically. Drop the `await Task.FromResult` wrapper.

### `PLang/App/FileSystem/Path.Authorize.cs:25` — `AlwaysExpiry` constant is unused

```csharp
private static readonly TimeSpan AlwaysExpiry = TimeSpan.FromDays(365 * 100);
```

Read in zero call sites; the `EnsureSigned()` on line 67 carries a comment
saying "future: pass `AlwaysExpiry`". Decide: either thread it through
`EnsureSigned(expires)` now, or delete the constant + comment. A test
name in `PathAuthorizeTests.cs:68` still says
`AuthorizeStatefulAnswerA_SignsWithAlwaysExpiry…` — also worth aligning.

### `PLang/App/Snapshot/this.cs:15–17` — duplicate `Dictionary` for sections + entries

Two `Dictionary<string, …>` fields with overlapping keys would be the
canonical shape-smell-1, but here `_sections` holds nested `@this` and
`_entries` holds `object?` — different value types, distinct ownership.
Not a smell, just noting we *looked*.

### `PLang/App/modules/error/handle.cs:154,170` — `cause` parameter is now dead

```csharp
private static async Task<global::App.Data.@this> RunRecovery(
    List<ActionEntity> actions, Actor.Context.@this context,
    Call? cause)  // ← never used in body (line 181: action.RunAsync(context))
```

The doc-comment still says "dispatched through App.Run with cause threaded
through." Both the comment and the parameter are stale — RunAsync no
longer takes cause, the body no longer threads it. Same for
`RunRecoveryWithErrorScope` and its `erroredCall` argument (line 154).
**Delete the parameters or restore the cause linkage; pick one.**

### `PLang/App/CallStack/Call/this.cs:46,56–58,131` — `Cause` chain is structurally dead

`_ownCause` is always assigned `null` (line 131). The `Cause` property
walks `Caller?.Cause`, which always resolves to null. Renderers in
`PLang/App/Errors/CallChainRenderer.cs:63,65,79,82` read `frame.Cause`
and silently no-op. A test in `CallChainRendererTests.cs:82` even
documents this.

The coder's deferred list notes "re-introduction (or full removal of the
Cause field) is follow-up." Calling it out here so it doesn't get
forgotten: **dead branch in the renderer (`if (annotateCause && frame.Cause
!= null)` is forever false)**. Either delete the cause-aware paths in
the renderer + the Call property, or restore the threading at error/handle
recovery. Today's state is two dead halves of a feature.

### `PLang/App/Data/this.Envelope.cs:78` — `EnsureSigned` blocks on async

```csharp
var result = _context.App.RunAction(action, _context).GetAwaiter().GetResult();
```

`EnsureSigned()` is sync because the signature property getter is sync.
This is documented; not new to this branch but worth flagging in context
of the new sync→async sites added below.

---

## Pass 3 — Readability

### `PLang/App/FileSystem/Path.Authorize.cs:38` — question prefix concatenation

```csharp
var question = $"{prefix}Allow {actor.Name} to {VerbLabel(verb)} {Absolute}? (y/n/a)";
```

`prefix` is only ever set to `"Invalid answer '…'. "` via the recursion
on line 56. A reader has to scan the whole method to discover that. A
named helper (`BuildQuestion(verb)` + a separate `BuildRetryQuestion(badAnswer, verb)`)
would say it once.

### `PLang/App/FileSystem/Path.Operations.cs:155–212` — `BundledTransfer` mixes ask + dispatch

The bundled-prompt path repeats the Authorize pattern manually instead of
delegating. It allocates a Verb, asks, switches on the answer, calls
`StoreGrant` per missing path, then performs. The code is correct but
duplicates the y/n/a branching from `Authorize`. Worth extracting the
"ask + interpret y/n/a" core so `Authorize` and `BundledTransfer` both
call it.

### `PLang/App/Channels/Channel/Stream/this.cs:107–109` — StreamReader leak risk

```csharp
using var reader = new StreamReader(Stream, ResolveEncoding(),
    detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
var line = await reader.ReadLineAsync(timeoutCts.Token);
```

`leaveOpen: true` plus `using` is correct (the reader disposes without
closing the stream). Worth a one-line comment that the `leaveOpen` is
deliberate so a later reader doesn't "fix" it.

### `PLang/App/Goals/Goal/Steps/Step/this.cs:155` — `ShouldExit() || result.Handled`

Inside Step.RunAsync the break condition is `result.ShouldExit() ||
result.Handled`. `ShouldExit` already covers `!Success && !Handled`. The
`|| Handled` covers the success-but-handled override path. Readable, but
a future reader will wonder whether `ShouldExit` should include `Handled`.
Either:

- Leave a one-line comment saying handled is a separate stop reason that
  intentionally doesn't belong in `ShouldExit` (because downstream code
  reads `Returned`/`Type`/`Success` distinctly), **or**
- Add a `Step.ShouldBreak(d)` predicate that bundles both, and use it.

---

## Pass 4 — Behavioral reasoning

### `PLang/App/FileSystem/Path.Authorize.cs:56` — unbounded recursion on bad input

```csharp
_ => await Authorize(verb, prefix: $"Invalid answer '{answer}'. "),
```

Every invalid answer recurses. Each recursion `await`s a fresh `output.ask`.
Adversarial input (a misbehaving Message channel that returns "x" forever)
grows the async state machine without bound. **Replace recursion with a
loop:** `while (true) { … switch { case "n": …; case "a": …; case "y": …;
default: prefix = "Invalid answer '…'. "; continue; } break; }`.

Same finding applies to `Path.Operations.cs:210` (`BundledTransfer` recurses
on garbage).

### `PLang/App/Actor/Permission/this.cs:142–148` — silent `catch`

```csharp
private bool VerifySignature(global::App.Data.@this<PermissionRecord> data)
{
    try { … return result.Success; }
    catch { return false; }
}
```

Bare `catch` swallows every exception (OOM, OCE, anything). The signing
verify path can fail for legitimate reasons (bad key, tampered envelope) —
those produce `result.Success == false` cleanly. The `catch` here masks
real bugs (NRE, cancellation, internal contract breaks) as "permission
denied", which then reprompts the user — confusing to debug.

**Fix:** filter as other catch sites in this codebase do, e.g.
`catch (Exception ex) when (ex is not (NullReferenceException or
OutOfMemoryException or StackOverflowException or OperationCanceledException))`,
and log the swallowed exception via `context.App.Debug.Write`.

### `PLang/App/Actor/Permission/this.cs:55–56, 86, 117, 145` — sync-over-async

`Find`, `Add`, `Revoke`, and `VerifySignature` all do
`.GetAwaiter().GetResult()` on async `SettingsStore` and `RunAction`
calls. `Find` is then called from `Path.Authorize` (an `async` method
on line 35), and `Add` from `SignAndStore` (called from a different
async context on line 53). **The async path is broken at the
Actor.Permission boundary.** This sync-over-async pattern is
deadlock-prone (less so in pure-net10 console apps but still
discouraged) and gives up the opportunity to fan out persistence work.

Promote the four methods to `Task<…>`. Path.Authorize is already async,
so the migration is mechanical. **This is the single biggest concrete
follow-up I'd ask the coder to take on.**

### `PLang/App/FileSystem/Path.Authorize.cs:82` — `OrdinalIgnoreCase` for path prefix

```csharp
return Absolute.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
    || string.Equals(Absolute, root, StringComparison.OrdinalIgnoreCase);
```

PLang runs on Linux and Windows. On Linux, `Absolute` and `root` are
case-sensitive — comparing case-insensitively here means
`/srv/myapp` matches `/SRV/myapp`. On Windows the comparison is
correct. **Pick per-OS** (use `StringComparison.Ordinal` on Unix-likes,
`OrdinalIgnoreCase` on Windows) — `OperatingSystem.IsWindows()` is the
canonical switch. Today's behaviour bypasses the gate when an attacker
controls the absolute-path casing on Linux.

### `PLang/App/FileSystem/Path.Operations.cs:51, 142–144` — `Exists` requires Read

`ExistsAsync` requests Read permission; if denied the caller can't even
check existence. `Delete` returns Ok when neither file nor directory
exists (line 144) — that's a fail-open on idempotent delete, which is
fine for `rm -f` semantics but worth a doc-comment so a future reader
doesn't classify it as a missing check.

### `PLang/App/Goals/Goal/Steps/Step/this.RunFrom.cs:28–38` — exception → ServiceError pattern

The catch block reconstructs the error key by stripping "Exception" from
the type name. Same pattern appears in the canonical Step.RunAsync —
they should share the helper rather than diverge. Otherwise a future
edit to one will silently desynchronise behaviour between fresh runs
and resumed runs.

---

## Pass 5 — Deletion test

For every code path reviewed, "if I deleted lines X-Y, would any test
fail?"

| Lines | What | Verdict |
|---|---|---|
| `Path.Authorize.cs:25` `AlwaysExpiry` constant | unused | **Delete or wire** |
| `CallStack/Call/this.cs:46,56–58,131` `_ownCause`/`Cause` | always null | **Delete-with-renderer or restore** |
| `Errors/CallChainRenderer.cs:63,65,79–82` Cause branches | dead | **Delete with Call.Cause** |
| `modules/error/handle.cs:154,158,170,181` `cause`/`erroredCall` | passed but unused | **Delete the parameters** |
| `Path.Operations.cs:236` `await Task.FromResult(…)` | redundant | **Delete the await wrapper** |
| `App.this.cs:418–426` block of "App.Run collapsed…" comment | history note inside production class | Keep — it's load-bearing for the coder's stage 2a story; will be deleted when App.RunAction goes |

---

## Specific tests that would shake out if findings are addressed

- The four sync-over-async sites in `Actor/Permission/this.cs` are
  exercised by `ActorPermissionStorageTests` (12 tests). Bumping the
  methods to async will need those test signatures updated.
- The unbounded-recursion fixes in `Path.Authorize` / `BundledTransfer`
  would benefit from a new "garbage input, N rounds" test asserting
  the loop terminates (today's tests don't cover the loop bound).
- Pass 4 case-insensitive `IsInRoot` should grow a Linux-specific test
  that proves `/srv/myapp` and `/SRV/myapp` are *not* both in-root.

---

## What the coder did well

Worth saying loudly so the follow-up doesn't sound net-negative:

- **The Permission record family is textbook OBP.** Singular folders,
  variant records with default-allow, owners doing their own coverage,
  methods taking whole domain objects (`Covers(@this request)`). The
  architect's own "OBP Variant Design" section in `good_to_know.md`
  reads like it was written *after* this code, but it wasn't.
- **The `IExitsGoal` marker + `Type.Exit()` predicate** is the
  smallest possible surface to express "stop the step." Two files, ~25
  lines combined.
- **Snapshot.Resume's recursion is honest** — the doc-comment says
  "acknowledged-clunky, tracked in todos.md" instead of pretending it's
  clever. That's the kind of comment that *does* earn its place.
- **All 2830 tests green.** This is a 6k-LOC restructure that touches
  the engine spine; getting the full suite through is genuinely hard.
