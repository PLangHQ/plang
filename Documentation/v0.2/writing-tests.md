# Writing tests for PLang

PLang is a programming language. Everyone interacts with it **two ways**:

1. **The PLang language** — a `.goal` compiles to a `.pr` (PR structure) that the runtime executes.
2. **A new C# module** — an action handler that PLang dispatches to.

A test should exercise the same path. **Default: build a `.pr` deterministically and run
it through the engine.** Only a thin, named *floor* stays as C# unit. This document is the
rule, the canonical pattern, and the smells to avoid — so we stop writing the bad version.

---

## The one rule

> **Test through the PR, not around it.** If a behavior is reachable by a PLang author —
> setting a variable, calling a goal, writing output, a step failing — the test builds a
> goal, loads it through the real read boundary, runs it through the engine, and asserts on
> what a user can observe: **output channel, variable state, returned `Data`, or raised error.**

Hand-constructing an action record and calling `.Run()` is the **bad version**. It tests the
handler in a vacuum and skips everything that actually breaks in production: born-typing,
`%var%` resolution, `Data<T>` wiring, source-gen guards, dispatch.

```csharp
// ❌ BAD — bypasses build + engine; proves nothing about the author's path
var action = new Get { Context = context, Name = new app.variable.@this("x") };
var result = await action.Run();

// ❌ BAD — TestAction.RunAsync is still around the engine, not through it
var action = TestAction.Create("variable", "set", ("name", "%x%"), ("value", 1));
await action.RunAsync(context);

// ❌ BAD — poking an internal C# type's API surface directly
var d = new Data("Value", 42, app.type.@this.FromName("int"));
await Assert.That(d.Type.Name).IsEqualTo("number");
```

---

## The canonical pattern

```csharp
using app.actor.context;

namespace PLang.Tests.App.actions.variable;

public class VariableGoalRunTests
{
    static async Task<(App engine, context ctx, Data result)> Run(Goal spec)
    {
        var engine = TestApp.Create("/app");                       // (1) one factory — always
        var goal   = await RealGoalLoad.ViaChannel(engine, spec);  // (2) the real .pr read boundary
        engine.Goal.Add(goal);
        var ctx    = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, ctx);         // (3) dispatch through the engine
        return (engine, ctx, result);
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var (engine, ctx, result) = await Run(Make.Goal("T",
            Make.Step("set %x% = \"hello\"",
                Make.Action("variable", "set",
                    Make.Param("Name", "x", "variable"),   // write-target → declared type "variable"
                    ("Value", "hello")))));                // born-typed: string → text, 42 → number, true → bool
        await using var _ = engine;
        await result.IsSuccess();
        await Assert.That(await ctx.Variable.GetValue("x")).IsEqualTo("hello");  // assert observable state
    }
}
```

The three helpers (in `PLang.Tests/Shared/`):

- **`Make.Goal / Make.Step / Make.Action / Make.Param`** — build a `.pr` by hand,
  deterministically. Params are **born-typed from their value**; use `Make.Param(name, value,
  "type")` only to *declare* a type that differs from the value's natural one (write-target
  `variable`, a date written as a string, `as text/md`, `strict`).
- **`RealGoalLoad.ViaChannel`** — serializes the goal to the `.pr` wire shape, feeds it
  through a stream channel (`application/plang-goal`), and lets the channel materialize it —
  **the exact read boundary a file/http channel uses.** This is what makes it "through" the
  runtime, not "around" it. Do **not** run the in-C# `PrAction` shape directly.
- **`TestApp.Create("/app")`** — the **only** way to make an engine in a test. It flips
  `Tester.IsEnabled = true` (in-memory settings, no on-disk pollution). Never
  `new app.@this(...)` directly. Need to bend a setting? Use an overload —
  `TestApp.Create("/app", modules)` / add one as needed — only when the test actually requires it.

### This is NOT the LLM builder
`Make.Goal` constructs the PR deterministically. It does **not** call the LLM compiler, so
the suite stays fast and reproducible. The LLM build path is tested separately, by PLang
itself, and needs only a thin "does the planner map this step to this action" layer — not
one test per behavior.

---

## Parameterize matrices — don't unroll them

A test that varies **only the input type/value** is **one** data-driven test, not N. The
value carries its born type; assert the derived result.

```csharp
// ✅ ONE test, five cases
[Test]
[Arguments("hello", "text")]
[Arguments(42,      "number")]
[Arguments(42L,     "number")]
[Arguments(3.14,    "number")]
[Arguments(true,    "bool")]
public async Task Set_InfersType(object value, string expectedTypeName)
{
    var (engine, ctx, result) = await Run(Make.Goal("T",
        Make.Step("set %v%", Make.Action("variable", "set",
            Make.Param("Name", "v", "variable"), ("Value", value)))));
    await using var _ = engine;
    await result.IsSuccess();
    await Assert.That((await ctx.Variable.Get("v"))!.Type!.Name).IsEqualTo(expectedTypeName);
}
```

`GetType_String_…`, `GetType_Int_…`, `GetType_Long_…` × 90 is a mapping table typed twice.
Collapse it to one `[Arguments]` test (or one data-driven test over the table itself).

---

## The floor — what legitimately stays a C# unit test

Keep a C# unit test **only** when the behavior has *no language surface* — a goal cannot
observe it. The floor is small and must be **named**, not accidental:

1. **Source generator** (`PLang.Tests/Generator/`) — compile-time codegen; no runtime path.
2. **Build-time validation** — `ValidateBuild` / `IBuildValidatable` runs during the build,
   which `Make.Goal` bypasses. Test the static method directly.
3. **Internal-state mechanisms** — "did only the touched branches materialize"
   (lazy-deserialize), wire byte-determinism/ordering, a cache/timing probe. The *output* is
   correct via a goal-run; the *internal* property isn't observable from PLang.

If you're about to write a C# unit test, ask: **"could a `.goal` observe this?"** If yes, it's
not floor — write the goal-run.

---

## How to know you didn't drop coverage (the gate)

When replacing unit tests with goal-run tests, **prove** the goal-run path covers the same
lines. Goal-run tests execute the same C#, so one collector measures both.

```bash
# baseline: the existing unit tests
dotnet exec PLang.Tests.Modules.dll --treenode-filter "/*/*/<UnitClass>/*" \
  --coverage --coverage-output-format cobertura --coverage-output baseline.cobertura.xml

# replacement: the new goal-run tests
dotnet exec PLang.Tests.Modules.dll --treenode-filter "/*/*/<GoalRunClass>/*" \
  --coverage --coverage-output-format cobertura --coverage-output goalrun.cobertura.xml
```

Diff the covered `(file, line)` sets. **Every line in `baseline − goalrun` is a dropped
path.** Each gets one disposition:

- **Convertible** → add a goal-run case that reaches it.
- **Floor** → keep a named C# unit (per the floor list above).

A module is safe to delete its unit layer only when `baseline − goalrun ⊆ {kept floor lines}`.
The gate is the **line-set difference**, not a coverage percentage.

> Worked example (the `variable` pilot): goal-run reproduced clear/exists/get/remove
> line-for-line; on `set.cs` it missed exactly `ValidateBuild` (build-time → floor) and one
> kind-derivation branch (→ added a forced-type goal-run). Nothing slipped silently.

---

## Checklist before you commit a test

- [ ] Does it run through `Make.Goal → RealGoalLoad.ViaChannel → engine.RunGoalAsync`?
      (If it news up an action and calls `.Run()`/`RunAsync`, rewrite it.)
- [ ] Engine made via `TestApp.Create(...)` — never `new app.@this(...)`?
- [ ] Asserts on observable state (output / variable / `Data` / error), not internal API?
- [ ] Type/value variations collapsed into one `[Arguments]` test?
- [ ] If it's a C# unit test, is it on the **named floor** (source-gen / build-time /
      internal-state)? If not, it should be a goal-run.
- [ ] If replacing unit tests, did the coverage diff come back clean (lost lines ⊆ floor)?

---

See also: `Documentation/v0.2/action-catalog.md` (action shapes), CLAUDE.md "Running plang
Tests" (runner, stale-binary trap), `PLang.Tests/Shared/` (`Make`, `RealGoalLoad`, `TestApp`).
