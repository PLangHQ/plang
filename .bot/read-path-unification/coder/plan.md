# Read-path unification — coder handoff

**Authoritative plan:** `../architect/v1/plan.md` (full design, error model, leaf-trace, per-phase demolition worklist, reader-coverage, settled questions, OBP table). Your v1 corrections are folded in (see `response-to-architect-v1.md`): tuple `(item?, Error?)` not throw, generic reader **delegates** to `type.Convert`, `IsFinal` + `Cacheable` both kept, properties lazy, `View` injected for the signature reader, value-ctor retirement moved to the last phase.

## You own this

Every line reference and code shape in the plan is a **suggestion** grounded in the current source — and line numbers drift, so re-verify before cutting. You own the final shape. Keep the two invariants and bring any structural change back here.

## The two invariants

1. **No value parse at load.** The read captures the `value` slot — and every property's value — as raw bytes (`IReader.RawValue()`, no DOM). The single parse happens per value in `source.Value()`.
2. **No type-discrimination fork.** Envelope and value choices are registry dispatch (`@schema`, `(type, kind)`); the only value-path branch is the narrow (F2), keyed on `Cacheable`.

The center: **`source`** (the one lazy carrier) + **`app.type.Create(source)`** (the one door → `App.Type.Reader(source).Read(source)`, total registry, returns `(item?, Error?)`). Read is `read(IReader)` — format-agnostic, mirror of `value.Write(IWriter)`; `json` is one `IReader`.

Open items still needing decisions are flagged in the plan (`@schema` registry placement; Phase 6 value-ctor scope).
