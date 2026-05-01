# Coder/v1 plan — verify Phase 0 claim, decide on migration

## Task

Architect's plan claims `Data.As<T>(ctx)` on a slot Data with `Value="%x%"` returns a
Data whose `.Name == "x"`, sourced from `TryFullVarMatch`. Ingi doesn't trust the claim —
he wants real proof before any migration starts.

## Approach

1. Read `PLang/App/Data/this.cs` (As<T>, AsT_Impl, TryFullVarMatch, ConstructWrap, WrapAs).
   Trace the code path manually.
2. Write Phase 0 tests as the architect specified. Run them.
3. Verify the .pr serialized shape matches what the test fixture constructs (`{name:"Name",
   value:"%x%"}`).
4. Verify the source-generator emission for `Data<T>` properties calls `As<T>(Context)` on
   the resolved slot Data (so the unit-level proof transfers to the real handler path).
5. End-to-end: run an existing PLang test that exercises `set %x% = "hello"`.
6. Report findings to Ingi. Hand off back to architect or proceed to Phase 1 per his call.

## Artifacts

- `PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs` — four tests covering the
  full claim (var missing, var exists, bare name negative, round-trip).

## Outcome

All tests pass. Mechanism works as architect described. But Ingi flagged that the bare-name
case (LLM emits `"x"` not `"%x%"`) silently routes to slot key "Name" — a regression vs the
existing `[VariableName]` path which uses `__StripPercent` and handles both forms.

**Decision: keep `[VariableName]`. Send architect a v2 plan request.**

See `architect-handoff.md` for the full handoff.
