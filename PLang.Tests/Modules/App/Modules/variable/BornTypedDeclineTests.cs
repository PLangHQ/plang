using app;

namespace PLang.Tests.App.actions.variable;

// The born-typed rule: a variable NAMES a thing; a variable.set whose Name slot
// holds a *value* (type:string, not type:variable) is declined (CreateVariableDeclined).
// This is the rule that broke the PLang suite; it had no C# guard before this.
public class BornTypedDeclineTests
{
    private global::app.@this _app = null!;
    [Before(Test)] public void Setup() => _app = TestApp.Create("/app");

    // Direct unit test on the decline seam (Variable.Create, app/variable/this.cs:67).
    [Test]
    public async Task Create_TextValue_DeclinesWithCreateVariableDeclined()
    {
        var ctx = _app.User.Context;
        global::app.type.item.@this textValue = new global::app.type.item.text.@this("some value");
        var asking = new Data("Name", "Name", context: ctx);

        var result = global::app.variable.@this.Create(textValue, asking);

        // cast to object: Variable has an implicit string operator that NREs on null
        await Assert.That((object?)result).IsNull();
        await Assert.That(asking.Error).IsNotNull();
        await Assert.That(asking.Error!.Key).IsEqualTo("CreateVariableDeclined");
    }

    [Test]
    public async Task Create_VariableValue_PassesThrough()
    {
        var ctx = _app.User.Context;
        var v = global::app.variable.@this.Resolve("%x%", ctx);
        var asking = new Data("Name", "x", context: ctx);

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
        var act = new global::app.goal.step.action.@this
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<Data>
            {
                new Data("name", "%path%", global::PLang.Tests.TestApp.SharedContext.Type.Create("string"), context: ctx),
                new Data("value", ".", context: ctx),
            }
        };

        var result = await act.RunAsync(ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CreateVariableDeclined");
    }

    // Positive control: same step with Name stamped type:variable succeeds — proves
    // the gate is the type stamp, not something incidental.
    [Test]
    public async Task Set_NameStampedVariable_Succeeds()
    {
        var ctx = _app.User.Context;
        var act = new global::app.goal.step.action.@this
        {
            Module = "variable",
            ActionName = "set",
            Parameters = new List<Data>
            {
                new Data("name", "path", new global::app.type.@this("variable"), context: ctx),
                new Data("value", ".", context: ctx),
            }
        };

        var result = await act.RunAsync(ctx);

        await result.IsSuccess();
        await Assert.That((await ctx.Variable.GetValue("path"))?.ToString()).IsEqualTo(".");
    }
}
