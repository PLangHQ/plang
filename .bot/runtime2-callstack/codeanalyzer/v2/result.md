# codeanalyzer — runtime2-callstack — v2

## Scope

11 commits since v1 (`bdfa1ab7..0941d4e3`). Two workstreams landed:
1. **coder/v1 follow-on** — Goal pushes Call frame, tag attaches to caller (`81dc910c`).
2. **runtime2-source-resolution merge** — `Data.AsT_Impl` carve-outs, `SubstitutePrimitive`
   preservation, `DictionaryNavigator` / `Variables.SetValueOnObject` JsonObject support,
   `TypeMismatch` diagnostic message, recovery-body Cause threading, quoted-key paths,
   "stored values are values" rule, builder `validateResponse` action.

Source-only diff: 19 files / +770 −101.

## v1 carry-forward — ALL 5 MINORs still open

The coder picked up source-resolution work and did not address last round's findings.
Verbatim status:

1. **`CallStackFlags.Tags` flag dead** — `Call.Tag` (`Call/this.cs:142-146`) and
   `tag.cs:42-47` write unconditionally. Doc lie unchanged.
2. **`Errors._current` is `static readonly AsyncLocal`** — `Errors/this.cs:15` still
   class-static; per-instance test isolation pattern still violated.
3. **`stack.Push` outside try/catch** in `App.this.cs:401`; `try` opens at line 411.
   `CallStackOverflowException` escapes as a CLR exception. Plus a NEW instance — see
   below.
4. **`Call.Diffs.Add` not thread-safe** — `Call/this.cs:131` no lock; sibling
   `Children` got one for the same reason.
5. **`context.Step.Context = context` not restored in `finally`** — `App.this.cs:408`
   sets it; `finally` at 443-451 restores `context.Step/Goal/Event` only. Step instance
   leak persists.

Plus: **`SerializableCallStack`** still has zero production refs (only test alias).

## New findings — 3 MINORs, 2 NITs

### MINOR — `Goal.RunAsync` Push outside try/catch (mirror of v1 #3)

`PLang/App/Goals/Goal/this.cs:278` —

```csharp
await using var goalCall = context.App.Debug.CallStack.Push(goalEntryAction);

try
{
    var result = await Steps.RunAsync(context);
```

Cycle detection lives in `CallStack.Push` (per `1881d6c0` commit msg + branch architecture). A
recursive goal trips it AT this Push, BEFORE the `try` opens. The
`CallStackOverflowException` escapes `RunAsync` as a raw CLR exception instead of becoming
the `Data.FromError(...)` the rest of the pipeline expects. Same shape as the v1 finding
on `App.Run`, new site.

Fix: wrap Push in try/catch, return `Data.FromError(new ServiceError(...))` on
overflow.

### MINOR — `[ThreadStatic]` cycle-detection HashSet in `Data.AsT_Impl` and `Variables.ResolveVariablesInPath`

`PLang/App/Data/this.cs:533-534`:

```csharp
[ThreadStatic]
private static HashSet<string>? _resolvingValues;
```

Same pattern at `PLang/App/Variables/this.cs:622-623` (`_resolvingVars`).

`[ThreadStatic]` is per-thread, not per-async-flow. Under `Task.WhenAll` two async branches
can interleave on a single thread pool worker:

- Branch A enters AsT_Impl, allocates the HashSet, adds `"%foo%"`, awaits.
- Branch B continues on the same thread, sees `_resolvingValues != null`, treats itself
  as nested, shares the set.
- Branch B adds `"%bar%"`. Branch A pops `"%foo%"` and clears the set when isCycleRoot.
- Branch B's `try/finally` then `Remove("%bar%")` on a null set or a foreign one —
  best case a stale entry, worst case a NRE depending on ordering.

Failure mode: false-positive `VariableResolutionCycle` errors on perfectly clean
parallel-dispatch goals, intermittent under load. `goal.call` chains under
`Task.WhenAll` are the realistic trigger.

Fix: `AsyncLocal<HashSet<string>?>` — same lifecycle semantics, async-flow safe.

### MINOR — `AsCanonical` partial-match resolves `%!*%` infrastructure refs at build

`PLang/App/Data/this.cs:477` —

```csharp
var interpolated = ctx.Variables.Resolve(strVal);
```

No `skipInfrastructure: true`. `SubstitutePrimitive` fixed exactly this asymmetry in
commit `dd7bf37e`:

```csharp
return ctx.Variables.Resolve(s, skipInfrastructure: true);  // SubstitutePrimitive
```

A partial string like `"depth=%!callStack.Current.Depth%"` flowing through `AsCanonical`
during build still gets the BUILDER's depth baked into the user's `.pr`. Same bug class
as `dd7bf37e`, same fix needed at the AsCanonical site.

(The `IsWalkableContainer` walked-container path goes through `SubstitutePrimitive` and
is fine. Only the bare-string partial-interpolation branch in AsCanonical is exposed.)

### NIT — `DictionaryNavigator` "Count" semantics inconsistent across arms

`PLang/App/Data/Navigators/DictionaryNavigator.cs`:

- `IDictionary<string, object?>` arm (line 36): "Count" check shadows keys — a dict
  literally containing `{"count": "hello"}` returns dict.Count, never `"hello"`.
- Non-generic `IDictionary` arm (line 49): same — "Count" shadows keys.
- `IDictionary<string, T>` arm for JsonObject etc. (line 64-80): walks keys FIRST,
  returns count only if no key match.

So a `Dictionary<string, object?>` with key "Count" can't expose its value, but a
JsonObject with key "Count" can. Three arms, two answers.

Pick one rule. Concrete suggestion: never shadow user keys — only return `dict.Count`
on lookup miss. The `.Count` property is rarely user-meaningful at this layer; user keys
are.

### NIT — Goal frame's `Action.Step` is pinned to `Steps[0]` for the goal's lifetime

`PLang/App/Goals/Goal/this.cs:276-278`:

```csharp
var goalEntryAction = new Steps.Step.Actions.Action.@this { Module = "goal", ActionName = "enter" };
if (Steps.Count > 0) goalEntryAction.Step = Steps[0];
```

Step 5 of a 10-step goal: stepCall.Action.Step = Step5 (correct), goalCall.Action.Step =
Step0 (misleading). The goal frame outlives every step's Push/Pop, so any tag/diff
audited against `Caller.Action.Step` will carry Step0 provenance forever.

Either leave the goal frame's Action.Step null (clearest — it's a synthetic frame, not
tied to one step) or rotate it as steps execute (more bookkeeping; probably not worth
it).

## Verdict

**NEEDS WORK.** Blockers:

- 5 v1 MINORs unaddressed.
- 3 new MINORs from this round.
- The `[ThreadStatic]` finding is the most pressing — false cycle errors under
  parallel dispatch will be confusing and intermittent. The Goal-Push-outside-try
  finding is the next-most pressing because it can crash an entire run on legitimate
  user error (recursive goal). The AsCanonical-infra-resolve finding is highest-impact
  for build-time correctness.

NITs are optional cleanup.
