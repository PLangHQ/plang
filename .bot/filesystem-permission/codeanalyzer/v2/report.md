# codeanalyzer v2 — filesystem-permission

**Scope:** seven new functional commits after the v1-fix landings
(`bd730d39f..dc3a17890`), +226 / -109 across 33 C# files under `PLang/`.
Plus the `%__data__% → %!data%` rename, mechanical but touches a lot of
comments/examples. All v1 fixes (#1–#10) re-verified clean before this
pass — Actor.Permission async sweep, filtered catch, AuthGate helper,
StatInfo record, loop-not-recursion in Authorize/BundledTransfer, OS-aware
IsInRoot, dead Cause/cause removal — all properly applied.

**Verdict: NEEDS WORK.** The v1 architectural fixes hold. But the new
action-handler wiring (commit 65a95c0d8) regresses v1 finding #1 — the
exact two-line authorize-preamble copy-paste that v1 told the coder to fold
into `AuthGate` is now reintroduced at the action-handler boundary (7 sites,
9 calls). `Path.Operations` got its helper; the handler layer did not, and
worse, the handlers route around `Path.Operations` entirely. The v1 finding
#4 also re-emerged at a sibling site (`PLangFileSystem.ValidatePath:227`).
The other five commits are clean — the verb-null-by-default change is the
right call, the Stream EOF fail-fast is sound, `%!data%` is a consistent
rename. Fix the two regressions and this goes to PASS.

---

## Pass 1a — OBP rules (per-file)

### `PLang/App/FileSystem/Permission/Verb/this.cs` — CLEAN (refined)
Defaults flipped from default-allow init values to default-null. The v1
default broke JSON signature round-trip because omitted verbs deserialised
as `null` instead of `new Read()`, so signed bytes differed pre/post
storage. `AllowAll()` factory now names the "fully granted" intent at call
sites. Variant family shape preserved.

### `PLang/App/FileSystem/Default/PLangFileSystem.cs:164–230` — CLEAN shape; bug + smell below
`ValidatePath` lets out-of-root paths through (no more
`UnauthorizedAccessException`) and delegates gating to `Path.Authorize`.
That's the correct single-owner consolidation. Concerns in Pass 2/4.

### `PLang/App/Channels/Channel/Stream/this.cs:87–142` — CLEAN
Two-call ask pattern correctly resolves the actor's `Output` channel before
falling back to self-write. The comment naming the CLAUDE.md rule
(`"Console.* Is Banned"`) is exactly the kind of WHY a future maintainer
needs. EOF fail-fast (line 126) is correct.

### `PLang/App/modules/file/{read,save,copy,move,delete,exists,list}.cs` — SHAPE OK, copy-paste below
Each handler does its own authorize step before delegating to
`Files.X(this)`. Shape per file is fine — they're thin handlers. The
copy-paste across them is Pass 2.

### `PLang/App/modules/output/ask.cs` — CLEAN
`Run()` delegates to `Context.Actor.Channels.Resolve(Input).Ask(this)`
instead of unconditionally building an AskCallback. Single ownership of the
ask shape lives in the channel; the handler is ~15 lines. The `!ask.answer`
resume sentinel + `Variables.Remove("!ask")` consumption is correctly
scoped (the comment on line 56–58 explains why).

### `PLang/App/modules/test/run.cs:79–86` — CLEAN, surgical fix
`FreezeFoundational()` called eagerly on the child `App` for both actors
prevents the goal-channel-recurses-on-itself stack overflow described in
the comment. The fix is in the right place — `test.run` is the boundary
where a child app spins up, so any "default state must be frozen here"
discipline belongs in that spin-up path, not inside `Actor.@this`.

### `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:188–196` — CLEAN
The rename `__data__` → `!data` is mechanical. The comment correctly notes
that the alias survives the override path (`beforeResult.Handled`) because
the write is identical. No shape change.

---

## Pass 1b — Shape smells (4-item checklist)

1. **Public mutable collection with rules enforced from outside?** None
   new.
2. **Cross-file lock target?** None new.
3. **Same logical thing stored twice across types?** **YES — see Pass 2.1.**
   The "authorize a verb against this path" choreography now lives in two
   places: `Path.AuthGate` (one home, used by `Path.Operations` × 9) and
   inline-copy-pasted across seven file action handlers. Same logical
   step, two homes, same return contract.
4. **Allocate-here / mutate-there / clean-up-elsewhere?** None new.

The smell-3 hit is exactly v1 finding #1 reintroduced one floor up.

---

## Pass 2 — Simplification

### 2.1 `PLang/App/modules/file/{read,save,copy,move,delete,exists,list}.cs` — copy-paste authorize preamble (7 sites, 9 calls) — **v1 #1 REGRESSION**

Every file handler opens with this (`read.cs:32–33`):

```csharp
var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
return Files.Read(this);
```

(or two pairs for `copy`/`move`). This is **exactly v1 finding #1** that
motivated the `AuthGate` helper — and `AuthGate` already exists on `Path`
(line 39 of `Path.Operations.cs`) doing this identical job:

```csharp
private async Task<Data.@this?> AuthGate(Verb verb)
{
    var auth = await Authorize(verb);
    if (auth.Type?.ClrType.Exit() == true) return auth;
    if (!auth.Success) return auth;
    return null;
}
```

`AuthGate` is `private`. The handlers can't call it. **Two ways out:**

(a) **Promote `AuthGate` to `public`** and have handlers call it:
`if (await Path.Value!.AuthGate(verb) is { } early) return early;`. Shaves
one line per handler and unifies the contract.

(b) **Route handlers through `Path.Operations`** instead of `Files.X(...)`.
`Path.Operations.ReadText/WriteText/CopyTo/Stat/...` already gate
internally. Handlers degenerate to one-liners
(`return await Path.Value!.ReadText();`) and the `IFile` legacy surface
dies. This is what the file-comment on `Path.Operations` line 17
(`The handlers under PLang/App/modules/file/*.cs become thin shells
(one-liner each) on top of this surface`) **already advertises** — but the
wiring stops short.

The architect's intent (per the existing doc-comment) is (b). The coder
implemented neither. Today's state is the worst of both: handlers ARE
gated, but via copy-paste; `Path.Operations` exists but isn't used by its
own advertised consumers; `Files.X(...)` legacy surface lives on. **Pick
(b) and delete `IFile.Read/Save/...`**, or pick (a) and accept the
two-floor architecture. Don't ship both.

Deletion-test: if I delete lines 32–33 of `read.cs`, the file is read
without consent. Yes, those lines matter. But seven identical two-line
preambles, with a public helper waiting to absorb them, is exactly when the
abstraction earns its place — same logic as v1 #1.

### 2.2 `PLang/App/modules/file/{copy,move}.cs:35–37` — bundled-consent UX punted, comment acknowledges

Both `Copy.Run` and `Move.Run` issue **two** prompts on a fresh out-of-root
pair (src Read, then dst Write). The doc-comments (`copy.cs:27–30`,
`move.cs:23–28`) say "Bundled-consent UX is pinned by the C# Path.MoveTo/
CopyTo path and tracked as a follow-up." Good that it's noted, but
`Path.Operations.BundledTransfer` (line 152, the v1 single-prompt path)
already exists and the handler doesn't call it. **Recommendation:** the
action handler for `copy`/`move` should call `source.CopyTo(dest)` /
`source.MoveTo(dest)` on `Path.Operations`, which already does the single
combined prompt. Today's two-prompt UX on the action surface is a
regression vs. what `Path.Operations` already does internally for the same
operation.

### 2.3 `PLang/App/FileSystem/Default/PLangFileSystem.cs:184–188` — empty-body branch

```csharp
if (IsOsRooted(path))
{
    // no-op: leave // prefix intact for idempotency. Permission.Authorize
    // gates these out-of-root accesses; System.IO handles normalisation.
}
else if (IsPlangRooted(path))
{ ... }
```

An empty `if` body with a tail-of-chain `else` reads awkwardly. Two
options that preserve the documented order-matters semantics:

```csharp
if (!IsOsRooted(path))   // OS-rooted: skip both branches, leave path alone
{
    if (IsPlangRooted(path)) { ... }
    else { ... }
}
```

or invert the entire chain. Pick whichever is easier to read. The current
shape works, but an empty-body branch is the kind of thing that gets
"fixed" by a later contributor who doesn't read the comment.

### 2.4 `%__data__% → %!data%` rename (684b95e6b) — well-executed
All 20+ comment/example/literal sites updated consistently. No straggler
found via grep. The rename unifies result-aliasing with the
infrastructure-variable namespace (`!ask`, `!app`, `!data`) — semantically
cleaner. No simplification concern.

---

## Pass 3 — Readability

### 3.1 `PLang/App/modules/file/*.cs` — three using-aliases per file is per-file noise

Each handler imports:

```csharp
using Verb     = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb  = global::App.FileSystem.Permission.Verb.Read;
using WriteVerb = global::App.FileSystem.Permission.Verb.Write;
```

Seven files; up to three aliases each. Plus `using App.Types;`. If the
handlers move to (b) from 2.1, all of these disappear. If they stay with
`AuthGate` promotion (a), consider a single shared aliases file
(`PLang/App/modules/file/GlobalUsings.cs` with `global using Verb = …`)
to centralise. Today's per-file repetition is a smell that the abstraction
sits at the wrong level.

### 3.2 `PLang/App/FileSystem/Default/PLangFileSystem.cs:177–188` — comment is load-bearing
The comment is excellent — it explains why a `// no-op` branch exists.
Don't delete the comment when restructuring per 2.3; carry it into the new
shape.

### 3.3 `PLang/App/Channels/Channel/Stream/this.cs:108` — "the prompt is just lost" branch

```csharp
// No writer at all — proceed to read; the prompt is just lost.
```

When `output` is non-writable and `self.CanWrite == false`, the prompt
text is silently dropped and the reader still blocks for input. This is
the "stdin-only test fixture" path. Acceptable, but consider returning a
`ServiceError("Channel '...' has neither output nor self-write capability",
"AskNoWriter", 400)` instead of silently reading without prompt — a reader
who never sees the prompt has no recourse. Lock the decision down or add a
`ChannelAskCannotWritePrompt` debug log.

---

## Pass 4 — Behavioral reasoning

### 4.1 `PLang/App/FileSystem/Default/PLangFileSystem.cs:227` — Linux case-insensitive comparison — **v1 #4 REGRESSION at sibling site**

```csharp
if (!path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
{
    return path;
}
```

V1 finding #4 fixed exactly this comparison style in `Path.Authorize.IsInRoot`
via OS-aware switch. The same bug now sits at `ValidatePath` line 227.

On Linux, `/srv/myApp/file.txt` and `/SRV/myapp/file.txt` will both
short-circuit through the early return when `RootDirectory = /srv/myApp` —
meaning two different filesystem entries are treated as "same root" for
the purpose of skipping further normalisation. The existing fix in
`Path.Authorize` should be hoisted to a helper:

```csharp
internal static StringComparison RootComparison =>
    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase
                                : StringComparison.Ordinal;
```

placed somewhere both `Path.Authorize.IsUnder` and
`PLangFileSystem.ValidatePath` can reach. Use it at both sites.

### 4.2 `PLang/App/Channels/Channel/Stream/this.cs:97` — null-conditional on Context with no fallback

```csharp
var output = action.Context?.Actor?.Channels.Resolve(global::App.Channels.@this.Output);
if (output != null && output.CanWrite) { ... }
else if (CanWrite) { ... }
```

If `action.Context` is null (in-test direct invocation) we silently fall
through to "self can write". Fine for tests, but if `Context` is null in
production (which would mean an action ran without context — bug condition)
the fallback masks it. Cheap fix: assert `action.Context != null` with a
`Debug.Write` warning, or only allow the null-Context path inside
`#if DEBUG`. Low priority.

### 4.3 `PLang/App/modules/test/run.cs:79–86` — fix-with-comment is good, but no test pins the recursion

The fix is correct (`FreezeFoundational` before user code can register
overlays). **Concern:** the lack of a regression test means a future
contributor who moves the freeze later (e.g., into `RunAsync` because
that "feels more like where wiring happens") will reintroduce the stack
overflow without any gate to stop them. Add a test that registers a
goal-channel as `Input` then triggers `output.ask`, asserting no SO (or
that resolution correctly hits the foundational stdin, not the
goal-channel itself).

### 4.4 `PLang/App/modules/file/copy.cs:35, move.cs:31` — non-atomic two-stage authorize

```csharp
var srcAuth = await Source.Value!.Authorize(new Verb { Read = new ReadVerb() });
if (...) return srcAuth;
var dstAuth = await Destination.Value!.Authorize(new Verb { Write = new WriteVerb() });
```

If the user says **"y" to source** and **"n" to dst**, the source grant
has already been signed-and-stored. Re-running the goal will not re-prompt
for source but will re-prompt for dst. That might be intended (incremental
authorize); it's also surprising vs. the bundled prompt offered by
`Path.Operations.BundledTransfer` which asks once and atomically grants
both. Tied to 2.2: route the handler through `BundledTransfer` and this
irregularity dissolves.

### 4.5 `PLang/App/FileSystem/Permission/Verb/this.cs` — ergonomics for narrow requests
The shape change (verbs null-by-default) is correct. But the call sites
that need "request Read" now read `new Verb { Read = new ReadVerb() }` —
twice-named with the alias trick. For *grants* the static is
`Verb.AllowAll()`. There's no `Verb.ReadOnly()` / `Verb.WriteOnly()` /
`Verb.ReadWrite()` factory. Given the 7 identical
`new Verb { Read = new ReadVerb() }` lines in the action handlers (plus
another 9 in `Path.Operations`), three static factories would let request
sites read `Verb.ReadOnly()` etc. Same shape as `AllowAll()`, zero new
concept. Low priority.

---

## Pass 5 — Deletion test

| Lines | What | Verdict |
|---|---|---|
| `modules/file/*.cs:8–11` `using Verb/ReadVerb/WriteVerb` aliases ×7 | per-file noise | **Delete via shared aliases or handler restructure (2.1)** |
| `modules/file/{copy,move}.cs` second authorize call | non-atomic with bundled path available | **Route through `Path.Operations.BundledTransfer` (2.2, 4.4)** |
| `PLangFileSystem.cs:184–188` empty `if` body | reads as code-removed-but-comment-left | **Restructure per 2.3** |
| `PLangFileSystem.cs:227` `OrdinalIgnoreCase` startswith | Linux false-match same as v1 #4 | **Fix per 4.1** |
| `Stream/this.cs:108` "the prompt is just lost" silent fallthrough | masks misconfigured channels | **Either log or hard-fail per 3.3** |
| `test/run.cs:79–86` `FreezeFoundational` calls | correct fix, no test pins it | **Add regression test per 4.3** |

Lines that would NOT survive a "delete this and a test fails": the
authorize preambles in every file handler (deletion → file written without
consent → permission tests fail). Those lines matter; the question raised
in 2.1 is whether they should live in seven places or one.

---

## Specific tests that would shake out if findings are addressed

- Promoting `AuthGate` to `public` (or routing handlers through
  `Path.Operations`) shouldn't break tests — same gate, same return shape.
  `FileHandlerTests.cs` exercises the gate end-to-end and should remain
  green.
- Adding the `RootComparison` helper and using it in
  `PLangFileSystem.ValidatePath` requires a new "`/srv/myApp` and
  `/SRV/myApp` are distinct on Linux" test, mirroring the `Path.Authorize`
  test the v1 fix added.
- Bundled-consent rewire (2.2/4.4) should not change
  `MoveCopyBundledConsentTests.cs`; it should ADD coverage to `copy.cs`/
  `move.cs` proving the action surface gets the same single-prompt
  behaviour.
- `test/run.cs` regression test (4.3) is new green coverage; nothing to
  remove.

---

## Regression check vs. v1 findings

| v1 finding | Status in v2 commits |
|---|---|
| #1 `Path.Operations` 9-site authorize copy-paste | Fixed in `Path.Operations` (AuthGate exists). **REGRESSED at handler layer** (2.1) |
| #2 `Stat` returns `Dictionary` | Fixed (now `StatInfo` record) |
| #3 `await Task.FromResult` in `PerformTransfer` | Fixed |
| #4 `Path.Authorize.IsInRoot` case-insensitive on Linux | Fixed in `Path.Authorize`. **NEW occurrence at `PLangFileSystem.ValidatePath:227`** (4.1) |
| #5 `AlwaysExpiry` unused | Fixed (deleted + comment) |
| #6–#10 (`_ownCause`, `cause`/`erroredCall` params, dead Cause walks) | Fixed (commits 1af7922ad) |
| Sync-over-async in `Actor.Permission` | Fixed (all Task-returning) |
| Bare `catch` in `VerifySignature` | Fixed (filtered) |
| Unbounded recursion in `Authorize`/`BundledTransfer` | Fixed (loops) |

**Two regressions, both at the same conceptual layer** (authorize
preamble): one is the v1 copy-paste finding re-emerged one floor up; one
is the v1 case-comparison bug at a sibling site. Both small fixes, both
should be in v3.

---

## What the coder did well

- **`Verb` default-null change.** Spotting that JSON round-trip needed
  null-by-default to preserve signature bytes is exactly the kind of
  invariant a reviewer only catches *after* the bug bites. The
  `AllowAll()` static makes "grant everything" still ergonomic. Good
  design taste.
- **Stream EOF fail-fast.** The previous "ReadLineAsync returns null →
  return Ok(string.Empty)" had a loop-forever failure mode (caller
  re-asks → reader returns "" → caller re-asks → …). The new `ChannelEof`
  ServiceError surfaces the misconfiguration at the channel boundary,
  which is the right place.
- **`FreezeFoundational` in `test/run.cs`.** Caught a subtle goal-channel-
  recursion-on-self bug with a one-line fix in the right place. The
  comment explaining the failure mode is load-bearing.
- **`%__data__% → %!data%`.** Sweeping rename done consistently; no
  stragglers; aligns the result-alias with the existing infrastructure
  variable namespace. Cosmetic but cleanly executed.
- **`ValidatePath` consolidation.** Removing the
  `UnauthorizedAccessException`-with-prompt-in-message anti-pattern and
  routing all gating through `Path.Authorize` is the single-owner
  consolidation the architect called for.

---

**Verdict: NEEDS WORK** — fix the handler-layer authorize copy-paste
(2.1, the v1 #1 regression) and the `ValidatePath` Linux case-comparison
(4.1, the v1 #4 regression); the other items are lower-priority follow-ups.
