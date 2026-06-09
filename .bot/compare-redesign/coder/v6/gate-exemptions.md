# Stage 2.1 gate — `.Materialize()` exemptions in app/module (flag for architect)

The architect's gate wants `grep .Materialize() PLang/app/module → zero`. These sites legitimately
stay `Materialize()` (genuinely-sync, provably no-I/O, can't reach a lazy reference) — like the
`System.IO` PLNG gate's `app.type.path.**` exemption. Flagging per the "bring the judgment back" rule.

## Genuinely-sync framework surfaces (can't be async)
- **`signing/Signature.cs` — `JsonConverter.Write` override** (3 sites). Serialization reads the
  already-materialised backing; `Write` is a sync framework contract. (Architect's rule-3 / bucket-D.)
- **`ui/code/Fluid.cs` — `IFileInfo`/`IFluidIndexable` sync members** (CreateReadStream, Length, the
  index accessors). Fluid's template-engine contracts are sync; params materialise up-front at SetValue
  per Stage 2. (Confirm each.)

## Diagnostic / display surfaces (sync, like ToString — never on a runtime path)
- **`debug/this.cs`** (~10 sites) — verbose-value formatting, `Watch`/trace `sb.AppendLine`,
  reflection-for-display (`GetType().GetProperty(...)`), and trace-path probes that already use
  `GetAwaiter().GetResult()`. These render values for debug output, gated on `--debug`; they never sit
  on a runtime content path. Equivalent to `ToString`.

## Build-time meta-validation (LLM output, never runtime references) — FLIPPED anyway
- `builder/validateResponse.cs` — validates the LLM's emitted params at build; never lazy references.
  Routed through the door anyway (harmless, sync-completing) rather than carve an exemption — DONE.

## Build-time meta-handlers (process the LLM's build output, never runtime references)
These read **build params/steps** (the LLM's emitted JSON being validated/normalised), not runtime
variables — so they can never reach a lazy `file`/`url` reference and can't bypass Stage 3.
- **`builder/code/Default.cs`** (~19 sites) — `Merge`/`EnrichResponse` (`IBuilder` methods → flipping
  cascades the interface for no benefit), `NormalizeParameterTypes`/`ToStepList`/`ToGoalCall`/
  `RenderActionFormal` (sync statics) reading `p.Materialize()` / `action.Step.Materialize()`.
- `builder/validateResponse.cs` — **same category but I flipped it** (contained: Run + 2 statics, no
  interface). Either is defensible; flagging the principle so the architect picks one rule:
  build-meta = exempt, OR flip-where-contained. (I flipped the contained one, exempt the cascading one.)

## Deferred to other stages (do NOT flip in 2.1a)
- `list/sort.cs` (4), `type/list/this.cs` — the **two-phase sort** is Stage 6's mechanism.
- `condition/Operator.cs` (sites), `data/Compare.cs` — the **old comparison mediator**, deleted in Stage 6.

## Remaining scattered sync-helper/predicate/service sites (review per-site: flip-cascade vs exempt)
- `error/handle.cs` — `MatchesError` (sync predicate reading `Key`/`Message`/`StatusCode` error-filter
  params; build-set config, no-I/O).
- `http/code/Default.cs` — `Configure` (IHttp method, reads `Default` bool config; flipping cascades IHttp).
- `identity/code/Default.cs` — `((IKey)__keyR.Materialize())` ([Code] service resolution, in-memory).
- `code/this.cs`, `code/this.Snapshot.cs` — service/assembly resolution (in-memory).
- `cache/wrap.cs` — `Sliding` bool config in the modifier-setup `Wrap` (returns a Func).
- `llm/code/OpenAi.cs` (dataUri/cached reads), `llm/query.cs` (messages validation), `mock/intercept.cs`
  (`ResolveParamValue` in a matcher lambda) — sync helpers; some read content that *could* be a reference
  → flip the helper async + caller, OR confirm no-I/O. Left for the per-site judgment.

## Recommendation
Gate as: `grep -rn "\.Materialize()" PLang/app/module PLang/app/variable/navigator` excluding
`Signature.cs`, `Fluid.cs` (the IFileInfo block), `debug/this.cs`, and the Stage-6-owned `sort.cs`/`Operator.cs`.
Or move the exempt surfaces' reads behind a named `internal` accessor so the grep is clean. Architect's call.
