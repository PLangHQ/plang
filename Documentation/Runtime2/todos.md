# Runtime2 TODOs

> **OBP shape violations** go in their own running collection: `Documentation/Runtime2/obp-cleanup.md` — parked for dedicated passes, not mixed into feature branches.

> **Audited 2026-05-11** (`runtime2-foundation-verify` v1, architect). Every
> entry in this file was checked against the current code. Resolved entries
> are marked inline (`✅ RESOLVED`). Entries still open were re-verified and
> kept as-is. Next reader: trust the inline markers, not the dates.

## 2026-04-24 — cleanup lazy generator, get it to OBP  ✅ RESOLVED 2026-05-01 (`runtime2-generator-obp`) + 2026-05-09 (`runtime2-cleanup`)

`LazyParamsGenerator.cs` is gone. Generator decomposed into `PLang.Generators/this.cs` (entry) → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Code}/this.cs` (polymorphic per-property). The `ResetResolution` patching was replaced by clean per-call backing-field reset emitted at `Emission/Action/this.cs:139` (`__<prop>_backing = default; __<prop>_set = false`). The deeper "request-scoped vs pr-template Data" lifecycle question dissolved with the backing-field shape — Data isn't reused across executions; the backing fields are.

The `[VariableName]` → `Data<app.variables.Variable>` migration was also part of the same arc (see 2026-04-30 entry, marked resolved 2026-05-01).

### Original entry (archived)

Context: `PLang.Generators/LazyParamsGenerator.cs` ballooned with special cases
(full-match/interpolate strings, `As<T>`, `ResetResolution`, default values,
IsNotNull validation, etc). Refactor to align with the OBP (Object-Based Pattern)
— each concern a distinct @this component rather than inlined codegen. Also
revisit the parameter Data lifecycle: the per-execution reset we now emit
(`data.ResetResolution()`) signals that Parameter Data semantics need a cleaner
model (request-scoped Data vs. pr-template Data) rather than reset-patching.

## 2026-04-27 — wire dormant CallStack into the runtime  ✅ RESOLVED on `runtime2-callback` + `runtime2-cleanup` (stage 7)

CallStack is now wired:
- `App.Run` pushes a frame for every action at `PLang/app/this.cs:460` (handles `CallStackOverflowException` outside the try).
- `Goal.Run` pushes a goal-entry frame at `PLang/app/goals/goal/this.cs:288`.
- The source generator captures `__callFrames` from `context.CallStack?.Current?.SnapshotChain()` at `PLang.Generators/Emission/Action/this.cs:154`.
- `!callStack` is registered as `DynamicData` at `PLang/App/Actor/Context/this.cs:168`.
- Depth limits and audit accumulation work (`Tests/App/CallStack/` has 16 `.test.goal` files covering cycle detection, audit, cause links, timeout, recovery).
- `app.Debug.CallStack` was promoted to `app.CallStack` on `runtime2-cleanup` stage 7.

The parallel-execution scope concern (2026-05-08 entry below) is the remaining piece — `_current` is `AsyncLocal` (fork-safe) but `_root` and `Audit` are instance-level. Fine for sequential CLI; revisit when Webserver lands.

### Original entry (archived)

Context: `App/CallStack/this.cs` defines `Push`, `PopAsync`, `PushError`,
`Errors`, `Current`, `GetStackTrace`, etc. — none are called by the runtime.
Verified by `grep -rn 'CallStack.Push\|callStack.Push\|.CallStack.Push'`:
zero hits. So:
- `%!callStack%` resolves to a stack with depth 0 always.
- `%!error.CallFrames%` is always `[]` even when an error has surrounding context.
- `CallStack.Errors` (the run-history of errors) is always empty.

The quick fix for `%!error%` (this session) sidesteps CallStack entirely —
adds a `Context.Error` property that error.handle.Wrap sets/restores around
recovery, and registers `!error` as DynamicData reading from it. That works
for the LlmFixer case but doesn't fix `%!callStack%` or error history.

Proper fix:
1. Push a frame on every action execution (probably `Action.RunAsync`) and
   pop in finally. Honor `IsEnabled` for the per-action overhead toggle —
   when off, only `PushError` should fire (already designed).
2. On error result from `next()`, mutate `Current.Error = result.Error`
   (or call `PushError` if the action wasn't pushed yet).
3. Once the stack actually populates, switch the `!error` DynamicData
   from `Context.Error` to `CallStack.Current?.Error`. Then drop the
   `Context.Error` property — single source of truth on the stack.
4. Add tests: `%!callStack.Depth%` matches actual nesting, `%!error.CallFrames%`
   shows the path that errored, error history accumulates across runs.

Probably surfaces other bugs (Push/Pop balancing in async paths, frame
disposal, snapshot handling) — budget time accordingly.

## 2026-04-27 — PLang tests for error.handle recovery-value path  ✅ RESOLVED 2026-05-11 (`runtime2-foundation-verify` stage 6)

Three `.test.goal` regression pins landed in `Tests/Errors/`:
- `GoalFirstReturnsRecoveryValue.test.goal` pins `handle.cs:109-114` (GoalFirst returns `recoveryResult`, sets `Handled=true`).
- `RetryFirstReturnsRecoveryValue.test.goal` pins `handle.cs:120-131` — the 2026-04-27 symmetry fix.
- `MultiActionRecoveryLastActionPropagates.test.goal` pins `handle.cs:177-184` (chain ordering, RunRecovery returns `last`).

Each test asserts both the recovery side-effect (`%content% equals "from-recovery"`) and `%!error% is null` outside the scope — the latter only holds if the `recoveryResult`-return branch ran with `Handled=true`. The auditor flagged one minor gap (Test 3 cannot distinguish `return last` from `return Ok()` because `variable.set` returns `Ok()` with no `Value`); defer-with-consumer — augment when a downstream surface actually reads the recovery's terminal `Value`.

### Original entry (archived)

Context: codeanalyzer v1 flagged that `error.handle.Wrap` line 109 (RetryFirst
path with recovery) returned `Ok()` while line 96 (GoalFirst) returned
`recoveryResult`. Asymmetric. Now aligned to both return `recoveryResult`.

Need PLang tests to lock in:
1. `ErrorOrder=GoalFirst` + a recovery action that produces a value → assert
   `%step.Result%` (or whatever the next step reads) equals the recovery's value,
   not `null`.
2. Same shape for `ErrorOrder=RetryFirst` (after retry exhausts, recovery value
   should now flow through too). This is the case the symmetry fix unblocks.
3. Recovery with multiple actions where the LAST action is the value-producer —
   confirm the chain's final `last` is what `Wrap` returns.

Without these tests, the asymmetry could re-regress the next time someone
"simplifies" Wrap. Nothing today forces the value path.

## 2026-04-30 — migrate handlers off [VariableName] / raw primitives — RESOLVED 2026-05-01

**Resolved on `runtime2-generator-obp` (architect/v5 → coder/v7 → coder/v8).** Took the typed-payload route rather than the speculative `VarRef<T>` (option 1 below): introduced `app.variables.Variable` (record `Name, RawValue, WasPercentWrapped`) plus `IRawNameResolvable` marker. `[VariableName] partial string` slots become `Data<app.variables.Variable>`; `Data.AsT_Impl` skips its `%var%` substitution branch for `T : IRawNameResolvable` and dispatches to `Variable.Resolve(raw, ctx)` directly. Both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. 22 handlers migrated, `Emission/Property/Legacy/this.cs` deleted, `[VariableName]` attribute and `__Resolve<T>`/`__StripPercent`/`__HasParam`/`RawScalarValidations` removed, `PLNG001` collapsed to a two-rule gate. Coder/v8 added a generator-side pre-`Run()` guard so non-nullable `Data<Variable>` slots surface `MissingRequiredParameter` (closing auditor/v2 finding #1). The original design discussion is preserved below for archival/context.

### Original entry (archived)


Context: Two property-emission paths exist in `PLang.Generators/Emission/Property/`:
- `Data/this.cs` — the Data<T> path. New handlers should use this.
- `Legacy/this.cs` — exists during the migration sweep so handlers still using
  `[VariableName] partial string` and raw `partial int / partial bool / partial string`
  keep building. Phase 5 was meant to delete this file.

Currently ~20 handlers still depend on the legacy path. They live in:
- `PLang/App/modules/list/` (add, any, contains, count, first, flatten, get, group,
  indexof, join, last, range, remove, reverse, set, sort, split, unique)
- `PLang/App/modules/loop/` (foreach)
- `PLang/App/modules/variable/` (clear, exists, get, remove, set)

Until this migrates, the auditor/v1 finding #1 fix is asymmetric: the Data<T>
emission and the Legacy emission both honor the cycle/depth `ServiceError`
contract, but only because Legacy still exists to honor it. Phase 5 still
needs to delete Legacy and the build-time `PLNG001` diagnostic fully takes
over.

### The design problem (open)

`[VariableName]` is **not** plain legacy — it's a distinct semantic. For
`list/get %products% 0`, the handler needs the *literal name* `"products"` to
call `Context.Variables.Get(ListName)` and `Variables.Set(ListName, ...)` on
the write side. If you wrap that property in `Data<string>`, As<T> resolution
walks `%products%` and hands back the *value* — the name is gone. So the
migration is not mechanical.

Options to consider when this branch opens:

1. **A new wrapper** like `VarRef<T>` that exposes both `.Name` (literal) and
   `.Value` (resolved). Replaces `[VariableName] partial string ListName` with
   `partial VarRef<List<object?>> List`. The handler does `Context.Variables.
   Get(List.Name)` for reads and `.Set(List.Name, ...)` for writes.
2. **Carry the name on `Data<T>`** itself. `Data` already has a `Name` field.
   So `partial Data<List<object?>> List` could expose `List.Name` directly,
   no new wrapper needed. Risk: confuses `Data.Name` (the variable's name)
   with the parameter name in the handler.
3. **Keep an attribute under a non-"legacy" name.** Rename `[VariableName]` to
   something like `[Literal]` and keep the special-case emission path. Smallest
   migration cost, but keeps two emission shapes forever — defeats the goal.

Recommendation for the next branch's architect pass: probably (1) — `VarRef<T>`
as a first-class wrapper. The `Data` carrying a name is already overloaded
(it's the variable's name, the parameter's name in some contexts, the .pr
parameter name in others). A separate type makes the semantics explicit.

### Scope

- Design the replacement (architect pass).
- Migrate ~20 handlers to the new shape.
- Delete `PLang.Generators/Emission/Property/Legacy/this.cs`.
- Delete `[VariableName]` attribute and all references.
- Remove `__Resolve<T>`, `__StripPercent`, `__HasParam`, `RawScalarValidations`
  from `PLang.Generators/Emission/Action/this.cs:250-295` and surrounding hooks.
- Remove the `if (__resolutionError != null) return __resolutionError;`
  pre-Run check at line 232 — once Legacy is gone, `__resolutionError` is only
  populated during Run (by Data<T> getters), so the pre-Run check can never
  trip. The post-Run check (added in coder/v6 to close auditor/v1 finding #1)
  is what catches Data<T> resolution failures and stays.

### Why not on `runtime2-generator-obp`

This branch (the one closing auditor/v1) is in landing shape — codeanalyzer/
tester/security/auditor all approved, and the fix for finding #1 is small and
contained. Bolting the [VariableName] migration on top means a new design
pass plus ~20 handler conversions plus a full re-review cycle — too much
churn on a shipping branch. The migration deserves its own architect pass
on a fresh branch.

## 2026-05-02 — Callback (context for callstack design)

Captured during the callstack architect pass. Callback is its own future
branch — this entry exists only so the design constraints it imposes on
callstack are on record.

### Concept

Callback is **state-machine restoration** for stateless flow + durable
execution. An action like:

    - ask user 'What is your name?', vars: %orderId%, write to %name%

returns a `Callback` object instead of blocking. The callback carries
`(goal, step, action, declared-vars, signature)` and is sent over the wire
(e.g., hidden form field, encrypted). When the user submits, the server
verifies the signature, hydrates the declared vars, dispatches at the
resume point with `%name%` set from form data, and continues from there.

The original run's process state is gone by the time the callback returns.
Resume is a fresh execution rooted at the resume point — not a thread/fiber
unblock.

### Settled design points

- **Vars are developer-declared**, not auto-snapshotted. Syntax: `vars: %orderId%`
  on the action. Practical reason: serializing a 1000-row %products% list
  into a hidden form is not viable. The developer carries IDs and re-queries
  the rest.
- **Vars are encrypted on the wire** so the user can't inspect or tamper with
  carried state.
- **Sign `(goal_hash, step, action, vars, expiry)`** — goal_hash means a
  rebuilt goal invalidates outstanding callbacks. Correct security posture:
  if the developer redeploys, in-flight callbacks fail validation rather
  than running against changed code.
- **Errors carry a callback** for "retry from here" — this is the durable
  execution payoff.

### What this means for callstack design (the only thing relevant now)

- **Frame stays OBP.** Frame holds the live `Action` reference; serializers
  for callbacks traverse the OBP graph at serialization time to extract
  `(goal-name, step-index, action-index)`. Do **not** denormalize stable
  IDs onto Frame.
- **Variable snapshot is not a callstack concern.** Callback issues a
  declared-var snapshot via its own mechanism; CallStack neither captures
  nor restores variables for resume.
- **No `Data.Pause` lane needed in callstack work.** Callback issuance is a
  callback-module concern (action returns a Callback as its Data value).
  Doesn't change Ok/Fail dichotomy.

### Open question for Callback's own design pass

When a callback resumes, does the new run's callstack carry a `Cause` link
back to the issuing run's identifier (cross-process causal trace), or is
resume a clean break? Decide on the Callback branch.

## 2026-05-05 — `crypto.encrypt` / `crypto.decrypt` real implementation

The `runtime2-callback` branch ships these two actions as identity
pass-through: input bytes are returned unchanged. The wiring is real
(Callback's `Serialize`/`Deserialize` calls through them; the Channels
Data layer signs the resulting bytes), so when real crypto lands, only
the action handler bodies change.

**Design target:** symmetric AES-256-GCM keyed by the existing
`IKeyProvider`. Both actions take `byte[]` and return `byte[]`.

**Gating:** named the missing PLang runtime features when picking this up
— briefly noted by Ingi as "we have some missing feature in the plang
runtime." Confirm what those are before starting.

**Migration:** none needed. Pass-through callbacks issued under v1 will
not decrypt under real keys, but nothing has shipped to users yet.

## 2026-05-05 — replace `App._statics` with goal-backed dynamic property  ⚠️ PARTIALLY RESOLVED — shape carved, deep replacement still pending

Shape carve done: the field is now its own `@this` at `PLang/App/Statics/this.cs` (separate type, OBP-shaped, snapshot-aware). The flat module-keyed `ConcurrentDictionary<string, ConcurrentDictionary<string, object?>>` is no longer inline on `App.@this`.

**Still open:** the deeper design target — "goal-backed dynamic property, addressable by dot-path" — is not implemented. `App/Statics/this.cs` still exposes `GetBag(key)` returning a flat `ConcurrentDictionary<string, object?>` and carries the same `TODO: replace with goal-backed dynamic property` comment. Callback's snapshot fidelity still depends on this bag structure. Pick up when there's a real use case driving the dynamic-property design (probably the callback ratification sweep or app.X.Y dot-path work).

### Original entry (archived)

`PLang/App/this.cs:108` carries a private
`ConcurrentDictionary<string, ConcurrentDictionary<string, object?>>`
keyed by module name, exposed through `GetStatic(key)`. Inline TODO at the
declaration says "Replace with goal-backed dynamic property" — that
replacement hasn't been written down anywhere as a real follow-up, so
this entry pins it.

**Why it matters now:** the callback design captures `App._statics` as
part of `app.Snapshot()` (snapshot-and-restore bucket, see
`plan/snapshotted-system.md`). The capture is *provisional* — once the
goal-backed dynamic property mechanism lands, statics live there and the
explicit `_statics` snapshot subtree drops out of `ErrorCallback`'s wire
shape. Callback code shouldn't depend on the field name.

**Design target:** dynamic properties scoped to a goal (or app-wide,
addressed via `app.X.Y` like the rest of the config tree) replacing the
flat module-keyed dict. OBP-shaped, addressable by dot-path, snapshots
naturally as a property of whatever `@this` owns it.

**Out of scope here:** the actual implementation. This entry exists so
the callback work doesn't bake the `_statics` field name into anything
load-bearing.

## 2026-05-05 — Snapshot envelope ratification (was: runtime2-callback close-out)

> **Rewritten 2026-05-24.** The runtime2-callback work was absorbed into
> Snapshot — `app/snapshot/` now owns Resume; the standalone Callback
> module shrank to `modules/callback/run.cs`; `AskCallback`,
> `RestoredFrame`, and `ErrorCallback` types are gone. Most of the
> original close-out items were resolved by that merge. What's listed
> below is what survived the merge, verified against source on
> 2026-05-24.

### Live items — envelope cluster

The lazy-signing + wire-format carve-outs introduced on `runtime2-callback`
still ship as-is on `Data`/`PlangDataSerializer`. No architect has blessed
or carved a redesign. Sweep these together in one pass:

1. **Lazy `Data.Signature` + `RawSignature` + sync-over-async `EnsureSigned`.**
   `app/data/this.Envelope.cs:39, 58, 66` and `app/channels/serializers/serializer/plang/Data.cs:43, 48, 87, 92`.
   The carve-out: `Signature` is not auto-populated on read; callers must
   call `EnsureSigned()` explicitly (which goes sync-over-async into the
   signer), and the serializer uses `RawSignature` to peek without
   triggering populate. Decide whether the lazy shape is right or whether
   signing should be eager / async all the way through.
2. **`PlangDataSerializer` JSON `{type, value, signature}` wire format.**
   `app/channels/serializers/serializer/plang/Data.cs`. Decide whether to
   pin a real wire format (CBOR, length-prefixed, etc.) or formally
   accept JSON.

### Live item — builder

3. **`os/system/builder/.build/buildgoal.pr` hand-edit.** Coder nulled
   `Actor: %subGoal%` and `KeyName: subGoal` on the foreach BuildSubGoal
   step because `plang build` was broken on a fresh app. Decide whether
   that's the right fix or whether the builder LLM prompt needs work.
   Independent of the snapshot merge; verify still present and still
   needed on next builder pass.

### Resolved by the snapshot merge

- ~~`AskCallback.Answer` init-only field~~ — type gone. The remaining
  `%!ask.answer%` sentinel question is tracked under the standalone
  `2026-05-20 — Replace !ask.answer sentinel` entry.
- ~~`RestoredFrame` surrogate~~ — gone, no grep hits.
- ~~`Errors.Push` setting `error.App` for `Error.Callback`~~ — `ErrorCallback`
  type is gone (only a stale comment at `app/errors/Error.cs:41`); Snapshot
  owns materialisation.
- ~~`ErrorCallback.Serialize` narrow wire shape~~ — same; Snapshot's wire
  shape supersedes. Per-channel section-filtering is tracked under the
  standalone `2026-05-20 — Per-channel serializer for stateless suspend`
  entry.
- ~~Stale `Tests/Callback/*` stubs (AskVarsOnNonAsk, CallbackTimeoutSetting,
  DurabilityRoundTrip, TamperedSignature)~~ — `Tests/Callback/` still
  exists (`AskVarsResumeBindsValue`, `ErrorCallbackOutsideHandler`,
  `ErrorCallbackSurface`, `InProcessResume`, `RunCallbackVerb`,
  `StatefulAskMidGoalBindsValue`, `StatelessCrossGoalResumes`) but
  the four named stubs are not there. Treat as resolved-by-rehoming;
  confirm during the next test triage that nothing under
  `Tests/Callback/` is stale post-merge, and consider relocating the
  folder to `Tests/Snapshot/`.
- ~~HTTP wire transport "for ask-user"~~ — re-scoped as a **Snapshot**
  transport question; folded into the standalone
  `2026-05-11 — End-to-end PLang tests for full-app Snapshot save+restore round-trip`
  entry, which already pins it to the same work.
- ~~Builder revalidation after `buildgoal.pr` hand-edit~~ — superseded by
  builder churn since; not worth re-tracking separately.

## 2026-05-06 — mobile signed code via channels (`actions.run`)

Context: PLang's primitives — channels + signing + identity + actor-scoped privilege — combine to enable signed mobile code. A server can ship a function (an actions list as Data) through a channel to a client. The client verifies the signature against the server's known identity, then runs the actions in a level-0 sandbox via a new `actions.run %actions% level: 0` action.

What this unlocks: form validators, custom queries, plug-ins, browser-extension-style scripts — all as signed code over channels. Trust is cryptographic, isolation is structural. Other languages need signed manifests + custom sandbox + RPC + protocol design. PLang has every piece; just needs the action.

What to add when this lands:
- `actions.run` action in a new module (or under `system`), takes `actions: List<Data>` and `level: int` (defaults to current actor's level; 0 for sandbox).
- Privilege gate that consults `Context.Actor.EscalationLevel` (need to re-introduce EscalationLevel — see runtime2-channels plan, removed there as dead code with note to bring back inverted: `system=0`, `user=1`, `untrusted=100+`).
- Channel-aware Data-as-actions packaging — probably a Mime type like `application/x-plang-actions`.

Defer until there's a real consumer driving the shape (a use case more concrete than "imagine if").

## 2026-05-06 — migrate `ExpiresInMs` to ISO 8601 duration  ✅ RESOLVED 2026-05-08 (runtime2-cleanup stage 14)

Migrated. `Callback.Signature.@this.ExpiresInMs` (int?) →
`.Expires` (TimeSpan?). Same on `App.modules.signing.sign`'s action
property. JSON wire form is ISO 8601 (`"PT5M"`, `"PT30S"`) via the
existing global `TimeSpanIso8601Converter`.

Other `*Ms` properties (`CacheSettings.DurationMs`, `RetryOverMs` in
`error/handle`) have the same shape smell but were out of scope for
stage 14 — flagged for future stages.

## 2026-05-07 — fork-site Variables isolation beyond parameters

Context: codeanalyzer v1 on `runtime2-channels` flagged that
`GoalChannel.InvokeGoal` raced on `%!data%` because parameter binding
mutated actor-shared `Variables`. Coder v5 fixed it by adding
`Variables.Calls` — an AsyncLocal frame pushed at `GoalChannel.WriteAsync`,
so each concurrent write sees its own `!data` slot.

The frame currently isolates **only parameter resolution**. Goal-body
`set %x% = ...` inside the called goal still writes to actor-shared
`Variables`. That's intentional for sequential calls (`LoadUser` writes
`%user%`, parent reads it — a feature) but means concurrent fork-site
invocations still race on goal-body sets:

```
ChatGoal:
- set %lastMessage% = %!data.message%       # races across concurrent writers
- write out %lastMessage%
```

When the runtime grows other concurrency boundaries (parallel foreach,
`call X, dont wait`), revisit:
- Should fork-site frames also intercept Set, isolating the entire branch?
- Or stay parameter-only, with users responsible for not racing on actor
  state inside a forked branch?
- For goal channels specifically, fanout-via-write to actor state is
  often the *intent* (e.g. accumulating chat history). So full isolation
  is probably wrong for channels but right for parallel foreach.

Probably the answer is per-fork-site policy: `Variables.Calls.Push` for
parameter-only isolation (current), and a separate
`Variables.Branches.Push` (or similar) for full read+write isolation
when forking parallel branches. Designed when the parallel foreach
work lands.

---

## 2026-05-07 — `OpenAiProvider._requestCount` static counter is a temporary blocker  ✅ RESOLVED 2026-05-08 (runtime2-cleanup stage 16)

Field, cap const, and increment-and-throw block deleted per Ingi's
2026-05-07 call. No replacement. If rate-limiting becomes a real
need, design properly when the requirement is clear.

## 2026-05-08 — Events three-tier scoping needs a design pass

Context: `Channel.@this.MatchingBindings` (Channels/Channel/this.cs:170-192) checks
event bindings at three tiers when an event fires:

1. **Per-channel** — `Channel.Events` (one Events.@this per Channel instance).
2. **Per-actor** — `Actor.Context.Events`. This is where PLang `event.on` and
   related modules write today (`event/on.cs:65`, `mock/action.cs:73`,
   `test/run.cs:120`).
3. **App-level** — `App.Events`. The reader infrastructure exists but **no writer
   path exists today**. The intent (per the inline comment) is "match across
   actors so one binding can cover every channel-of-name 'logger' regardless
   of which actor owns it" — useful for cross-actor hooks, but no PLang
   construct currently produces these.

The three-tier design is intentional and stays as-is for now (Ingi 2026-05-08).
What's missing: the writer side for the app-level tier, and a clearer mental
model for when each tier is the right scope.

When you pick this up:
1. Decide whether cross-actor events are an actual need (driven by a use case),
   or whether the app-level tier should be removed.
2. If kept, build the writer path — what PLang construct registers an
   app-level binding? `- add app-wide before /admin call Authenticate`?
   Or some other surface.
3. Document the three tiers and when each applies, so users (and module
   authors) can reason about scope.
4. Watch for interaction with the per-context mode flags direction
   (Build/Debug/Testing as per-context) — Events scoping may want to
   align with that conversation.

Filed alongside the scope-map (`.bot/runtime2-cleanup/architect/plan/scope-map.md`)
which records the current state and Ingi's "keep as-is, file todo" call.

### Addendum 2026-05-09 — structural shape under `Events/` to revisit in the same pass

Surfaced when carving the runtime2-cleanup Tier 5 stage that was originally
going to "drop the Lifecycle layer and make Before/After properties on
Events.@this." That collapse was based on a misread of the structure:

- `Events.@this` (per-actor) is the **registry** — flat `List<EventBinding>` with
  Register/Unregister/Save/Restore/GetBindings/GetMatchingBindings/Count.
- `Lifecycle.@this` is a **per-target view** — Before+After pair, built lazily
  by `Actor/Context/this.cs:370–431` (`LifecycleFor(goal/step/action)`),
  cached per-context.
- `Bindings.@this` is the inner per-phase collection with the `Run(...)` dispatcher.
- `Binding.@this` is the individual binding record.

So Lifecycle and Events are different *scopes*, not redundant nesting — the
planned "Before/After become properties on Events.@this" can't be right
without conflating per-actor with per-target.

There may still be cleanup worth doing here, but it's tangled with the
three-tier scoping decision above:

1. **Lift `Bindings/` and `Binding/` out from under `Lifecycle/`.** The deepest
   namespace path today is `app.events.lifecycle.bindings.binding.@this`
   (the `EventBinding` alias). `bindings` and `binding` aren't conceptually
   under `lifecycle` — Lifecycle is one *use* of them. Lifting them up would
   shorten to `app.events.binding.@this`.
2. **Move Lifecycle out of Events/ entirely.** It's created by Context, not
   by Events itself. A natural home is `Actor/Context/Lifecycle/this.cs` —
   closer to where it's actually instantiated. Bigger import-surface change.
3. **Rule B smell on Events.@this.** `GetBindings(EventType)` and
   `GetMatchingBindings(...)` return `IReadOnlyList<EventBinding>` — the
   collection-as-method-call shape. Probably wants a typed sub-property or a
   navigated query, but that's a redesign of the registry surface, not a
   pure relocation.

Pulled from runtime2-cleanup Tier 5 stages — keeping the structural shape
question with the three-tier scoping decision so they get the same design pass.
The shape choice may follow naturally once tier scoping is settled (per-context
Lifecycle ⇄ Context-owned Lifecycle folder, etc).

## 2026-05-08 — CallStack scope: shared on App.Debug is wrong for parallel execution

Context: `app/modules/debug/this.cs:101` allocates a single `app.callstack.@this()` per app
and exposes it as `app.Debug.CallStack`. Comment in `app/actor/context/this.cs:44-47`
explains the move ("moved there from per-context ownership so it's a single tree
per run, fork-safe via AsyncLocal"). The reasoning was sequential CLI execution.

The problem: under parallel execution (web-pool, multiple Contexts in flight),
the shared CallStack interleaves pushes from concurrent flows:

- `_current` is `AsyncLocal<Call.@this?>` — correctly flow-scoped per flow ✓
- `_root` (the first call pushed, line 60) — shared
- `Audit` collection (line 43) — shared
- Tree structure — different request flows interleave into one tree
- `%!callStack%` from one request may show frames from another concurrent
  request (depending on how traversal navigates)
- Audit mixes traces from all parallel flows

For sequential CLI (`plang start`), this is fine — one flow at a time.
For parallel web-pool execution, the shared scope is broken.

When you pick this up:
1. Grep readers of `app.Debug.CallStack` to understand who depends on the
   shared tree (cross-context error reporting? cross-actor stack traces?).
2. Decide between:
   - **Per-context** — each Context has its own CallStack.@this. Simplest;
     sequential CLI behaves identically; parallel web-pool gets isolated
     per-request trees.
   - **Split config from state** — config (Flags, MaxDepth) stays shared on
     App; tree state (`_current`, `_root`, `Audit`) moves per-Context.
     Cleaner OBP if config really needs cross-context overrides.
3. Update stage 7 of the runtime2-cleanup plan if still pending — its
   current scope ("promote `app.Debug.CallStack` to `app.CallStack`")
   maintains the wrong shared scope.

**Not touched in the runtime2-cleanup branch** (Ingi 2026-05-08). Stage 7
stays as a property-promotion only.

## 2026-05-11 — Settings encryption-at-rest decision before secrets-bearing modules port

Surfaced during the foundation verification sweep (`.bot/runtime2-foundation-verify/architect/v1/verification.md`).

Context: `PLang/App/Settings/Sqlite.cs` is plain SQLite — values stored as-is. Settings will hold LLM API keys, HTTP credentials, signing keys (once they aren't on disk separately), and webserver session secrets when those modules port from main. Whether encryption-at-rest is the Settings layer's job (transparent to callers) or the caller's job (`crypto.encrypt` the value before `settings.write`) is not decided.

**Pick up when:** before the first secrets-bearing module ports — likely Webserver, LLM provider config, or Db connection strings. Cheap to design pre-port; expensive to retrofit once dozens of secrets are written under one model and need migrating to another.

**Design questions for that pass:**
1. Layer responsibility: transparent encryption in `Sqlite.cs` keyed by `IKey`, vs. caller-side `crypto.encrypt` per value.
2. Key rotation: how does a re-keyed identity migrate existing encrypted values?
3. Migration story: how does an in-flight settings DB transition between "plain" and "encrypted" modes?
4. PLang surface: does `settings.write key=X value=Y` flag encryption per-call, or by namespace?

## 2026-05-11 — End-to-end PLang tests for full-app Snapshot save+restore round-trip

Surfaced during the foundation verification sweep (`.bot/runtime2-foundation-verify/architect/v1/verification.md`).

Context: All 7 snapshot subsystems have C# TUnit round-trip tests. No `Tests/Snapshot/*.test.goal` exists that pauses an app, dehydrates state, rehydrates a fresh app, and asserts behaviour continues. The single `.test.goal` covering snapshots (`Tests/TestModule/Assert/TestAssertFailureSnapshotsVariables.test.goal`) tests assertion-time capture, not full-app pause/resume.

**Pick up when:** the ask-user HTTP wire transport lands (2026-05-05 callback todo, item 14). That's the first real consumer of full-app pause/resume — ask-user's resume flow IS the snapshot round-trip in production. Couple the tests to that work; designing them in isolation now would over-specify a shape the transport will refine.

**No standalone branch needed.** Folds naturally into the ask-user transport architect pass.

---

## 2026-05-15 — Typed-value pass: targeted `Dictionary` → `List<Data>` / tightening

> **Rewritten 2026-05-24** after a real source survey (137 hits, 46 files).
> The original framing ("migrate across codebase") was wrong: most
> `Dictionary<string, object?>` usage is load-bearing (JSON deserialization,
> Variables backing store, OpenAI HTTP body, settings/statics registries,
> builder catalog type-guards). Real scope is four small clusters and a
> shared navigator prereq.

### Pre-work — navigator branch for `List<Data>` by `.Name`

`app/variables/navigators/` has no branch today that walks
`IEnumerable<Data>` and resolves a path segment to the matching `.Name`.
`List.cs` is `Data`-aware only to avoid double-wrapping list elements;
nothing handles "find Data in list by name." Add this branch first
(probably a sibling to `Dictionary.cs`, e.g. `DataList.cs`) and pin it
with a navigator-level test. Every cluster below depends on this; no
cluster ships PLang-side resolution without it.

### Cluster C — `Error.Details` + `AssertionError.Variables` (ship first)

Smallest, lowest risk, proves the construction + navigator shape end-to-end.

- Fields: `app/errors/Error.cs:28 Details`, `app/errors/AssertionError.cs:21 Variables`.
- Construction sites: `app/data/JsonString.cs:83`, `app/modules/llm/code/OpenAi.cs:242`, `:333`.
- Reader: `app/errors/Error.cs:215-222` (display only; no `.Details["X"]` indexer readers).
- PLang-side: enables `%!error.Details.KEY%` for the first time (no test
  exists today — add one to lock the navigator).
- `AssertionError.Variables` is a Variables-snapshot dict; converting
  means `Variables.Snapshot()` either grows a `SnapshotAsData()` sibling
  or the assertion site converts at capture time. Pick whichever keeps
  `Variables` itself dict-internal (don't ripple into Variables core).

### Cluster A — Mock parameter capture

Semantically a parameter list; the dict shape erases types needed for
replay matching.

- `app/modules/mock/action.cs:17` `Parameters`, `:93 CaptureParameters`,
  `:107` matchers.
- `app/modules/mock/types.cs:15 RecordCall(parameters)`, `:27 Parameters`.
- Tests under `Tests/Mock/` need a pass once the capture/match shape changes.

### Cluster B — Schema / Action catalog rendering

Removes hand-built nested-dict literals in the builder catalog path.

- `app/modules/Schema/Spec/Action.cs:18 Params`.
- `app/modules/Schema/Render.cs:123-137` (`BuildActionRecord` + nested
  param dicts).
- **Risk:** the rendered JSON is LLM-facing — the wire shape that goes
  into builder prompts must stay byte-stable, or the builder's behavior
  shifts. Internal model becomes `List<Data>`; the JSON serializer for
  catalog output keeps emitting the existing object shape. Verify with
  a builder regression run after the change.

### Cluster D — HTTP / Signing Headers (separate motivation)

Not a `List<Data>` migration — values are always strings. The right
fix is type-tightening to `Dictionary<string, string>` (or
`IReadOnlyList<KeyValuePair<string, string>>` if multi-value matters
for HTTP). Bundled into this plan because it surfaces in the same
grep and reads as "the Dictionary problem" from a distance.

- `app/modules/signing/{sign,verify,Signature}.cs` — `Headers` fields.
- `app/modules/http/{request,upload,download,configure,Config}.cs` —
  `Headers` / `DefaultHeaders` fields.
- `app/modules/http/code/Default.cs:385, 407, 412` — construction and
  resolution sites.
- Decide multi-value question first (HTTP allows repeated headers; if
  PLang doesn't need that, `Dictionary<string, string>` is fine).

### Explicit non-targets (verified boundary, keep as dict)

- `app/data/this.cs` JSON deserialize + `WalkDict` — the dict-walker IS
  the path-navigator's canonical engine.
- `app/variables/this.cs` `ToDictionary()` / `Snapshot()` — Variables
  backing store; navigator depends on it.
- `app/modules/llm/code/OpenAi.cs` request body / tool args — OpenAI HTTP
  wire format.
- `app/Statics/this.cs`, `app/config/Scope.cs`, `app/snapshot/this.cs` —
  registries with genuine arbitrary keys.
- `app/modules/variable/set.cs:133` — PLang user setting a variable to a
  parsed-JSON dict value; that's the value model.
- `app/modules/builder/code/Default.cs` / `identity/code/Default.cs` —
  type-guards walking parsed JSON/YAML results.
- `app/modules/Schema/Render.cs:159 Fluid`, `list/group.cs:31` — transient
  local dicts for template rendering / grouping output.

### Sequencing

1. **Navigator branch** (pre-work) — adds `IEnumerable<Data>`-by-Name
   resolution, with a navigator-level test. Lands alone; nothing else
   ships without this.
2. **Cluster C** (Errors) — smallest, proves end-to-end, adds the first
   PLang test for `%!error.Details.KEY%`.
3. **Cluster A** (Mock) — same construction shape as C, low risk.
4. **Cluster B** (Schema) — gated on byte-stable LLM-facing JSON;
   needs a builder regression run.
5. **Cluster D** (Headers) — independent type-tightening; ship anytime
   after the others (or first, since it doesn't depend on the navigator).

### Branching

Cross-cutting cleanup, not a single module-action. Pattern: open one
branch per cluster (e.g. `dict-to-data/errors`, `dict-to-data/mock`,
`dict-to-data/schema`, `headers-tightening`), all rooted off main.
The navigator pre-work either lands first on its own branch or piggybacks
on Cluster C and is reviewed as the first stage of that branch.

### Exit criteria

- The four clusters above are migrated or explicitly closed as
  not-worth-it after attempt.
- `grep -rn "Dictionary<string, object" PLang/` returns only items in
  the "Explicit non-targets" list above (or new ones added with a
  comment explaining why they stay dict).
- The navigator branch has a test covering `%foo.X%` where `foo` is a
  `List<Data>` with a `Data { Name = "X" }`.
- One `.test.goal` regression pins `%!error.Details.KEY%`.

## 2026-05-20 — Replace `!ask.answer` sentinel with explicit Answer parameter pattern

Context (from `filesystem-permission` branch, stage 2a design): the resume
path for `output.ask` works by the channel setting `!ask.answer` into
`Context.Variables` before invoking the resume action; `output.ask`'s body
checks for that sentinel and short-circuits to the answer instead of
issuing a fresh ask. The mechanism works but `!ask.answer` smells —
state passed through a variable namespace instead of an explicit parameter.

When `output.ask` grows structured options (separate TODO), revisit this:
the resumed action should declare an `Answer` input parameter (nullable);
the resume entry binds it explicitly from the wire payload; the action
checks `if (Answer != null) return Answer;`. No sentinel needed.

## 2026-05-20 — Add structured options to `output.ask`

Context (from `filesystem-permission` branch, stage 2b design): today
`output.ask` only takes a free-text question. Permission's `Path.Authorize`
has to format the consent question as a string, then reconstruct the
`Permission` record on the answer side via `BuildRequest` — the same
data is built twice (once for the prompt, once for storage).

When `output.ask` grows structured options (e.g., the action carries the
Permission record alongside the question, and the channel renders the
options based on the record's shape), refactor `Path.Authorize`:
1. Define the `Permission` record once.
2. Hand it to `output.ask` as the structured option.
3. User signs over the actual definition.
4. Store the signed `Permission` directly — no `BuildRequest` reconstruction.

Drop the `BuildRequest`/`SignAndStore` helpers when this lands.

## 2026-05-20 — Per-channel serializer for stateless suspend (error vs ask vs ...)

Context (from `filesystem-permission` branch, stage 2a design discussion):
Snapshot is now the unified suspend/resume currency. Each channel kind
owns its serializer — stateful (Stream) doesn't serialize at all (in-process
resume); stateless (Message/HTTP and any future kind) does.

The minimal wire for an ask-resume needs only CallStack + Variables. The
minimal wire for an error-resume needs CallStack + Variables + the Errors
trail (the caller needs failure detail to decide retry behaviour). Other
stateless kinds may need different sections.

Open: how to express section-filtering on the serializer side. Options:
(a) one configurable Plang Data serializer with an allowlist set per
channel kind; (b) distinct serializer classes per resume kind registered
under different MIME types; (c) each subsystem's Capture takes a "minimal"
hint and writes less. (a) and (b) feel cleaner than (c).

Resolve when the Message/HTTP channel actually ships.

## 2026-05-20 — Relocate App.Snapshot() orchestration to Snapshot.@this.Capture(ctx)

Context (from `filesystem-permission` branch, stage 2a OBP discussion):
The orchestration that walks subsystems and builds a Snapshot currently
lives on `App.Snapshot()` (`PLang/App/this.Snapshot.cs:16-27`). OBP-wise,
App shouldn't know how a Snapshot is built — the Snapshot type should.

Refactor: move the body to `Snapshot.@this.Capture(Actor.Context.@this ctx)`
as a static factory. Delete `App.Snapshot()`. Update callers (today only
ErrorCallback constructor + tests). Action's `Snapshot()` helper (stage 2a
deliverable #3) then calls `Snapshot.@this.Capture(Context)` directly.

Pure refactor — no behaviour change. Out of scope for the
filesystem-permission branch (stage 2a uses the existing entry); land
separately to keep stage 2a focused.

## 2026-05-20 — Revisit Snapshot.ResumeChain shape

Context (from `filesystem-permission` branch, stage 2a):
`Snapshot.Resume` walks the captured frame chain recursively via
`ResumeChain(chain, idx, ctx)` — outermost-to-innermost on the way down
(pushes each parent frame back onto the live CallStack), bottom frame
runs `Goal.RunFrom(stepIdx, actionIdx)`, then unwind continues each
parent at `ActionIndex + 1`.

Works, but it's clunky:
- Explicit recursive helper alongside the natural call-stack semantics.
- Two RunFrom calls per parent (one for the in-flight step's remaining
  actions if any, one when the call returns) — kind of.
- Push-without-execute on parent frames feels off; the call frame is
  faking "in flight" state.

There's probably a cleaner shape — possibly something where Restore
itself does the pushing in the right order, and a single `RunFrom`
walks naturally because the call stack is set up. Or some way the
goal-call action knows how to continue from "I'm mid-call, my sub-goal
just returned with X."

Revisit when stage 2a's coder gets to the implementation — the recursive
shape may obviously be the wrong abstraction in code form even though
it works on paper.

## 2026-05-29 — audio transcription module: review real-time voice-agent reference

When we build a module that can transcribe audio, check out
https://nemorize.com/roadmaps/building-real-time-voice-agents-from-scratch
as a reference for the design (real-time voice agent architecture).

## 2026-05-30 — lazy JSON parse on file read (don't parse at read time)

Context (from `type-kind-strict` branch, architect/Ingi). `FilePath.ReadText`
(`PLang/app/type/path/file/this.Operations.cs:61`) currently parses eagerly:
for a non-string CLR target (`.json` → dict, etc.) it runs
`TryConvertTo(text, clr)` at read time, so reading a `.json` file always
deserializes to a dict even when the caller only wanted the raw text.

Change: read the file as text and stamp the type from the extension
(`{name: object, kind: json}` for `.json`), but keep the value as the raw
string and only parse to the JSON object on **first access** (lazy). The
mechanism already exists — `Data.SetValue(Func<object?>)` for the deferred
factory and `Data.ConvertValue()` ("if value is a string and Type knows the
conversion, convert once on first navigation"). Route the read through those
instead of converting inline.

Why: matches the settled type/kind decision — `text` means "the value is a
string"; JSON stays `object` with kind `json`. No reason to pay the parse
cost (or commit to a shape) until someone navigates into the value. Text
reads (`.md`/`.txt`/`.csv`) already stay string; this makes the structured
formats lazy too.

Scope (settled 2026-05-30, Ingi): applies to **every** non-string conversion
in `ReadText`, not just JSON. The rule is "read returns raw text, materialize
to the structured shape on first access" — JSON/dict/any non-string CLR target
all go lazy via the same factory.

## 2026-05-31 — event type/property rename (Lifecycle / Bindings)

Still pending (confirmed on `type-kind-strict` by Ingi). Agreed target naming for the
event surface, not yet landed:
- `GoalStepEvents` / `ActionEvents` → `Lifecycle` (one type for all entities)
- `EventList` → `Bindings`
- Navigation reads: `goal.Lifecycle.Before.Run(context)`, `step.Lifecycle.After.Run(context)`

Why: `Lifecycle` with `.Before`/`.After` IS a lifecycle (noun = identity); `Bindings`
with `.Add()`/`.Run()` IS a collection of bindings. Current names describe shape, not
identity. (Lifted out of the old `good_to_know.md` "OBP Naming Principle" block when it
was consolidated into `obp-smells.md`, so the intent isn't lost.)

## 2026-05-31 — feature idea: event-driven parallel branch (fan-out the remaining steps)

Scenario:

```
DoStuff
- read numbers.csv, write to %csv%
- run calculate.cs for %csv%, write to %result%
- write out %result%
```

Bind an event to step 1 (`read numbers.csv`). The handler sends the numbers to an
LLM and gets back **two new sets**. It then tells the runtime: *"here are two sets —
branch, and run the rest of the steps (2,3) in parallel, once per set."* Result: two
parallel pipelines, each computing on its own set.

**Core capability:** an event handler can **return a branch/fork instruction** that
forks the remaining step execution into N parallel processes, each with its own
variable binding. A scatter driven by an event's return.

Open design questions (seeds, not decided):
- **How the event returns the instruction.** A typed return the engine recognises —
  analogous to how a suspending value (`IExitsGoal`/`Ask`) or an error-recovery value
  flows back through the step loop. e.g. a `Branch`/`Fork` Data carrying N binding-sets;
  the engine sees it and forks.
- **Fork point = continue-from-here.** The event fired at step 1; the branch forks the
  *remaining* steps (N+1…) — N continuations of the same step-chain, each with its own
  scope (`%csv%` = set1 / set2).
- **Parallelism + scope isolation.** N parallel runs of the remaining chain, each with
  an isolated variable scope / forked CallStack. Ties to the existing fork-safe
  `_current` AsyncLocal (each branch is its own context).
- **Fan-in / rejoin.** Does the goal end as N independent `write out %result%` (no join),
  or do branches rejoin (collect the N `%result%`s into one)? The instruction may carry
  the join policy, or a later step is the join.
- **Relationship to `loop.foreach`.** foreach already fans per-item by *calling a goal*,
  sequentially. This is different: an event injects a *parallel* fan-out of the
  *remaining pipeline*. Could unify (foreach = sequential case) or stay distinct.
- **Where it lives.** Event-handler return → the step-execution loop forks the remaining
  steps. Connects to "the event collection owns the run" (obp-cleanup #4) and the step loop.

A real new runtime primitive (event-driven scatter / parallel-branch). Captured as a
seed; design when picked up.

## type.Kind vs type.Kinds — singular sweep candidate (2026-05-31, Ingi)

`type.@this` carries both `Kind` (the per-value subtype — `"png"`, `"md"`) and
`Kinds` (the advertised vocabulary — `["keccak256","sha256"]`). Two names one
letter apart for two different concepts reads suspiciously, and OBP leans
singular. Left as-is for now (the vocabulary `Kinds` is a genuine "all valid
kinds" collection, which is a defensible plural). Investigate whether the
vocabulary should be renamed to a distinct singular noun so `Kind`/`Kinds` stop
looking like a typo of each other — without merging the two concepts.

## Render a `table` to a UI — write half of lazy-deserialize (2026-06-03, Ingi)

Follow-on to the `lazy-deserialize` branch, which is the *read* half. That branch types csv/xlsx as `type=table` (grid shape) and carries it untouched to the render boundary — the precondition for Ingi's vision: "read a csv, nothing in the runtime cares about it until I render it, then it draws a table in the UI."

Because csv/xlsx are typed by *shape* (`table`) rather than by encoding, the renderer needs **no kind-awareness** — it dispatches on `type=table` through its existing `(type, format)` model. The follow-on is simply a `(table, html)` renderer entry that draws a grid (and whatever other UI formats), alongside `(table, csv)`/`(table, xlsx)` write-back. Read half + the `table` type ship in `lazy-deserialize`; this is the write half. (Earlier framing of this todo asked for kind-awareness on a `text`/csv value — superseded: typing by shape moved the "it's a table" knowledge onto `type`, where the renderer already looks.)

## Unify TimeSpan's two wire forms (2026-06-03, coder/lazy-deserialize)

Latent inconsistency surfaced during the reader-registry consolidation: a CLR
`TimeSpan` has **two** wire forms depending on the path it takes.
- `IWriter.TimeSpan` (json/writer.cs:42) writes `ToString("c")` → `"00:00:30"`.
- `TimeSpanIso8601` (channel/serializer/TimeSpanIso8601.cs) reads/writes ISO-8601 → `"PT30S"`.

So the same value serializes differently on the plang-value-tree path vs the
STJ-converter path. Real, but out of scope for `lazy-deserialize` and risky to
unify mid-branch (it's load-bearing on snapshot/signing wires). `duration` (the
PLang type) parses both forms via `duration.Resolve`, so reads are tolerant; the
divergence is on the *write* side. Architect's call (2026-06-03): flag, don't fix
here. Unify later — pick one canonical TimeSpan wire form and route both paths
through it (likely the duration type's own renderer/Read once the format-layer
`TimeSpanIso8601` converter is reconsidered).

## Fully type-driven nested Data — retire envelope-recognition (2026-06-03, architect; from `lazy-deserialize` Stage 3)

`lazy-deserialize` Stage 3 keeps a **lean** `LiftDataIfShaped` (`app/data/Wire.cs`): it recognizes a nested Data by its envelope shape (`name`+`value` keys) in the eager-untyped path, because a nested Data in a bare value slot has **no type slot** to drive reconstruction (there is no `data` type, and `json.Writer` emits a nested Data inline with a type slot only when `!Type.IsNull`). The `GetRawText` double-parse was dropped, but the shape-recognition remains.

Endgame (separate branch): add a `data` type to the registry, stamp a Data-valued slot with it on **write**, and reconstruct nested Data purely via `Readers.Of("data", …)` — then the envelope-recognition can be deleted entirely and reconstruction is 100% type-driven (the branch thesis). This is a **wire-format + new-type change on the serialization core** (touches snapshot + signing round-trips), so it was held out of Stage 3. Pick up when the snapshot branch or a dedicated pass touches the wire shape. Note: recognizing the Data envelope is legitimately a leaf serializer's job (not the banned content-sniffing, not a courier #7) — so the lean version is correct interim, not a smell to rush.

## Cleanup CommandLineParser — too low-level (2026-06-05, Ingi)

`app/Utils/CommandLineParser.cs` hand-parses each flag value through
`JsonDocument` + `UnwrapJsonElement` + per-`ValueKind` branching, then decomposes
the native dict/list back to raw for the config bag (architect's item D on
`collections-are-data`). In theory the whole thing should be roughly
`JsonSerializer.Deserialize<Build>(--build)` (and the equivalent typed shape for
`--debug`/`--test`/`--app`) — parse straight into the strongly-typed option record
instead of a raw `IDictionary<string,object?>` the consumers then re-read by key.
Kills the build-native-then-flatten round-trip and the stringly-typed property bag
in one move. Perimeter/infra, low priority — do it as a focused pass, not mid-branch.

**Update (2026-06-10, Ingi, born-typed stage):** ruled with the born-typed store seam
(`.bot/compare-redesign/coder/stage-proposal-born-typed.md`) — CLI config **stays
outside Data** (no carve-out; the seam's "no raw CLR in the slot" invariant has no
exceptions). CommandLineParser keeps its raw shapes until this cleanup lands; the
cleanup is the moment it lifts at the perimeter (typed option records), not a blocker
for the born-typed stage.

## Make Data._type non-null — kill the `if (_type != null)` derive-fork (2026-06-05, Ingi)

`Data.Type` (`app/data/this.cs:341`) lazily derives the type from the value's CLR
type (`AppTypes.GetPrimitiveName(clr)` / `App.Type.Name(clr)`, + number kind) and
caches into `_type`; `_type == null` means "no explicit type, derive on access".
Goal: make `_type` always populated so the getter is trivial and the scattered
`Type != null` / `Type?.` guards can go.

**Watch out — the null is currently load-bearing, not just a cache flag.** It also
encodes "*explicitly* typed vs derived", and a few sites branch on that meaning:
- `this.Navigation.cs:275` (`val is string && _type != null`) and `:316`
  (`_value is string raw && _type != null`) — string→type coercion fires only when
  the user *explicitly* typed the value (`set %x% = "5" as number` coerces; bare
  `set %x% = "5"` stays text). Eager-derive would make these always-true and try to
  coerce plain strings.
- setter `_type = value.IsNull ? null : value` (`:378`) — assigning the `type.Null`
  sentinel means "clear to derive-mode".
- `Value` setter `_type = null` (`:193`) — a rebind drops the cached/explicit type.

So a clean "non-null `_type`" needs to preserve the explicit-vs-derived distinction
(eager-derive at construction/context-wiring + a separate marker, or rework the two
coercion gates to compare against the derived-default instead of null) — not just
flip the field. Derivation for primitives works without context; only runtime-loaded
types need it, so eager stamping is feasible for the common case. Medium-size change
touching the Data core + ~15 `Type?.`/`Type != null` call sites; do as a focused pass.

## List rope/chunked model — spec written (2026-06-05, Ingi)

Decided: PLang `list` is a flat sequence (no observable nesting; 2-D goes to a
`matrix`/`table` type). Internally a rope of `Data` chunks, one per `add`: `add`/`remove`
are O(1) chunk edits that never read existing leaves; `count` is a running leaf counter;
`GetEnumerator` walks chunks yielding the next leaf (no flatten op, no copy); only
list-producing ops (sort/where/unique/map) materialize a flat list. Observable shift:
`add list to list` becomes merge, not nest. Full spec + open bits (flat-index addressing,
internal representation swap, wire shape) in `.bot/collections-are-data/list-rope-model.md`.

## Re-enable 2 signing round-trip tests after signature rework (2026-06-05)

`Tests/LazyDeserialize/{SignAndVerifyRoundTrip, SignedDataSurvivesInList}.test.goal`
are SKIPPED (real steps intact, with a `- tag this test 'skip'` first step). The `@schema`
Data marker makes a signed Data correctly round-trip AS a Data through the
store/goal-call/list; the old `verify` path then hashes a Data-wrapping-a-Data and
mismatches. The fix is the signature redesign (branch `signature-as-schema-wrapper`, spec in
its `.bot/`): a signature wraps the data (`@schema:"signature"`), `verify` peels-and-validates,
`Data.Signature` is removed. **Re-enable: delete the `tag this test 'skip'` line and rebuild
the two .pr** on that branch. (The skip is detected from the goal source by
`test.discover.HasSkipTag`, so the stale `.pr` for these two is never read until re-enabled.)

## Collection mutation: copy-on-write / immutability (the "C" model) — 2026-06-05 (auditor O1, low)

**Problem (raised by auditor v1 O1 on `collections-are-data`).** Collection mutation mixes
value- and reference-semantics, so aliasing leaks in some paths and not others:
- `set %x% = %y%` SHARES the value (`Data.ShallowClone` → same `_value` by reference; the old
  deep-copy was deliberately removed as "redundant", `variable/list/this.cs:308`).
- `add %b% to %a%` COPIES the added list (F1 fix, `list.@this.CopyStructure` in `add.cs`/`set.cs`).
- `add %d% to %list%` SHARES the dict, and `set %d.x%` mutates it in place
  (`SetValueOnObject` → `dict.Set`, `variable/list/this.cs:346`).

So these leak (write-through to a different variable), beyond the one F1 patched:
```
set %x% = %y%;  set item N of %x%        # mutates the shared list → %y% changes too
add %d% to %list%;  set %d.x% = 5        # mutates the shared dict → %list[0].x% changes (O1)
```
Under the flatten/row model, `set item N of %x%` reaching into a shared list is the same
write-through codeanalyzer F1 called "indefensible" — still open via `set`. Copying dicts on
add ("option A") does NOT fix it: `set` still shares.

**The fix — "C": finish the immutability the codebase half-built.** It already chose
share-by-default (ShallowClone) and `set %x%` already rebinds; the in-place mutators don't.
Make EVERY mutator (`add`, set-item, set-path, `remove`, `insert`, `sort`, `reverse`)
**copy-on-write**: mutate in place when the collection is uniquely owned (so `add`-build stays
O(1)), copy-once-then-mutate when it is shared. Needs a cheap "am I shared?" signal (a flag set
when a collection is ShallowCloned into a variable or stored inside another collection).
Result: sharing is cheap AND safe, lists/dicts behave the same, and the F1 `CopyStructure` /
copy-on-add **dissolves** (replaced by the uniform guard). This is the Clojure/persistent-
structure model; full structural sharing (O(log n) mutators) is a later optimization over the
first copy-on-write cut.

**Sequencing.** Own branch + spec (supersedes the F1 copy on `collections-are-data`). Not urgent
(low): on `collections-are-data` the F1 copy stays as the interim patch for the most-flagged
path; the residual `set`-share / dict-in-list leaks ride until C lands.

## Per-path lazy narrowing of a materialized value

**Date:** 2026-06-06 on branch `scalars-as-native`. Raised by Ingi during the
born-native scalar flip.

**What:** Today, the first touch of a json-backed value materializes the **whole
tree** in one shot. `read user.json, write to %user%` holds only raw bytes until
touched (genuinely lazy at the file boundary), but the moment you touch `%user%`
*in any way* — `%user.name%`, `%user.address.zip%`, even writing `%user%` out —
the `(object, json)` reader (`PLang/app/type/object/serializer/json.cs`) runs
`JsonSerializer.Deserialize` + `UnwrapJsonElement`, which recursively walks the
entire document and wraps every leaf (`name`→`text`, `zip`→`number`, the whole
`address` subtree). There is no per-*path* laziness: touching `zip` does not
narrow only the spine down to `zip` and leave the rest un-narrowed `item`.

**Why consider it:** for a large document where a goal reads only one deep field,
the whole tree is parsed and every leaf allocated a wrapper (~24 B/leaf, see the
scalars-as-native allocation note). Per-path laziness would materialize only the
navigated spine, leaving siblings as un-narrowed `item(kind=json)` slices of the
raw blob until they too are touched.

**Why NOT done in scalars-as-native:** this branch deliberately keeps the
existing "whole value materializes on first touch" model (which predates it — it
was already true for `dict`/`list`; the branch only changed the *leaf* from raw
`int` → `number.@this`). Per-path laziness is a separate, larger change to the
narrow/materialize machinery, orthogonal to making leaves native.

**Where it would live:** the narrow seam — `Data.Materialize()` /
`item.Narrow()` (`PLang/app/data/this.cs`, `PLang/app/type/item/this.cs`) and the
`(object, json)` reader. A per-path design would have `Narrow()` produce a `dict`
whose property values are themselves raw-backed `Data` (a `_raw` JSON slice +
`item(kind=json)` type), materializing only when navigated — turning today's one
eager walk into N lazy ones. Cost: more bookkeeping, holding the raw blob alive
longer (GC trade-off), and re-parse-per-path unless slices are cached.

**Decision:** Ingi is fine with the 2× allocation for now; logged for later.

## Centralize value-type serialization to IWriter; purge STJ attributes from value/domain classes

**Date:** 2026-06-06 on branch `scalars-as-native`. Raised by Ingi while adding
bare serialization for the scalar wrappers.

**Context / current state (Option A, what shipped on this branch):** each scalar
wrapper (`text`/`number`/`bool`/`datetime`/`date`/`time`/`duration`/`null`) and
each collection (`dict`/`list`, from collections-are-data) carries a per-type
`[System.Text.Json.Serialization.JsonConverter(typeof(Json))]` attribute + a
sibling `Json.cs` that does the raw-STJ projection. This is the "raw STJ" path
(plain `JsonSerializer.Serialize` calls: LLM cache, snapshots, http bodies,
`dict.Json.Write` recursion) — distinct from the format-agnostic
`application/plang` wire (`Data.Normalize → IWriter → json.Writer`). Chosen for
consistency with the already-merged dict/list precedent.

**The problem Ingi flagged:** a value `this.cs` should NOT be aware of any
concrete serializer. Today they are, two ways:
  1. the `[JsonConverter(typeof(Json))]` attribute names System.Text.Json, and
  2. domain/value classes are littered with STJ-specific attributes —
     `[JsonIgnore]`, `[JsonPropertyName]`, `[JsonConstructor]`, `[JsonConverter]`.
When a second IWriter ships (protobuf, MsgPack, CBOR), it must NOT have to know
about `[JsonIgnore]` — that attribute is meaningless to protobuf. Serialization
discipline (what crosses the wire, what's hidden, property names) must be
declared in a **PLang-native, format-neutral** vocabulary that every IWriter
honors uniformly.

**Option B — the target shape:**
  - **One** `JsonConverterFactory` registered at the `app/data` layer that
    intercepts ANY `item` during raw STJ and bridges its format-neutral IWriter
    bare-write onto STJ's `Utf8JsonWriter`. Delete every per-type `Json.cs` +
    `[JsonConverter]` attribute (scalars AND dict/list, so there's one pattern,
    not two).
  - **PLang-native wire attributes** replacing the STJ ones on value/domain
    classes: a `[WireIgnore]` (or reuse/extend the existing `[Out]`/`[Store]`/
    `[LlmIgnore]` Tagged filter in `app.channel.serializer.filter`), a
    PLang-native property-rename, and a PLang-native "construct from wire"
    marker — so `[JsonIgnore]`/`[JsonPropertyName]`/`[JsonConstructor]` disappear
    from the value/domain surface. Every IWriter (json, protobuf, …) reads the
    SAME PLang-native tags; the json IWriter is the only thing that translates
    them to STJ semantics, at the one bridge point.

**Why not now:** it's a cross-cutting refactor touching every value/domain class
and the already-merged dict/list; out of scope for locking the scalar wrappers.
Ingi's decision: ship A on this branch, do B as its own pass.

**Where:** `app/data/` (the factory + Tagged filter), every `app/type/<t>/Json.cs`
(delete), every value/domain class carrying `[Json*]` attributes (retag).

## Error messages should show the PLang type name, not the CLR GetType().Name

**Date:** 2026-06-06 on branch `scalars-as-native`. Raised by Ingi reviewing the
collapsed ScalarComparer.

**What:** Many error/diagnostic messages interpolate `value.GetType().Name` (CLR
name, e.g. `@this`, `Dictionary``2`, `Int64`) where the PLang type name
(`dict`, `number`, `text`) would read far better for a PLang developer. Example:
`ScalarComparer.Order` throws `"cannot order {a.GetType().Name} against
{b.GetType().Name}"` — should say `"cannot order dict against number"`.

**Why not trivially done:** at many of these sites the value is a bare value
object, not a `Data`, so `a.Type.Name` isn't reachable and there's no Context to
hit the type registry (`catalog.ResolveName(clrType)`). Two fixes are needed:
  1. A context-free value→PLang-name helper (the wrappers can expose a static
     PLang name; the registry maps CLR→name for the rest), OR move the message up
     to where a `Data`/Context is in scope and use `data.Type.Name`.
  2. A sweep: grep `GetType().Name`/`GetType().FullName` in error/exception
     message strings across production C# and convert each to the PLang name.
     (The collapsed `ScalarComparer` removed the old per-type `Name()` switch that
     used to do this for the comparer — the systematic replacement should restore
     PLang-named messages everywhere, not reintroduce a per-site switch.)

**Decision:** logged for a dedicated pass; not folded into scalars-as-native.

## Serialization centralization, part 2: Normalize-on-item (read/walk side)

**Date:** 2026-06-06 on branch `scalars-as-native`. Ingi, extending the
serialization-centralization todo.

Companion to the IWriter centralization. Two virtuals now belong on `item`:
- `Write(IWriter)` — **DONE on scalars-as-native**: each leaf renders its own bare
  wire form; `json.Writer.Value` collapsed its per-scalar `case` list to one
  `if (value is item leaf && leaf.IsLeaf) leaf.Write(this)`.
- `Normalize(mode, visited, depth, types)` — **TODO**: make the NormalizeValue
  type-switch a virtual on `item`. `dict`/`list` override (recurse entries),
  scalars use the default (return self). Then `NormalizeValue` collapses to
  `if (value is item iv) return iv.Normalize(...); else <raw-CLR perimeter>`.

**Residual that can't vanish:** raw CLR values from non-PLang code (bare `string`,
`Dictionary`, `enum`, `byte[]`) are not `item`, so the raw-CLR perimeter arm stays;
and `path`/`image`/`code` reflect through the shared `[Out]`-filtered NormalizeObject
+ renderer registry, which doesn't cleanly move per-type. Net: one dispatch for all
items + a perimeter fallback.

**Decision:** land the `where T : item` constraint first (branch goal), then do
Write+Normalize-on-item as one pass. Folds into [the IWriter-centralization todo above].

## `variable.set` — redundant type when force matches judged value type

**Date:** 2026-06-07 on branch `scalars-as-native`. Ingi, noticing the set.examples
mapping `variable.set Name([variable] %iso%), Value([duration] PT5M), Type([type]
{"name":"duration"})`.

The type appears twice: once as the **Value wrapper's own type** (`[duration]` on
`PT5M`) and once as the **separate `Type` force parameter** (`{name:duration}`). When a
force (`as duration` / `(duration)`) coerces the value to exactly the type it would be
judged anyway, the two are identical — redundant. Now that literals are judged by form +
intent (see the literal-judgement change), a force *usually* lands the value at the same
type its wrapper already carries, so the `Type` param duplicates the Value's type more
often than not.

**Investigate:** does the separate `Type` parameter still earn its place? It is only
meaningfully distinct when the force DISAGREES with the value's natural type — e.g.
`"2026-01-01" as text` (force text over a date judgement) or `"42" as image/jpg` (a kind
that isn't derivable from the value). For the agreeing case, the Value wrapper's type is
the single source and `Type` is noise. Options to weigh: (a) emit `Type` only when it
differs from the Value wrapper's judged type; (b) drop `Type` entirely and let the force
flow through the Value wrapper's type (the `as`/`(kind)` just sets the wrapper's type);
(c) keep both but document the redundancy as deliberate. Touches `variable.set`'s
`TypeFromWire`/`FromName` reconstruction and the compile teaching.

**Decision:** logged for a dedicated investigation; not changing now.

## 2026-06-11 — Typed-null slot citizen `@null.@this<T>`
A declined/absent `Data<T>` slot currently holds the untyped `absent.Slot`. A
typed-null citizen (`@null.@this<T>`) would make the slot say *which* type is
missing — richer diagnostics for navigation/serialization/error views without
reading the error object. Orthogonal to the instance-returning `Create` (that
boundary must stay `T?` — a typed-null can't satisfy a `T` return; can't inherit
a type param, and the leaf types are sealed). Cost: thread T through every
absent/present-null construction site; the door returns bare `item` so untyped
couriers still see `item`. Polish, not a correctness gap — the decline error
already names the target type. Don't fold into the born-typed slice.

## 2026-06-11 — Remove the raw-container Lift bridge (born-native invariant)
`data.@this.Lift` currently converts a raw C# `List<object?>`/`Dictionary`/`ArrayList`
into native `list.@this`/`dict.@this` (via `json.Parse(SerializeToElement(v))`) as a
TEMPORARY bridge — see the `TODO(remove)` in `PLang/app/data/this.cs` Lift. A raw
container should never reach a `Data`: container values are born native off the wire.
The bridge exists because several seams still hand raw containers to `Data`:
  - LLM result — `OpenAi.Query` → `Data.Ok(resultValue)` (`llm/code/OpenAi.cs:476`)
  - `Variable.Set(name, rawValue)` (`variable/list/this.cs:253`)
  - `Diff`/`Normalize` transients — `Data.Ok` of a decomposed raw bag
    (`data/this.Diff.cs:38` → `this.Normalize.cs`)
  - `Reconstruct` (`As<T>`) — `Data.Ok` during CLR reconstruction (`this.Reconstruct.cs:44`)
Fix: make each seam build native, then delete the Lift conversion and restore the
commented-out throw as the invariant (the throw is kept in place, commented, right
above the conversion). The throw was verified to fire at exactly these seams + the
unfaithful C#-composition tests.

### Known regressions from the temporary Lift bridge (restored when bridge is removed)
The raw-container Lift bridge over-resolves ACTION-TEMPLATE containers: it flattens a
raw action-list into a uniform native dict/list/text graph, so `StampTemplates` recurses
and stamps DEFERRED sub-action `%var%` that must stay raw; the door then renders them.
Two tests regress (green at c026ff245):
  - `DataWrappedActionList_DoesNotRecurseIntoActions`
  - `DataWrappedActionList_SubActionParametersRemainRaw`
The real pipeline keeps action templates as `PrAction` (which the stamp walker doesn't
recurse), so deferral holds there. Both are fixed for free by the "template born on the
value from the builder" design (`.bot/compare-redesign/coder/v8/template-ownership-proposal.md`):
no walker → no over-stamp. Do not chase these separately; they go green when that lands.

## 2026-06-11 — Switch text .ToString() → Clr<string>() across the project
`Clr<string>()` names the intent explicitly ("I want the CLR string at a .NET edge");
`text.ToString()` hides it. `text` already implements `Clr(Type)` (`type/text/this.cs:117`),
so the swap is mechanical. Three sites already converted (variable/set.cs, actor/this.cs,
type/duration/this.Convert.cs); there are more `.ToString()` reads on `text` instances
across the project — find them (grep for `.ToString()` where the receiver is a
`text.@this`) and switch to `Clr<string>()`. Keep `ToString()` only for genuine display
edges (logs, error messages, interpolation), not for extracting the backing for a .NET call.

## 2026-06-11 — dict/list raw-backing + lazy parse (collection redesign)
Today dict stores `List<Data>` entries (and list likewise) — eagerly Data-keyed, so
`Clr` must BUILD the raw `Dictionary`/`List` on demand (the decompose loop in
`dict.Clr`/`list.Clr`). The original justification (per-entry signature preservation
through containers) is GONE — signatures don't work that way (Ingi, 2026-06-11). So the
right design is: the collection's backing IS the raw C# object (`Dictionary<string,object?>`
/ `List<object?>`), values parsed/typed lazily on access (like `source`). Then `dict.Clr`
collapses to "return the backing" — no loop, no ClrConvert decompose. Major refactor:
touches navigation (`%dict.key%`), wire (Normalize/Json), conversion, the generic
`list<T>`. Overlaps the lazy-Normalize rework — same architect redesign bucket. Works for
now (the loop is correct on the current Data-keyed design); do not start solo.

## 2026-06-11 — Split type.catalog: LLM vocabulary (plang) vs runtime CLR registry/conversion
`type/catalog/@this` wears three hats in one class: (1) LLM type VOCABULARY —
`BuildTypeEntries`, `ValidValues`, the schemas (only needs PLANG types: names +
teaching, no CLR); (2) name↔System.Type REGISTRY — `Get`/`Clr`/`GetPrimitiveOrMime`
(CLR-keyed, runtime dispatch); (3) CONVERSION ENGINE — `TryConvert`/`ClrConvert`
(the .NET-edge bridge every `Clr()` funnels through). Roles 2+3 legitimately need
CLR (perimeter to .NET); role 1 does not — yet it's CLR-keyed too (`ValidValues`
takes a `System.Type` to reflect an enum's values for the LLM, when it could key
by the plang type). Split the display vocabulary (plang-centric) from the runtime
CLR registry/conversion. Also OBP shape smells to fix in the same pass: Get/verb
twins (`GetValidValues`+`ValidValues`, `GetBuilderTypeNames`+`BuilderNames`,
`GetTypeName`+`Name`) and `Build`/`Get`+noun names (`BuildTypeEntries`,
`GetPrimitiveOrMime`). Architect bucket.

## 2026-06-11 — Data.Load()/LoadValue die with async Write
`data.Load()` (called by the Json + plang serializers before writing) walks the value
graph and pre-`LoadAsync`es every lazy reference (file/image/url) ONLY because the STJ
writer is sync and can't await a load mid-write. Its private `LoadValue` recurses by
type-switching on dict/list (a courier iteration ladder — left as-is, do NOT polish).
The async-Write rework (already a tracked stage-9 prerequisite) makes each value load
itself at write time through its own async Write/door, deleting Load()/LoadValue and
their dict/list branches entirely. So the data.Load.cs is/as sites are resolved by that
rework, not a standalone fix.

## 2026-06-12 — error as a first-class plang type
An error is not a plang value type. `%!error%` is a `DynamicData` whose value is the
raw C# `IError`, so when an error rides as a *value* it gets wrapped in a `clr` carrier
(an opaque object to the type system). Any code that needs to recognize "this value is
an error" must open that carrier — e.g. `throw` re-raise does
`thrown?.Clr<object>() is IError` (PLang/app/module/error/throw.cs), and navigation
reads `Message`/`Key`/`Details` off the IError by reflection. That carrier-opening is
the smell, and it recurs everywhere an error rides as a value.

Fix: add `app.type.error.@this` (an `item`) wrapping the `IError`. Then `%!error%`'s
value is an `error.@this` (navigation reads its members as a real plang value), and the
re-raise becomes `if (thrown is error.@this err) return Error(err.Inner);` — the value
tells us its nature, no `Clr`. Point the `!error` DynamicData (PLang/app/actor/context/
this.cs:179) and the other error-as-value paths at the new type. Removes the
`Clr<object>() is IError` leaf-read in throw and the reflection navigation of IError.

## 2026-06-12 — builder broken: cascade of value-layer strays from the fundamentals refactor
`plang build` of ANY goal was failing. Root causes are value-layer strays from the
born-typed refactor (NOT source-gen / param-bind):

1. FIXED — `text.Value` (the deleted property) read as a method group in `type.Judge`
   (PLang/app/type/this.cs:410, `text.@this t => (object)t.Value`). C# infers a natural
   delegate type, so `t.Value` silently became `Func<data,ValueTask<item>>` and the
   channel param "builder" judged into `source(Func,…)` → `ChannelNotFound`. Changed to
   `t.ToString()` (matches the sibling extraction at :370). Net −14 test failures across
   Data/Modules/Wire/Types — this leak was breaking tests broadly. AUDIT for other stray
   `text.Value` reads (the property is gone; any `someText.Value` now compiles to a Func
   silently — the compiler won't catch it).

2. OPEN — typed `%ref%` params don't render. `builder.goals path=%path%` arrives as
   `clr(Value = text("%path%"))` labeled `path`; `Build.goal:13` then hits
   `Directory not found: …/os/system/builder/%path%`. `StampedForm` (PLang/app/data/
   this.cs) only stamps a `%ref%` carrier as a template when its declared type is
   text/string AND its value is a raw string — but a typed `%ref%` rides as a
   `clr`/`source` carrier wrapping a `text`, so it's never stamped → never rendered.
   Pervasive: ANY non-text-typed param with a `%var%` value (path/channel/etc.). The
   right fix likely intersects with param-bind's "full-match %var% → Variable.Get<T>
   identity hop" (a reference, not a born-typed carrier) — flag for design. BLOCKS the
   .test.goal layer until fixed.

## 2026-06-12 — Data.Created/Updated are DateTime, should be DateTimeOffset (plang datetime)
PLang/app/data/this.cs — `public DateTime Created { get; }` and `Updated` are CLR
`DateTime` set via `System.DateTime.UtcNow`. Intended shape is `DateTimeOffset` (the
plang default `datetime` object). Switch both fields + the assignments to
`DateTimeOffset.UtcNow` (and audit any consumer that reads them as DateTime).

## 2026-06-12 — remove the type converter (Judge) from Data; ctor takes only (name, item)
Data still does declared-type conversion in its ctor (`new Data(name, value, type)` →
`Lift` + `type.Judge`) and `Data.Declare` → `type.Judge`. Per the settled design, Data
must only HOLD an item; the `raw → typed item` step is owned by the type's reader
(`type.Deserialize`, already built) and happens at the CALL SITE (Wire.cs ReadBody,
variable.set handler, etc.), then `new Data(name, item)`. The object-taking ctor stays
as a convenience (lifts raw→natural item) so we avoid migrating ~944 `new Data(name,raw)`
sites now.

Two gates to finish the removal:
1. **ctor item-only** = migrate the ~944 `new Data(name, raw)` sites to `data.Ok(raw)` /
   pass items (production by hand, tests bulk-sed). data.Ok/From lift raw→item; the ctor
   sticks to `(name, item)`.
2. **readers apply declared kind/strict** — Judge also does kind/strict/binary/facet
   reconciliation (text Kinded, image-vs-binary, strict carrier label). Deleting Judge
   without moving that INTO each type's reader regresses ~11 kind/strict tests
   (SetAsTextMd, *ImageGifStrict*, Wire/PrParameter kind roundtrip, Data_KindGetter).
   The work moves onto each type (the type knows its own kind/strict), per OBP.

Then: delete `Judge`, `Declare`→`type.Deserialize`, `As(string)` (replaced by `Value<T>()`,
delete + its tests), `FromWireShape`/static `action.FromWire` collapse into the Wire path.
Source-gen binding = the `param-bind` branch (`GetParameter<T>` + `Variable.Get<T>`, bind
lines, demolish the resolve machinery). Foundation already committed: readers for all types,
`type.Deserialize`, `path` holds `text`, `Wire`/`FromWireShape` deserialize via reader,
`Variable.Get<T>` (Data<T>), `Action.GetParameter<T>` (started). See branch `param-bind`.

## 2026-06-12 — remove variable.GetValue(name) (never return a CLR type)
`app.variable.list.@this.GetValue(name)` returns a raw CLR `object?` — the store
should never hand back a CLR type; it holds Data and answers Data. Callers that
genuinely need a .NET-edge value lower through the item's own `Clr<T>()` at the leaf.
`Variable.Get<T>(name)` now returns `Data<T>` (the typed ask, identity hop); that is
the door. Migrate `GetValue` callers (app/this.cs, identity/code/Default.cs,
signing/code/Ed25519.cs, actor/context/this.cs, module/test/run.cs) + the CLR-typed
`Get<T>` test callers (MemoryStackCloneTests `Get<List<string>>`/`Get<Dictionary>`,
ExecutorTests `Get<string>`) to the `Data<T>` ask / native collection ops, then delete
GetValue. Part of the same born-typed completion as the type-converter removal above.

## 2026-06-14 — clr-dissolution follow-ups (after the value-renders-itself commit)
The "value renders itself" change (writer dispatches `item.Write`; killed the
IsLeaf-gate + TypedValueNode arm for items; enum→choice in Lift) dissolved two
clr sources. Remaining, in priority order:

1. **Static renderers are now dead for items.** image/file/code/directory/url +
   hash each have an instance `Write`; their `serializer/<fmt>.cs` static
   `Write` is no longer reached for items (only via the TypedValueNode case,
   which items no longer hit). They still hold a duplicate copy of the render
   logic. Make them delegate to `value.Write(writer)` (path's pattern) or delete
   them once *SerializerTests are confirmed to route through the channel, not the
   static directly. snapshot's instance Write delegates the OTHER way (to its
   static walk) — fine, single-source, but inconsistent direction; unify later.
2. **error is the last non-item with a renderer.** A non-item POCO with a
   renderer nested in a dict still hits the rebox→clr (TypedValueNode can't be a
   Data value). Real production case: `error` (IError). Make `app.type.error.@this`
   a real item (already a separate todo above) — then NO non-item has a renderer,
   TypedValueNode dies entirely, and the writer's TypedValueNode arm + the
   renderer registry can both go. Test `Normalize_NestedRegisteredValueInsideUnregistered_TagsInner`
   (pre-existing red) encodes the old non-item-tagging behavior; revisit when error becomes an item.
3. **Write should be abstract / build-gated.** Today item.Write is virtual with a
   throwing default. Once actions stop being `:item` (next), make Write abstract
   for value types — or gate it via the generator (PLNG) for `app/type/**` so a
   value type missing a Write/Read fails the build, not at runtime.
4. **Actions should not be `:item`.** Confirmed inert: the catalog discovers
   types by `@this`-naming (+[PlangType]), not by `:item`; RunAction constrains
   `ICodeGenerated`, not `item`. Drop `:item` from the 132 action records (via the
   generator) so `item` = value types only — which unblocks abstract Write (#3).

## 2026-06-14 — clr/TypedValueNode kill: where it stands + the remaining sweep
DONE this session (committed+pushed on compare-redesign):
- value-renders-itself: writer dispatches `item.Write`; killed the IsLeaf-gate +
  TypedValueNode arm FOR ITEMS; enum→choice in Lift. (2 clr sources dissolved.)
- error IS an item: `Error : item.@this, IError`. error-as-value (%!error%,
  errors-trail, throw re-raise, snapshot) renders/navigates itself, no wrapper,
  no clr. Lift returns it as-is (it's an item). Data.Error sidecar unchanged.
- Result: ZERO clr carriers on the wire across all suites; no production code
  produces a TypedValueNode; zero regressions; Types 26→13, Runtime 66→57, Wire 32→30.

REMAINING to fully delete TypedValueNode (the class) — a bounded test-surgery sweep:
- Normalize this.Normalize.cs:214 — drop the `: new TypedValueNode(...)` else
  (only test-fixture non-item-with-renderer hits it now); make it `return value`.
- writer.cs — delete the `case app.data.TypedValueNode typed:` arm.
- delete PLang/app/data/TypedValueNode.cs; clean the doc-comment refs in
  catalog/this.cs:60 and IWriter.cs:24.
- TEST surgery (10 files reference TVN): delete TypedValueNodeNormalizeTests.cs
  (19 refs, tests the deleted mechanism) + the TVN-dispatch methods in
  IWriterFormatTests.cs (5); the other 8 have 1-3 incidental refs (assert
  IsTypeOf<TypedValueNode> — now expect the item directly). ANNOUNCE the test
  deletions before doing them.

THEN (separate, larger):
- renderer (write) registry `app.type.renderer.@this` can die once Normalize's
  `Renderers.Has` "renders-self?" check is replaced (every value-item has Write).
  The static `serializer/<fmt>.cs` Write methods go with it (keep the Read methods
  — the reader registry still needs them).
- clr the CLASS only fully dies with "force everything to a real item" (actions
  not `:item`, all domain types become items) — the big migration.

## 2026-06-14 — retiring the renderer (write) registry — `app/type/renderer/this.cs`
After TypedValueNode's deletion the registry's WRITE-DISPATCH (`Of`) is dead — a
value renders itself via `item.Write`. Two jobs remain before the file can go:

1. **Built-in decouple (bounded, do next):** Normalize still calls
   `types.Renderers.Has(typeName)` as its "this value renders itself, don't
   reflect it" signal. Move that onto the item — `item.@this` virtual
   `RendersSelf => false`, overridden `=> true` on the non-leaf self-renderers
   (path [covers file/http/dir-path], file, url, code, directory, image,
   snapshot, hash, error). Leaves already pass through via IsLeaf. Then drop the
   now-dead `renderers` ctor param + `_renderers` field from json/Writer (the
   only `_renderers` user, the TVN case, is gone) and update its construction
   sites (Wire.cs:519 + ~10 Wire test files that pass `renderers:`). After this
   the registry's `Of`/`Has` have no built-in callers.
2. **Code-load seam (Stage 7, larger):** `ITypeRenderer` + `Loader.cs`
   (Renderers.Register + the `Renderers.Has` coverage gate at Loader.cs:169) is
   how a `code.load`ed DLL ships a renderer for its type. Under "renderer =>
   Write" a runtime type renders via `item.Write` too — retire ITypeRenderer /
   Register / the Loader gate, then delete `app/type/renderer/this.cs` and the
   catalog `Renderers` property. The static `serializer/<fmt>.cs` Write methods
   (dead once nothing discovers them) go too; KEEP their Read methods (the reader
   registry still needs them).
