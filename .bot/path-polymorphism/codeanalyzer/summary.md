# codeanalyzer — path-polymorphism

**Version:** v4

## What this is

`path-polymorphism` makes PLang's `path` type scheme-polymorphic and has
since accumulated downstream typing work. v4 reviews two large landings on
top of the v3-clean foundation:

1. **Coder v6** — builder-regression fixes: slash-qualified `goal.call`
   resolution (`BuildGoal/Start` → `BuildGoal/.build/start.pr` with caller-
   ancestor walking), `LoadFromFile` leaf-match for slash names, inverted
   `File.Exists` in `builder/this.cs`, optional `Actions` filter param on
   `builder.actions`, two structural builder validators.
2. **Strongly-typed-returns sweep** — ~70 action handlers flipped
   `Task<Data>` → `Task<Data<T>>`; provider interfaces (`IAssert`, `ICrypto`,
   `ISigning`, `IIdentity`, `IStore`, `IEvaluator`, `ITemplate`, `ILlm`)
   typed end-to-end; `IPath` verbs typed. New `Data<T>.From(@this)`
   retype-without-rewrap factory bypasses the implicit-operator double-wrap.
   Reflection-driven catalog now surfaces `→ returns T` via the new
   `DescribeReturnTypeName` in `modules/this.cs`.

## What was done

**v1–v3** — earlier review rounds: 8 findings → 3 follow-ups → CLEAN.

**v4** (this version) — full five-pass over the ~30 commits since v3.
**Verdict: NEEDS WORK (low-severity, doc-class only).**

- **F1 (Low)** — `Data<T>.From(@this)` docstring claims "Preserves all
  wrapper state" but the implementation does `source.Value is T t ? t :
  default`, which silently coerces a non-T Value to `default(T?)`. All
  current call sites guard with `if (!source.Success)` so the value-drop
  only fires on errored Data (Value=null anyway). Doc fix: name the
  intended use as "error/sentinel propagation across typed boundaries" and
  surface that the `Properties` dict is forwarded by shared reference.
- **F2 (Low)** — `modules/this.cs::DescribeReturnTypeName` carries an orphan
  `<summary>` block above its own summary (left over from inserting between
  the prior `DescribeReturnType`'s doc and definition). Trivial delete.

No correctness bugs. Build clean. C# tests **2889/2889** pass. PLang tests
**202/203** pass, **0 stale** — the 1 fail is `Modules/Llm/LlmCache.test.goal`
hitting a 503 from the upstream LLM; the equivalent `Llm/LlmCache.test.goal`
fixture passes, so the cache code itself works.

One **informational observation (O1)**: path write/append/mkdir/delete now
return the path in Value instead of empty Data. Intentional per
typed-returns spec; flagged so anyone reading `%result%` after a write
knows the shape changed.

## What to do next

Coder picks up F1+F2 as a one-commit doc pass (or defers them). Either
direction the branch is sound and ready for tester/merge.

## Code example

The `From()` factory the sweep added — and the docstring sharpening F1
recommends:

```csharp
// PLang/app/data/this.cs, public static @this<T> From(@this source)
//
// Current docstring: "Preserves all wrapper state: Value (cast to T?), …"
//
// Better: explicitly bound the contract —
//   Intended for error/sentinel propagation across typed boundaries. When the
//   source carries a success Value not assignable to T, Value silently
//   coerces to default(T?). The Properties dictionary is forwarded by
//   shared reference (not deep-cloned).
```

The behaviour is fine; the doc just over-promises.
