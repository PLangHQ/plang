# Stage 2a — slicing plan

Stage doc enumerates **10 deliverables**. They are not independent — some surfaces (`Action.Synthetic`, `App.Run` deletion, `ICallback` deletion) cascade across many files. Slicing below picks the order that keeps the tree compilable per commit and keeps callable surfaces alive until their replacements land.

Survey numbers (just measured):

- `App.Run` / `App.RunAction<>` / `cause` parameter — 32 call sites across signing, http, GoalCall, Data.Envelope, Action.this.cs, Variable.cs, plus the soon-deleted callback classes.
- `AskCallback` / `ErrorCallback` / `ICallback` — 30 references outside `App/Callback/` (Data.Envelope, GlobalUsings, App.this.cs, Channels/Channel/this.cs, output/ask.cs, Errors/Error.cs, plus tests).

## Commit-by-commit plan

### 2a.1 — Primitives (no behavior change yet)

Lands the new types alongside the old ones. Old machinery still works; nothing deletes.

1. `App/IExitsGoal.cs` — empty marker interface.
2. `App/Types/Type.cs` — extension `Type.Exit()`.
3. `App/modules/output/ask.cs` — add `public sealed class Ask : IExitsGoal { }` (the payload type; the action class stays).
4. `App/Data/this.Snapshot.cs` (new partial) — `public Snapshot.@this? Snapshot { get; set; }` on `Data.@this`.
5. `App/Data/this.ShouldExit.cs` (new partial / extension) — `ShouldExit()` per spec.
6. `App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — add `public bool Synthetic { get; init; } = true;` + `public Snapshot.@this Snapshot() => Context.App.Snapshot();`.

Flip green: `TypeExitTests` (6), `ActionSyntheticTests #1` (default true), `DataSnapshotTests #1-3` (property + round-trip), `StepLoopShouldExitTests` (5 — pure extension test).

Generator change for `Synthetic=false` on PR actions — do as part of this commit. Single emission point; small.

### 2a.2 — Step-loop short-circuit

7. Wire `result.ShouldExit()` into `Steps.RunAsync`, `Step.RunAsync`, replacing the existing three-way check. Behavior preserved (unhandled-fail + Returned already exit; Exit-type is new but no action returns Exit yet).

Flip green: `StepLoopShouldExitTests #6` (loop short-circuit).

### 2a.3 — Goal.RunFrom + Steps.RunAsync(fromIndex) overload

8. `Step.RunFrom(ctx, fromActionIdx)` on existing `Step/this.cs`.
9. `Goal.RunFrom(ctx, stepIdx, actionIdx)` on `Goal/this.cs`.
10. `Steps.RunAsync(ctx, fromIndex: int)` overload.

Note on a review comment (`757a8eed54` — "dont think we need this" against the `Steps.RunAsync(fromIndex)` overload). The recursive `ResumeChain` needs *something* to "run the rest of this goal after `(stepIdx, actionIdx)`". If I push that into `Goal.RunFrom`'s body inline without a `fromIndex`-aware steps loop, I duplicate the existing loop. Will write the overload as the lowest-friction option but flag for you whether you'd prefer the body inlined into `Goal.RunFrom` to keep the steps surface narrower.

Flip green: `GoalRunFromTests` (5).

### 2a.4 — Channel.Ask / Stream + Message channels

11. Rename `AskCore` → `Ask` on `Channels/Channel/this.cs` (abstract); take `modules.output.ask` directly. Rename `WriteCore` → `Write` likewise.
12. `Channels/Channel/Stream/this.cs` — `Ask` extracts question from action, writes via `Write`, reads stdin (preserve existing timeout logic).
13. `Channels/Channel/Message/this.cs` — `Ask` returns `Data.@this<Ask>("", action.Question.Value)` with `data.Context` + `data.Snapshot = action.Snapshot()`.
14. Rewrite `modules/output/ask.cs:Run()` to ~10 lines (consume sentinel, delegate to `Channel.Ask`). **Old AskCallback construction body deleted at this commit** (callers will be cleaned in 2a.5–2a.6).

Compile-touch: `Channels/Channel/this.cs`'s `FireBefore`/`FireAfter` private helpers reference `Callback.AskCallback`. For this commit, change those signatures from `AskCallback?` to `modules.output.ask?` (the action carries position/variables via Snapshot now). No external callers — `FireBefore`/`FireAfter` are private.

Flip green: `OutputAskRoutingTests` (6).

### 2a.5 — Action owns its execution (drop App.Run / App.RunAction / cause)

Mechanical sweep. Per spec: `Action.RunAsync(ctx)` absorbs current `App.Run` body (Push/Anchor/Execute). `CallStack.Push` reads `action.Synthetic` (no new param). All 32 call sites rewrite `Context.App.RunAction<T>(a, ctx)` → `await a.RunAsync(ctx)`.

Sub-list of touch sites (from grep): `Variables/Variable.cs`, `Data/this.Envelope.cs`, `Goals/Goal/GoalCall.cs`, `Goals/Goal/Methods.cs`, `Goals/Goal/Steps/Step/Actions/Action/this.cs:164` (the existing `RunAsync` body), `modules/error/handle.cs`, `modules/http/code/Default.cs`, `modules/signing/code/Ed25519.cs` (+ ECDSA variant if any), tests under `PLang.Tests/`.

`cause` parameter gets dropped from the chain. Error-recovery sites (`handle.cs`) need to keep "this is recovery-body dispatch" semantics — but `cause` was only used to thread up to the new Call frame for blame. Replacement: keep the Call.Push contract; if a callsite needs to record a cause, it can do so directly on Call.Errors.

Flip green: `ActionRunAsyncTests` (4), `ActionSyntheticTests #3-5` (CallStack.Push reads Synthetic, wire serializer filters synthetic).

### 2a.6 — Snapshot.Resume + callback.run rewrite

15. `App/Snapshot/this.cs` — `Resume(ctx)` body per spec (recursive `ResumeChain`).
16. `App/modules/callback/run.cs` — rewrite to ~10 lines: if `Data.Snapshot == null` error, else `Data.Snapshot.Resume(Context)`.
17. Stream/Message channel resume entry sets `Context.Variables["!ask.answer"] = answer` before invoking `callback.run`.

Flip green: `SnapshotResumeTests` (6), `DataSnapshotTests #4-6` (action.Snapshot helper, exit-data carries Snapshot invariant).

### 2a.7 — Drop dead code

18. Delete `PLang/App/Callback/ICallback.cs`, `AskCallback.cs`, `ErrorCallback.cs`, `Wire/`.
19. `Errors/Error.cs:55` — `Callback` property → `Data<Snapshot>` (or just `Snapshot.@this?`; will pick during the change).
20. `Data/this.Envelope.cs` — drop `ICallback` branches in `EnsureSigned` / expiry resolution. Snapshot-carrying Data still seals; the branch was specifically about the old type marker.
21. `GlobalUsings.cs` line 54 — drop `ICallback` alias.
22. `Channels/Channel/this.cs` — `FireBefore`/`FireAfter` (already touched in 2a.4) — finalize signature.
23. Tests cleanup: delete `PLang.Tests/App/CallbackTests/{AppCallbackConfigTests, AskCallbackTests, ErrorCallbackTests, FailureMatrixTests, ICallbackPositionTests, CallbackRunActionTests}.cs` — these exclusively exercise the deleted types. Verify nothing useful is lost; if a behavior is still needed, port it under the new shape into one of the new test files.

Flip green: full suite green minus the test-designer "Not implemented" stubs that belong to later stages.

### 2a.8 — Cross-goal integration test

24. PLang `.test.goal` end-to-end: `Tests/Callback/StatelessCrossGoalResumes/` already has stubs. Fill them (test-designer left `throw "not implemented"`). This is the acceptance bar for the stage.

## Two specific design questions before I start

**Q1.** Comment `757a8eed54` says "we dont need" the `Steps.RunAsync(ctx, fromIndex)` overload. I read it as "fold the body into `Goal.RunFrom`". I'll do that — keep `Steps.RunAsync` single-arity, write the fromIndex loop directly inside `Goal.RunFrom`. OK?

**Q2.** Comment `db46f862d3` / `712dc5aae0` asked to see a "small snapshot for HTTP" and challenged the top-down capture direction. The architect responded with the wire reference at the bottom of the doc (full vs stateless-ask vs error-resume). For Stage 2a, I read this as: **don't** build per-channel serializers yet — the full Snapshot is captured in-memory, in-process Stream channel never serializes, Message channel serializes the full thing. The "ask-only vs error-only wire shape" is a follow-up (in `todos.md`). OK to defer?

**Q3.** Error.Callback property → `Data<Snapshot>` per architect comment, vs plain `Snapshot.@this?`. `Data<Snapshot>` means the Snapshot is signature-sealed when it leaves the process (which is the right thing for error-resume on the wire). Plain `Snapshot.@this?` is simpler if the error never leaves the process. I lean **Data<Snapshot>** since errors *can* cross the wire (Message channel error-resume). OK?

## What I'll commit per slice

Each numbered slice (2a.1 through 2a.8) is one commit. Build green + targeted tests green per commit. I'll push after each so you can read the diff in flight if anything looks off.
