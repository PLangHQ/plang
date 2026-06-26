# Stage 6: Invariant proof — tripwire, fixture sweep, acceptance

**Goal:** Prove the invariant holds: no Store read ever reaches the wire with a null context, the acceptance test goes green, and `plang build` works fresh and on a cache hit. Turn the design into a guarded, regression-proof state.
**Scope:** The proof and the test fallout. Included: the tripwire, the ~61-fixture sweep, the acceptance test, the build re-validation. Excluded: the downstream LLM-cache code fix (`RestoreFromCache` deletion etc.) and `remove-goalcall` — those are the *next* branch; this stage only confirms the ground is ready for them.
**Deliverables:**
- `data/Wire.cs` (`ReadBody`) — add the tripwire after the no-declared-type throw: `if (_context == null && View == Store) throw` naming the slot + type. Off until Stages 1–5 land, then on; it guards the invariant.
- Test-fixture sweep — a shared helper that always supplies a context (e.g. `TestApp` + its actor context); sweep the ~61 fixtures that built a context-less serializer/app onto it (Runtime ~+28, Types ~+18, Data +8, Modules +4, Wire +3 over a ~23 baseline). These go green *because the behavior is now correct*, not because they were silenced.
- `PLang.Tests/Wire/.../DictTypedEntryRoundTripTests.cs` — un-skip; it is the acceptance test (the dict-of-typed-entries Store round trip) and goes green when Stage 4 lands.
- Confirm the adjacent 4 pre-existing Wire failures (`Properties_RoundTrip_*`, `Deserialize_ShallowNesting`) and the 2 the context-ful read newly exposes — decide fix-now vs track (the `{name,value}` no-type serialization family).
- Re-validate `plang build` fresh + cache-hit (rebuild from clean per the stale-binary rule).
**Dependencies:** Stages 1–5.

## Design

The tripwire is the falsifiable definition of done: with it on, a green suite means no context-less Store read survives. The fixture sweep is mechanical but voluminous — most failures are fixtures that constructed a context-less store, and the fix is to route them through the context-supplying helper, not to edit them one by one.

Keep the framing from the plan: this stage does not fix the LLM cache. It proves context-never-null + one-read-path holds, so the cache fix (next branch) becomes the trivial `settings.Set/Get` round trip the coder's seed describes.

Use the canonical test commands (rebuild from clean before claiming any `plang --test` result; `cd Tests` first; never delete `.build/`).

Full detail: `plan.md` "Acceptance"; `plan/mime-and-verify.md`.

## Tests to remove (not adapt)

These pin behavior the branch deletes — they assert a state that becomes unreachable, so they go rather than getting a context threaded in. Distinct from the ~61-fixture sweep above (those use `ContextLessFallback` as a *tool* and get a context wired in; these test the context-less behavior *itself*).

Definite removes:
- `Wire/App/Serialization/WireConverterSigningTests.Deserialize_SignatureLayer_NoActorContext_FailsClosed` — asserts the `_context == null` fail-closed branch (`SignatureVerifyContextMissing`), deleted in Stage 4.
- `Runtime/App/Goals/PathTypingTests/GoalPathTypingTests.PathJsonConverter_LeavesContextNullAtDeserializeTime` — its point is "the context-less converter produces a Path with Context=null"; the converter is context-ful after Stage 4.
- `Data/App/DataTests/DataTests.Context_DefaultsToNull` — asserts `new Data(...).Context` is null; contradicts the invariant.

Review at Stage 3 (premise is the deleted construct-then-stamp / no-context model — remove or rethink depending on the final ctor shape):
- `Data/App/VariablesTests/VariablesTests.PLangContext_ExistingData_GetsContext` — the "Data context-less before Context creation, stamped after" lifecycle; at least the `IsNull`-before assertion dies.
- `Runtime/App/SingularNamespaces/NullabilityTests/NonNullInvariantTests` — `ClrType_OnUnstampedDomainType_ReturnsNull`, `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard`, `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext`. All premised on a value with no Context. If context-less construction becomes illegal in Stage 3, they can't exist; if a context-free path survives for primitives, keep and adjust the assertion.

Each removal is a place where a test's *reason for existing* was the behavior we deleted — leaving it would mean re-introducing the dead path just to keep the test compiling.

## You own this

The shape of the shared test helper and which adjacent failures to fold in vs track are yours. The contract: the tripwire is on and green, `DictTypedEntryRoundTripTests` passes, and `plang build` works fresh and cache-hit.
