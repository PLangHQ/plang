## 2026-05-28 — Primitive vocabulary discussion captured (not decided)

Ingi raised the broader question: PLang's primitive set is not well-defined. He wants TimeSpan, DateTimeOffset (no DateTime), `date` as DateOnly, `time` as TimeOnly, and `image`/`video`/`code`/… as picks the LLM can make. Conversation surfaced that the high-level kind table he remembered isn't deleted — it's `app/formats/this.cs` (30+ Kinds). It just lives next to `app.types.Primitives` instead of being part of it.

Three concepts are colliding under "primitives" today: wire-level CLR types, named LLM picks, and format kinds. Three locations, no single owner — which is why DateTimeOffset is half-registered (`IsPrimitive` accepts it but the name table has no entry).

Open question for Ingi to settle: do we ship `number` first then carve the broader OBP shape later (two arcs), or do we widen this branch to introduce the `app/types/primitive/<name>/this.cs` shape and slot `number` into it as one folder among many (one arc)? Writeup at [plan/primitive-vocabulary.md](plan/primitive-vocabulary.md). No decisions made; nothing in the existing plan changed.

Stage status (unchanged):

| Stage | What | Status |
|-------|------|--------|
| 1 | `app/types/number/` class — storage, parse, operators, IBooleanResolvable | pending |
| 2 | `app/environment/number/` settings home + goal overlay + NumberPolicy resolver | pending |
| 3 | `math.*` retype (canary at `math.add`, then sweep) | pending |
| 4 | Primitives + catalog registration | pending |
| 5 | Compile.llm decimal-literal rule (lands first as precursor) | pending |

## 2026-05-27 — Number type design (plan written, stages pending)

Designed `number` as a sibling-to-`path` category type: real C# class at `app/types/number/this.cs` carrying tagged-union storage (`long _i; decimal _d; double _f; NumberKind _kind`) covering int / long / decimal / double / float. Operators are policy-free (always lenient); a `NumberPolicy` struct with two axes (`Overflow`, `Precision`) and three scopes (app / goal / step) drives configurable behavior. Step-scope is the per-action parameter; app-scope lives on `App.Environment.Number`; goal-scope is a lazy overlay on `Goal` mirroring the existing `Events` pattern.

Key design forks settled in conversation with Ingi:

1. **Rejected decimal-only storage.** Would break IEEE-754 semantics (NaN, Infinity, scientific notation). PLang is general-purpose; can't pick favorites between currency and science. The `number` type spans all numeric kinds.
2. **Rejected builder's "boxed object + Kind" sketch.** Double-allocates (class header + boxed primitive). Picked tagged union with explicit slots — no boxing, strongly typed at every reach.
3. **Rejected architect-imposed promotion rules.** Arithmetic policy is developer-configurable via settings; defaults are lenient, strict mode is one step away. C# operators stay deterministic-lenient; math handlers consult settings and call the policy-aware overload.

Stage status:

| Stage | What | Status |
|-------|------|--------|
| 1 | `app/types/number/` class — storage, parse, operators, IBooleanResolvable | pending |
| 2 | `app/environment/number/` settings home + goal overlay + NumberPolicy resolver | pending |
| 3 | `math.*` retype (canary at `math.add`, then sweep) | pending |
| 4 | Primitives + catalog registration | pending |
| 5 | Compile.llm decimal-literal rule (lands first as precursor) | pending |

Next steps: carve stage files. Order is 5 → 1 → 2 → 3 → 4.

See [plan.md](plan.md) for the spine and design narrative; [plan/storage.md](plan/storage.md) and [plan/policy.md](plan/policy.md) for the deep dives.
