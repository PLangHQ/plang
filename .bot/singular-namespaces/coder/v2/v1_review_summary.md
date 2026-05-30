# Tester v1 review of coder v1 — summary

7 findings; v2 addresses all.

- **F1 (CRITICAL)** — Stage 2 contract tests inverted; non-null invariant deferred. Tester wanted the back-refs flipped, the `?.` defensiveness stripped, and the producer-stamping bugs surfaced.
- **F2 (CRITICAL)** — Stage 4 builder golden was a tautology (`schema.ToJson() != null`). No real byte-diff. The architect called this "the gate" for the Entry fold.
- **F3 (MAJOR)** — `DataType_OnStampedData_ResolvesViaRegistry_NotStaticFallback` asserted on `int` — both registry and static fallback return `typeof(int)`, so the path wasn't actually distinguished.
- **F4 (MAJOR)** — `DataTypeReadsEntity.test.goal` body was a plain variable round-trip, never touched the type entity. Would have passed even if Stage 4 broke.
- **F5 (MAJOR)** — `ChannelIndexMissThrows.test.goal` tested a null channel, not a registry index-miss; assertion was `equals true`, never checked `Error.Key`/`StatusCode`.
- **F6 (MAJOR)** — `BuilderValidate_CallsBuildOnEachAction_InOrder` flake — `BuildOrdered.InvocationLog` is a shared `static` list, `Clear()`'d in `[Before(Test)]`; TUnit parallel races the Clear against the assertion.
- **F7 (MINOR)** — `ChannelWriteThroughAccessor.test.goal` was a no-assertion smoke test (`write out "…"` only).

Two structural decisions emerged during F1 that broadened scope:

1. **Data.Context flipped non-null.** The architect's stage 2 plan listed "the 9 Context fields non-null." `Data.Context` was the last one still nullable. Flipped via `= null!` so the public surface is non-null and `?.Context` chains go away. Internal `_context == null` defensive guards stripped where they were dead, kept in `EnsureSigned` (a real producer-stamping contract throw).
2. **Type entity has a `Null` sentinel.** Instead of `Data.Type` returning `null` for value-less Data, it returns `type.@this.Null` — a primitive-shaped entity with `IsNull = true` and `ClrType = typeof(object)`. Wire converter skips emission for the sentinel so the on-wire shape is unchanged. Public `Data.Type` is now non-null end-to-end. The Type setter absorbs the "Null means clear my explicit type" rule, so call sites copy `source.Type` unconditionally.
