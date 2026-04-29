# v3 review summary — what runtime2's merge revealed

v3 was structurally approved and the work was moving forward when Ingi pushed a large merge into `runtime2` (Builder V2, plus ~50 commits of runtime/generator iteration accumulated since the branch split at `c51dcebb`). He asked me to pull the merge in and verify whether v3 still applies.

## What the merge added (the relevant chunk)

Around `Data` and the generator, runtime2 accumulated a tightly related set of patches:

- `Data._resolved` cache flag
- `Data._rawValue` preservation
- `Data.ResetResolution()` method
- `data.ResetResolution()` call emitted in the generator's parameter-Data construction
- `IsDeferredActionTemplate` carve-out in `Data.Value`'s lazy getter
- A new finally block in the generated `ExecuteAsync` (callstack pop + Step/Goal/Event restore + frame snapshot) — duplicated the existing finally and broke the build until `00fd25e3` removed the dup
- `__SnapshotParams()` per-property snapshot, attached to errors via `ParamSnapshot`/`Error.Params`

Plus orthogonal work: `Action.IsModifier`/`Description`/`ModuleDescription`, `Variables.Set` reference-aliasing semantics, `Action.RunAsync` Handled-override path, `IEvent` capability, callstack at action level.

## The pattern under the resolution-related patches

The commit history tells a four-step retreat around one decision:

1. `f9008255` — *"Lazy variable resolution in Data.Value for list/dict parameters"* (put resolution on the getter)
2. `4c22c895` — *"Replace _resolved cache with NeedsResolution flag for .pr parameter Data"* (first cache-invalidation attempt)
3. `b0014c91` — *"Cache lazy property resolution"* (re-added the cache because the flag-only version recomputed too much)
4. `fedd44e6` — *"Fix shared parameter Data mutation + reset backing fields per execution"* (had to bust the cache from outside, because the cache lives on shared input data)

The comment in today's generator narrates the wound directly:

> The pr's Parameter Data is shared across action executions. Its resolution cache (set on first .Value access) must be cleared each call so %var% references re-resolve against the current variables. Without this, e.g. llm.query's Messages[content="%goalForLlm%"] kept the first resolution forever — sub-goal builds got the parent's rendered prompt.

Each layer is a tighter wrap around the same wound: **resolution was given a side effect on a shared object's getter**, and every patch since has been a different way to manage that side effect's lifetime.

## v3's blind spot

v3's Phase 2 said: *"Move ResolveDeep's logic into Data.Value's lazy getter, gated on NeedsResolution=true."*

That's the same decision. v3 would inherit the same wound — same need for cache invalidation, same need for `IsDeferredActionTemplate`-style carve-outs to keep the deep walk from going into things it shouldn't, same risk in the "Data.Value lazy getter side effects" line of v3's risks section.

## Ingi's pushback

When I presented this — *"the runtime2 patches are plaster on the resolution-as-side-effect-of-`.Value` decision; v3 makes that same decision in different clothes"* — and proposed the alternative (resolution lives in `Data.As<T>(context)`, `.Value` stays read-only and stateless, no caching on Data, no Reset, no carve-out), Ingi accepted the frame.

On the open question — *"is there any caller that reads `.Value` on parameter Data expecting the resolved form?"* — his answer was: *"I don't know. But if it is, then it's not valid implementation. Let it break and find out."*

That's the v4 stance. v4 doesn't pre-grep for offenders. The contract change is: `.Value` on parameter Data returns raw. Anything that breaks under that contract was depending on a side effect it shouldn't have been.

## What this changes for the v4 plan

- **Phase 2 simplifies.** The plan was "move ResolveDeep into Data.Value, keep the cache, gate on NeedsResolution." It becomes: "write `Data.As<T>(context)` that walks + substitutes + converts; delete `Variables.ResolveDeep`; delete `_resolved`, `_rawValue`, `ResetResolution`, `IsDeferredActionTemplate`. `.Value` is a read-only property."
- **Phase 3 simplifies.** The generator stops emitting `data.ResetResolution()` and the `__StripPercent`/`__TryConvert` family — both were in service of the cached-resolution model.
- **Phase 4 grows by one method.** `ActionProperty.EmitSnapshotEntry(StringBuilder)` alongside `EmitProperty`. `__SnapshotParams` becomes a per-property contribution sum.
- **Phase 6 changes.** v3's "slim As<T>" step is wrong under v4 — As<T> is now where resolution lives, not slimmed. Phase 6 instead just confirms `Variables.ResolveDeep` is gone.
- **Risks section shrinks.** "Data.Value lazy getter side effects" and "Cycle: Data.Value → Variables.Get → returns Data → reads its Value" both disappear.

## Other items from runtime2 that survive untouched

These are orthogonal to the resolution question — kept as-is:

- `Action.IsModifier`, `Description`, `ModuleDescription` — additive metadata, no impact
- `Variables.Set` reference-aliasing — about variable storage identity, not parameter resolution
- `Action.RunAsync` Handled-override path — about mock/event interception, not resolution
- `__SnapshotParams` and `ParamSnapshot` — useful diagnostic; v4 keeps it but its implementation simplifies because raw value is now trivially accessible via `.Value`

## Items still pending from v3

- The matrix from v3's Phase 0 was never built (v3 was approved but no coder picked it up). v4 inherits Phase 0 unchanged in shape; matrix coverage list expands by three cases (`DataWrappedActionList`, `ModifierAction`, `SnapshotOnError`).
- `[VariableName]` removal still pending. `11386f1c` ("Wrap remaining action properties in Data<T>") closed most of the migration path; the last sweep is smaller now.
