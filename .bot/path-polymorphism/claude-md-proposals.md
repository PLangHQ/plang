## coder — v3 — 2026-05-22
**Target:** /CLAUDE.md
**Why:** v3 (codeanalyzer F3) made a value's boolean meaning its own
responsibility and turned the condition-evaluation pipeline async. Future module
and type authors need to know `Data.ToBoolean()` is no longer the place to add
type-specific truthiness, and that condition/assert evaluation is async — both
are easy to get wrong without a canonical note.
**Proposed change:**
Add under "## Runtime2 Conventions":

- **Truthiness — `IBooleanResolvable`**: a value's boolean meaning belongs to
  the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/
  0/"" falsy, everything else truthy); do **not** add type-specific cases to it.
  A type that knows its own truthiness implements `app.data.IBooleanResolvable`
  (`Task<bool> AsBooleanAsync()`) — `path` does, where truthiness means "does
  the resource exist". `Data.ToBooleanAsync()` dispatches to it. Because that
  probe can be I/O (HTTP HEAD for the http scheme), the condition-evaluation
  pipeline is **async**: `IEvaluator.Evaluate` returns `Task<data.@this>`,
  `Operator.Evaluate` is `Func<data.@this?,data.@this?,Task<bool>>`, and
  `assert.IsTrue/IsFalse` are async. A new operator or evaluator must `await`.

## coder — typed-returns sweep — 2026-05-23
**Target:** /CLAUDE.md (Runtime2 Conventions section)
**Why:** Action handlers are mid-migration from `Task<Data>` to `Task<Data<T>>`. ~50 of ~100 actions typed; the rest need a follow-up. The pattern + footgun must be discoverable so the next bot doesn't reintroduce the bare-Data shape or hit the silent double-wrap.
**Proposed change:**
```markdown
- **Action `Run()` returns are typed**: every handler declares its return type via the method signature — `Task<Data<T>>` for concrete T, `Task<Data<object>>` for genuinely polymorphic returns (catalog renders as `data`), bare `Task<Data>` only for actions that produce no value (catalog renders no `→ returns` line; the Compile.llm rule treats `write to %x%` after them as invalid). Reflection in `Modules.Describe()` reads the signature; `action.@this.ReturnTypeName` carries T's PLang name; `stepActionDetails.template` renders `→ returns T` after the parameter block; the compile LLM uses that T as the trailing `variable.set`'s `Value` type-annotation (no separate `Type=` param — the `Data<T>` wrapper carries the type).
- **Footgun — `Data<T>` implicit operator** (`@this<T>(T value)` in `PLang/app/data/this.cs`): when `T = object` and the source value is itself a `Data` subtype, the operator silently wraps it (`Data<object>{ Value = Data<bool>{...} }`) instead of passing through. Bites methods declared `Task<Data<object>>` whose body returns a base `Data` or `Data<U>`. Mitigations: (a) explicit `data.@this<object>.Ok(value)` factory call, never `return innerDataInstance;`; (b) for polymorphic actions whose body genuinely forwards a `Data` (goal.call, llm.query, condition.if), stay on bare `Task<Data>` until a `Data.As<T>()` passthrough or a `Data<T>.From(Data source)` helper lands.
```
