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
