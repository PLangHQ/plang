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
