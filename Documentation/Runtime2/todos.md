# Runtime2 TODOs

> **Audited 2026-05-11** (`runtime2-foundation-verify` v1, architect). Every
> entry in this file was checked against the current code. Resolved entries
> are marked inline (`✅ RESOLVED`). Entries still open were re-verified and
> kept as-is. Next reader: trust the inline markers, not the dates.

## 2026-04-24 — cleanup lazy generator, get it to OBP  ✅ RESOLVED 2026-05-01 (`runtime2-generator-obp`) + 2026-05-09 (`runtime2-cleanup`)

`LazyParamsGenerator.cs` is gone. Generator decomposed into `PLang.Generators/this.cs` (entry) → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Code}/this.cs` (polymorphic per-property). The `ResetResolution` patching was replaced by clean per-call backing-field reset emitted at `Emission/Action/this.cs:139` (`__<prop>_backing = default; __<prop>_set = false`). The deeper "request-scoped vs pr-template Data" lifecycle question dissolved with the backing-field shape — Data isn't reused across executions; the backing fields are.

The `[VariableName]` → `Data<App.Variables.Variable>` migration was also part of the same arc (see 2026-04-30 entry, marked resolved 2026-05-01).

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
- `App.Run` pushes a frame for every action at `PLang/App/this.cs:460` (handles `CallStackOverflowException` outside the try).
- `Goal.Run` pushes a goal-entry frame at `PLang/App/Goals/Goal/this.cs:288`.
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

## 2026-04-27 — PLang tests for error.handle recovery-value path

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

**Resolved on `runtime2-generator-obp` (architect/v5 → coder/v7 → coder/v8).** Took the typed-payload route rather than the speculative `VarRef<T>` (option 1 below): introduced `App.Variables.Variable` (record `Name, RawValue, WasPercentWrapped`) plus `IRawNameResolvable` marker. `[VariableName] partial string` slots become `Data<App.Variables.Variable>`; `Data.AsT_Impl` skips its `%var%` substitution branch for `T : IRawNameResolvable` and dispatches to `Variable.Resolve(raw, ctx)` directly. Both `value="%x%"` and bare `value="x"` collapse to `Variable { Name = "x" }`. 22 handlers migrated, `Emission/Property/Legacy/this.cs` deleted, `[VariableName]` attribute and `__Resolve<T>`/`__StripPercent`/`__HasParam`/`RawScalarValidations` removed, `PLNG001` collapsed to a two-rule gate. Coder/v8 added a generator-side pre-`Run()` guard so non-nullable `Data<Variable>` slots surface `MissingRequiredParameter` (closing auditor/v2 finding #1). The original design discussion is preserved below for archival/context.

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

## 2026-05-05 — runtime2-callback branch close-out (open items from coder handoff)

Coder shipped Stages 1–4 (2720/2720 C# green; 188 PLang pass / 0 fail / 4
stale). Open decisions and gaps from `.bot/runtime2-callback/coder/handoff.md`,
captured here so the branch can close. Each cluster is a real follow-up, not
branch-blocking.

### Ratification sweep (decisions made under the line)

The following calls were made by coder mid-stage without an architect doc.
They're shipped and tested, but no architect has read the code that landed
them. Next architect session that touches callback should sweep these and
either bless or carve a redesign:

1. `output.ask` shape — sentinel `%!ask.answer%` for resume; untyped `Data`
   `Variables` param; `AskCallback.Answer` init-only field written before
   re-dispatch. (Stage 4 doc didn't specify the ask handler.)
2. Lazy `Data.Signature` carve-out — auto-populates only when `_value is
   ICallback`; everything else uses explicit `EnsureSigned()`. Done to
   preserve existing `Signature == null` verify checks.
3. `RawSignature` internal accessor — peek without triggering populate.
   Pragmatic workaround for (2); revisit if (2) gets redesigned.
4. `RestoredFrame` surrogate record vs. extending `Call.@this` with a
   restored mode. Coder picked surrogate because `Call`'s ctor is internal
   and lifecycle-coupled (Stopwatch, AsyncLocal, OnSet).
5. `Errors.Push` setting `error.App = this.App` to wire the back-ref needed
   by `Error.Callback` materialisation. Confirm this is the right injection
   point, or move materialisation off `Error`.
6. Sync-over-async (`.GetAwaiter().GetResult()`) in `Data.EnsureSigned` and
   `Callback.Serialize`/`Deserialize`. Stage-3 doc explicitly endorsed it
   for `Signature`; coder extended to Serialize/Deserialize for symmetry.
7. `PlangDataSerializer` JSON `{type, value, signature}` envelope. Stage-3
   left format open. Decide whether to pin a real wire format (CBOR,
   length-prefixed, etc.) or accept JSON.
8. `ErrorCallback.Serialize` narrow shape — only CallStack frames + Variables,
   not full Snapshot fidelity (Errors.Trail, Providers regs, Statics bags
   don't round-trip across the wire). Adequate for current tests; production
   needs a richer wire pass.
9. `os/system/builder/.build/buildgoal.pr` hand-edit — fixed `Actor:
   %subGoal%` and `KeyName: subGoal` to null in the foreach BuildSubGoal
   step, because `plang build` was broken on a fresh app. Decide whether
   this is the right fix or whether the builder LLM prompt needs work.

### Stale PLang tests — need builder/verb work

Four `Tests/Callback/*/Start.test.goal` stubs are stale because the PLang
surface they test doesn't exist yet:

10. **AskVarsOnNonAsk** — needs builder validator that rejects `vars:`
    annotation on non-`output.ask` actions. Build-time check; lives in
    `system/builder/`. Real builder work.
11. **CallbackTimeoutSetting** — needs PLang verb that writes
    `app.Callback.Signature.ExpiresInMs`. Either extend `variable.set` to
    walk into App config, or add a `callback.timeout` action. ~30 lines
    once approach is picked.
12. **DurabilityRoundTrip** — needs PLang surface for writing `Data` with
    explicit `application/plang+data` mime to a file and reading it back
    into a different App. Needs `file.write` (or similar) to take a mime
    hint and dispatch through the registered serializer.
13. **TamperedSignature** — trivial once (12) lands; needs a Plang surface
    for byte-level mutation of a serialised payload. Without (12), no
    Plang reach into raw bytes.

(11) and (12)+(13) are entangled with (1) and (7) respectively — pick up
together when the ratification sweep happens.

### Other branch-level loose ends

14. **HTTP wire transport for ask-user** — Stage 4 doc explicitly listed
    this as separate work. Without it, real ask-user pause/resume across an
    HTTP boundary doesn't exist; only the in-process resume in
    `AskCallback.Run` works. Needs its own design pass.
15. **Real symmetric crypto** — already tracked in the
    `2026-05-05 — crypto.encrypt / crypto.decrypt real implementation`
    entry above. Listed here only for cross-reference.
16. **Builder revalidation after `buildgoal.pr` hand-edit** — per CLAUDE.md
    "When the builder changes — revalidate. All previously passing tests
    must be rebuilt and rerun." Coder didn't trigger a global rebuild; the
    edit only affects fresh-app foreach behaviour, so probably fine.
    Confirm on next builder pass.

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
   namespace path today is `App.Events.Lifecycle.Bindings.Binding.@this`
   (the `EventBinding` alias). `Bindings` and `Binding` aren't conceptually
   under Lifecycle — Lifecycle is one *use* of them. Lifting them up would
   shorten to `App.Events.Binding.@this`.
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

Context: `App/Debug/this.cs:101` allocates a single `App.CallStack.@this()` per app
and exposes it as `app.Debug.CallStack`. Comment in `App/Actor/Context/this.cs:44-47`
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

