# Good to Know — index

This file was a catch-all (~70 sections). It's been decomposed into focused topic docs;
each section moved to the doc named beside it. References elsewhere that name a section by
title still resolve here — search the title in the list below.

## Topic docs
- [`conventions.md`](conventions.md) — Conventions — Folders, Namespaces, Goal Resolution
- [`test-architecture.md`](test-architecture.md) — Test Architecture
- [`builder-runtime.md`](builder-runtime.md) — Builder & Runtime Notes
- [`data-internals.md`](data-internals.md) — Data Internals & Source Generator
- [`wire-serialization.md`](wire-serialization.md) — Wire & Serialization
- [`type-system.md`](type-system.md) — Type System Notes
- [`bans.md`](bans.md) — Production Guardrails — Bans & Limits
- [`code-modules.md`](code-modules.md) — app.module.code — Pluggable Implementations
- [`obp-smells.md`](obp-smells.md) — OBP naming, shape smells (worked examples), variant design
- [`object_pattern_formal.md`](object_pattern_formal.md) — the OBP law (philosophy + 9 rules)

## Section → doc
- Folder Structure & Namespaces → `conventions.md`
- Goal Resolution & Relative Paths → `conventions.md`
- Event Override (skipAction) → `builder-runtime.md`
- Test Architecture → `test-architecture.md`
- Mock Module Architecture → `test-architecture.md`
- Libraries Replaces ActionRegistry → `code-modules.md`
- GoalFirst Retry Behavior → `builder-runtime.md`
- Error Reporting — When to use what → `builder-runtime.md`
- Sub-Step Execution — Condition-Gated Skipping → `builder-runtime.md`
- Condition Orchestration — if/elseif/else in One Step → `builder-runtime.md`
- Data.Compare — Structural JSON Diff → `data-internals.md`
- Security Hardening — Defense-in-Depth Limits → `bans.md`
- [Sensitive] Attribute — Two-Mode Serialization → `wire-serialization.md`
- Domain types ride the wire as property bags, not bespoke JSON converters → `wire-serialization.md`
- IdentityData — Data Subclass → `data-internals.md`
- %MyIdentity% — DynamicData Registration → `code-modules.md`
- app.module.code — Pluggable Module Implementations → `code-modules.md`
- Condition Evaluation — Type Normalization → `builder-runtime.md`
- Signing Module — Architecture → `code-modules.md`
- Signing — Lazy Verification on Property Access → `code-modules.md`
- ILlm — LLM Implementation in app.module.code → `code-modules.md`
- IHttp — HTTP Implementation in app.module.code → `code-modules.md`
- IBuilder — Builder Implementation in app.module.code → `code-modules.md`
- TransportPropertyFilter — [In] / [Out] Attributes → `wire-serialization.md`
- ISettings → IConfig Rename → `code-modules.md`
- IConfigure\<T\> — Build-Time Defaults Pattern → `code-modules.md`
- Action Modifiers — Fold + Grouping → `builder-runtime.md`
- GoalCall — Clone, Never Mutate → `builder-runtime.md`
- Modifier Hardening Backlog → `builder-runtime.md`
- Test Module — Cross-Cutting Invariants → `test-architecture.md`
- Source Generator — OBP shape and incremental cache → `data-internals.md`
- Action property kinds (PLNG001 build-time gate) → `data-internals.md`
- `app.variable.Variable` — the variable-name carrier → `data-internals.md`
- `Data.As<T>` — cycle, depth, ServiceError contract → `data-internals.md`
- `[Sensitive]` masking in ParamSnapshot → `wire-serialization.md`
- `Action.GetParameter` — pure parameter lookup → `data-internals.md`
- `ICodeGenerated.SnapshotParams` — default-impl interface method → `data-internals.md`
- Data identity preservation — `As<T>` four wrap rules → `data-internals.md`
- `AsCanonical` — plain `Data` slots return the live variable → `data-internals.md`
- `Variables.Set` — events follow the name, Properties stay with the Data → `data-internals.md`
- `variable.set` is the sole binding-mint site → `data-internals.md`
- String-not-iterable — `IsPlangIterable` / `IsPlangAssignable` → `data-internals.md`
- JsonNode / JsonArray dispatch in `TypeConverter` → `data-internals.md`
- Lazy `Data.Signature` is ICallback-only — the carve-out → `data-internals.md`
- `RestoredFrame` is a surrogate, not a `Call.@this` → `data-internals.md`
- `Errors.Push` sets `error.App = this.App` for callback materialisation → `data-internals.md`
- System.IO Is Banned in Production C# (use `path.@this`) → `bans.md`
- Console.* Is Banned in Production C# → `bans.md`
- Action `Run()` returns are typed — and the `Data<T>` implicit-operator footgun → `data-internals.md`
- Truthiness — `IBooleanResolvable` and async condition evaluation → `data-internals.md`
- Per-action LLM teaching lives in markdown, not attributes → `builder-runtime.md`
- Build()-time type stamping — `IClass.Build()`, `(type)` hints, and `BuildWarning` → `builder-runtime.md`
- `Serializers/ISerializer` returns `Data` — no throws → `wire-serialization.md`
- Multi-segment serializer extension matching → `wire-serialization.md`
- `IExitsGoal.ShouldExit()` — Value-side opt-out for resolved sentinels → `type-system.md`
- Recursion guards belong on the value, not on a parallel context layer → `data-internals.md`
- Typed values — `app/type/<name>/`, per-(type, format) renderers, `type` + `kind` as separate fields → `type-system.md`
- `app.X` is the collection node — `[name]` / `.list` / `.current` → `type-system.md`
- Producer-stamping invariant — `Data.Type` propagation → `type-system.md`
- `type.@this.Null` — non-null sentinel on `Data.Type` → `type-system.md`
- OBP Naming Principle / Smell Checklist / Variant Design → `obp-smells.md`

## Contradiction sweep (2026-05-31, confirmed with Ingi)
Resolved:
- ✅ **PathData** — confirmed gone (no source). The dead "PathData — Data Subclass in `app/filesystem/`" section was removed from `data-internals.md`.
- ✅ **`app/types/` → `app/type/`** — plural fixed in `type-system.md` (and this index).
- ✅ **Permission/Verb namespace** — confirmed `app.type.path.permission` (`permission/verb/{Read,Write,Delete,Execute}.cs`); corrected inline in `obp-smells.md`.
- ↪ **Event rename (`GoalStepEvents`/`ActionEvents` → `Lifecycle`, `EventList` → `Bindings`)** — still pending; moved to `Documentation/Runtime2/todos.md` so the intent isn't lost.

Still pending (the broad pass):
- General pre-rename PascalCase/plural names (`app.Goals`→`app.Goal`, etc.) inside moved bodies — safe from CLAUDE.md, do in a focused sweep.

