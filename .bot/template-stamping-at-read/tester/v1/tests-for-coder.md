# Tests to add — handoff to coder (template-stamping-at-read)

These close F1 (HIGH) and F2 (MEDIUM) from `test-report.md`. **I wrote and ran
all 14 — they compile and pass on the real code, and the F1 decline tests were
confirmed to FAIL under a mutation that neuters the decline (true guards, not
tautologies).** Tester doesn't commit source, so they're handed to you to add.

Place them in the **Modules** suite (verified there). Drop the `TesterTemp`
prefix when you adopt them.

---

## F1 — born-typed `variable.set` decline (HIGH)

`PLang.Tests/Modules/App/Modules/variable/BornTypedDeclineTests.cs`

```csharp
using app;

namespace PLang.Tests.App.actions.variable;

// The born-typed rule: a variable NAMES a thing; a variable.set whose Name slot
// holds a *value* (type:string, not type:variable) is declined (CreateDeclined).
// This is the rule that broke the PLang suite; it had no C# guard before this.
public class BornTypedDeclineTests
{
    private global::app.@this _app = null!;
    [Before(Test)] public void Setup() => _app = new global::app.@this("/app");

    // Direct unit test on the decline seam (Variable.Create, app/variable/this.cs:67).
    [Test]
    public async Task Create_TextValue_DeclinesWithCreateDeclined()
    {
        var ctx = _app.User.Context;
        global::app.type.item.@this textValue = new global::app.type.text.@this("some value");
        var asking = new Data("Name", "Name") { Context = ctx };

        var result = global::app.variable.@this.Create(textValue, asking);

        // cast to object: Variable has an implicit string operator that NREs on null
        await Assert.That((object?)result).IsNull();
        await Assert.That(asking.Error).IsNotNull();
        await Assert.That(asking.Error!.Key).IsEqualTo("CreateDeclined");
    }

    [Test]
    public async Task Create_VariableValue_PassesThrough()
    {
        var ctx = _app.User.Context;
        var v = global::app.variable.@this.Resolve("%x%", ctx);
        var asking = new Data("Name", "x") { Context = ctx };

        var result = global::app.variable.@this.Create(v, asking);

        await Assert.That(result).IsNotNull();
        await Assert.That(asking.Error).IsNull();
        await Assert.That(result!.Name).IsEqualTo("x");
    }

    // Handler-level: a variable.set Name param typed as text (the stale .pr shape,
    // NOT type:variable) declines at dispatch. Built by hand to bypass
    // TestAction/PrParam's auto-stamp of type:variable.
    [Test]
    public async Task Set_NameTypedAsText_DeclinesAtDispatch()
    {
        var ctx = _app.User.Context;
        var act = new global::app.goal.steps.step.actions.action.@this
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<Data>
            {
                new Data("name", "%path%", global::app.type.@this.FromName("string")),
                new Data("value", "."),
            }
        };

        var result = await act.RunAsync(ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CreateDeclined");
    }

    // Positive control: same step with Name stamped type:variable succeeds — proves
    // the gate is the type stamp, not something incidental.
    [Test]
    public async Task Set_NameStampedVariable_Succeeds()
    {
        var ctx = _app.User.Context;
        var act = new global::app.goal.steps.step.actions.action.@this
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<Data>
            {
                new Data("name", "path", new global::app.type.@this("variable")),
                new Data("value", "."),
            }
        };

        var result = await act.RunAsync(ctx);

        await result.IsSuccess();
        await Assert.That((await ctx.Variable.GetValue("path"))?.ToString()).IsEqualTo(".");
    }
}
```

**Mutation confirmation (run by tester):** with the decline at
`app/variable/this.cs:70-73` replaced by `return new @this(value?.ToString() ?? "");`,
`Create_TextValue_DeclinesWithCreateDeclined` and `Set_NameTypedAsText_DeclinesAtDispatch`
both FAIL; the two positive tests stay green. The guard is real.

---

## F2 — temporal comparison operators through the condition path (MEDIUM)

`PLang.Tests/Modules/App/Modules/condition/TemporalOperatorTests.cs`

```csharp
using app.module.condition;
using app.module.condition.code;

namespace PLang.Tests.App.Modules.condition;

// Temporal comparison operators (<,>,<=,>=) through the user-facing
// DefaultEvaluator/Operator path for datetime/date/time/duration. Previously
// covered only at the low-level Stage4_PerTypeCompareTests, never through Operator.
public class TemporalOperatorTests
{
    private readonly Default _eval = new();
    private static Data D(object? value) => value == null ? new Data("") : Data.Ok(value);

    private Task<global::app.data.@this<global::app.type.@bool.@this>> Eval(object? left, string op, object? right)
        => _eval.Evaluate(new Compare { Left = D(left), Operator = (global::app.type.choice.@this<Operator>)new Operator(op), Right = D(right) });

    private bool IsTrue(global::app.data.@this<global::app.type.@bool.@this> r) => r.Success && (r.Peek() as global::app.type.@bool.@this)?.Value == true;
    private bool IsFalse(global::app.data.@this<global::app.type.@bool.@this> r) => r.Success && (r.Peek() as global::app.type.@bool.@this)?.Value == false;

    private static global::app.type.datetime.@this Dt(string iso) => new(System.DateTimeOffset.Parse(iso));
    private static global::app.type.date.@this Date(string iso) => new(System.DateOnly.Parse(iso));
    private static global::app.type.time.@this Time(string iso) => new(System.TimeOnly.Parse(iso));
    private static global::app.type.duration.@this Dur(System.TimeSpan ts) => new(ts);

    [Test] public async Task Datetime_LessThan() => await Assert.That(IsTrue(await Eval(Dt("2024-01-01T00:00:00Z"), "<", Dt("2024-06-01T00:00:00Z")))).IsTrue();
    [Test] public async Task Datetime_GreaterThan() => await Assert.That(IsTrue(await Eval(Dt("2024-06-01T00:00:00Z"), ">", Dt("2024-01-01T00:00:00Z")))).IsTrue();
    [Test] public async Task Datetime_GreaterOrEqual_Equal() => await Assert.That(IsTrue(await Eval(Dt("2024-06-01T00:00:00Z"), ">=", Dt("2024-06-01T00:00:00Z")))).IsTrue();
    [Test] public async Task Datetime_LessOrEqual_False() => await Assert.That(IsFalse(await Eval(Dt("2024-06-01T00:00:00Z"), "<=", Dt("2024-01-01T00:00:00Z")))).IsTrue();

    [Test] public async Task Date_LessThan() => await Assert.That(IsTrue(await Eval(Date("2024-01-01"), "<", Date("2024-06-01")))).IsTrue();
    [Test] public async Task Date_GreaterThan() => await Assert.That(IsTrue(await Eval(Date("2024-06-01"), ">", Date("2024-01-01")))).IsTrue();

    [Test] public async Task Time_LessThan() => await Assert.That(IsTrue(await Eval(Time("08:00"), "<", Time("17:30")))).IsTrue();
    [Test] public async Task Time_GreaterOrEqual() => await Assert.That(IsTrue(await Eval(Time("17:30"), ">=", Time("08:00")))).IsTrue();

    [Test] public async Task Duration_LessThan() => await Assert.That(IsTrue(await Eval(Dur(System.TimeSpan.FromSeconds(30)), "<", Dur(System.TimeSpan.FromMinutes(2))))).IsTrue();
    [Test] public async Task Duration_GreaterThan() => await Assert.That(IsTrue(await Eval(Dur(System.TimeSpan.FromMinutes(2)), ">", Dur(System.TimeSpan.FromSeconds(30))))).IsTrue();
}
```

All 10 pass as-is (verified). These are coverage, not bug-finds — the behavior
already works; nothing guarded it through the Operator path until now.

---

## F3 — low (optional)

`OperatorTests.Choices_ContainsAllOperators` and `Stage1_ComparisonEnumTests` are
structural (string/enum-name checks). Not wrong, but they don't substitute for
behavioral coverage; F2 covers the real gap. No action required beyond F2.
