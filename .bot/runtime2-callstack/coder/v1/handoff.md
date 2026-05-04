# Handoff — runtime2-callstack — coder/v1

## Where we are

The big architecture refactor is done and **all C# tests pass (2593/2593)**.
The build pipeline is unblocked end-to-end, and **12 of 16 PLang test goals pass**.
Branch is on commit `81dc910c`, pushed to `origin/runtime2-callstack`.

## What's still failing (and why)

Three real failures + one intentional stub:

1. **`CauseLink.test.goal`** — `Expected: "error" Actual: (null)`
   Tests that inside an `error.handle` recovery body, `%!callStack.Current.Cause`
   points at the errored Call. The test goal:
   ```
   - throw error "boom", on error call CaptureCause
   - assert %causeModule% equals "error"
   ```
   `CaptureCause.goal` does `set %causeModule% = %!callStack.Current.Cause.Action.Module`.
   The C# test `CauseLinkageTests.Push_WithCause_SetsCauseField` proves Cause is
   wired correctly — the issue is in PLang navigation: `%!callStack.Current.Cause`
   may be null at the assertion point because the recovery dispatch happens via
   `App.Run(action, context, cause)` but the path threading `cause` through
   `error.handle.Wrap` → `RunRecoveryWithErrorScope` → `RunRecovery` → `action.RunAsync(context, cause)`
   needs verification end-to-end. Add a print in `CaptureCause.goal` to see what
   `%!callStack.Current.Cause%` actually returns.

2. **`Audit.test.goal`** — `Expected: True Actual: (null)`
   Tests that 4 errors (3 handled in foreach + 1 unhandled) accumulate to
   `%!callStack.Audit.Count == 4`. Test uses:
   ```
   - foreach %items%, call ThrowItem item=%item%, on error set %finalCaught% = true
   ```
   The LLM mapping for "foreach with on-error" is tricky. The assertion gets
   `null` for `%finalCaught%` — likely because the on-error wraps the foreach
   itself, and when it fires, `%finalCaught%` is set in a scope that doesn't
   propagate. Try restructuring as separate steps or just inspecting
   `%!callStack.Audit.Count%` directly.

3. **`TagBareLabelWritesTrue.test.goal`** — `Expected: "true" Actual: (null)`
   Strange: `TagWritesPairsOntoCurrentCall` PASSES but the bare-label form
   fails. The PLang code is:
   ```
   - debug.tag "manual-checkpoint"
   - assert %!callStack.Current.Caller.Tags."manual-checkpoint"% equals "true"
   ```
   Possibilities:
   - LLM mapped the bare-label form to `Pairs={"manual-checkpoint":"true"}`
     but `Tags."manual-checkpoint"` access has trouble with the quoted segment
     in dot-path navigation.
   - Check `cat .build/tagbarelabelwritestrue.test.pr` — see if Pairs vs Label
     path. If Pairs, the tag itself works, the assertion path navigation
     through `."manual-checkpoint"` (with quotes) is the issue. Try
     `%!callStack.Current.Caller.Tags["manual-checkpoint"]%` (bracket form) instead.

4. **`HandledFlagFalseWhenRecoveryFails.test.goal`** — `Error: not implemented`
   Intentional stub. Documented contract for now; needs nested error.handle
   that the builder maps reliably (currently doesn't). C# coverage in
   `CallStackAuditTests` pins the underlying flag wiring.

## Build pipeline status (now working)

The big foundation fixes that unblocked everything:

1. **`Data.AsT_Impl` action-destination carve-out** (`ceab763f`) — was
   unconditionally skipping `%var%` substitution, even when `raw` was itself
   a `%var%` reference.

2. **TypeMismatch error message** (`4ce7262b`) — uses FullName, value preview,
   and `%var%` hint so the next person sees the cause without a debugger.

3. **DictionaryNavigator + Variables.SetValueOnObject** (`10d0216c`) — both
   only handled `IDictionary<string, object?>` / non-generic `IDictionary`;
   JsonObject's `IDictionary<string, JsonNode?>` fell through. Read returned
   reflection junk; write *replaced* the live JsonObject.

4. **`SubstitutePrimitive` preserves `%var%` references** (`786782af`) — was
   nulling LLM-emitted parameter values when the variable was unset.

5. **`SubstitutePrimitive` skips `%!*%` infrastructure refs at build**
   (`dd7bf37e`) — was baking the BUILDER's `%!callStack.Current.Depth%` (4)
   into the user's `.pr`.

6. **Goal pushes a Call frame; tag attaches to caller** (`81dc910c`) — gives
   step actions an enclosing scope so tags persist across step boundaries.

## Files changed in this session

- `PLang/App/Data/this.cs` — three fixes (AsT_Impl carve-out narrowed,
  SubstitutePrimitive preserves %var% and skips %!*%)
- `PLang/App/Data/Navigators/DictionaryNavigator.cs` — generic IDictionary<string, T> arm
- `PLang/App/Variables/this.cs` — generic IDictionary<string, T> SetValueOnObject arm,
  JsonNode coercion via SerializeToNode
- `PLang/App/Utils/TypeConverter.cs` — improved TypeMismatch message
- `PLang/App/Goals/Goal/this.cs` — Goal pushes a Call frame
- `PLang/App/modules/debug/tag.cs` — tag writes to Current.Caller
- 16 `Tests/App/CallStack/*.test.goal` (real assertions, helper goals)
- 8 helper goals: `Inner`, `CycleA`/`CycleB`, `ChainOuter`/`ChainMiddle`/`ChainThrows`,
  `ThrowItem`, `CaptureCause`, `OuterRecoveryWithInnerThrow`, `SlowGoal`
- `Documentation/v0.2/debug.md` — `--debug={callstack:...}` reference
- 6 new C# test files (95 tests across 15 files for the callstack contract,
  + JsonObjectNavigationTests + TypeMismatchMessageTests)

## Recommended next steps

1. **CauseLink**: add a `write out` step inside `CaptureCause.goal` to inspect
   what `%!callStack.Current.Cause%` actually contains during the recovery body.
   The dispatch wires through `App.Run(action, context, cause)` — verify
   `cause` lands on the Call.

2. **Audit**: simplify the test goal — replace the foreach on-error pattern
   with explicit error.handle steps. The architect intent is just "4 errors
   logged in Audit"; an outer try/catch-style structure is enough.

3. **TagBareLabelWritesTrue**: inspect the `.pr` to see if the LLM mapped to
   Pairs or Label form, and try bracket access syntax for the quoted key.

4. **HandledFlagFalseWhenRecoveryFails**: leave as stub — C# tests cover the
   contract; this would need builder support for nested error.handle that
   doesn't exist yet.
