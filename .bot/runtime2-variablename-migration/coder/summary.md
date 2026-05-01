# coder summary — runtime2-variablename-migration

## v1 (2026-05-01)

Verified architect's Phase 0 claim — `Data.As<T>(ctx)` does propagate `.Name = "x"`
from `Value="%x%"` slot Data, both for the unset and live-variable cases. Four tests
pass in `PLang.Tests/App/DataTests/VariableSetNameResolutionTests.cs`. Source generator
confirmed to call exactly that path for `Data<T>` properties.

But: the bare-name case (`value="x"` without `%`) silently routes to the slot key
"Name" under the new path. Existing `[VariableName]` / `__StripPercent` path handles
both forms natively. Ingi declined the migration on robustness grounds — `[VariableName]`
stays canonical for write-target slots; Legacy emission stays permanent.

Sent the architect a v2 plan request via `v1/architect-handoff.md`. Migration scope
reduces to read-site cleanup only (~16 handlers). Tester / test-designer have nothing
to pick up until v2 lands.

Details: [v1/summary.md](v1/summary.md)
