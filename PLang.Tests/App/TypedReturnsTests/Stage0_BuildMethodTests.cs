using System.Reflection;
using app.modules;
using app.modules.builder.code;
using app.modules.typedreturns;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: IClass exposes a Build() method that the validate pass invokes
// per action. A handler's Build() can return Data.Ok(typeName) to stamp the
// type on the step's terminal variable.set, Data.Fail to abort validation,
// or bare Data.Ok() to contribute nothing.

public class Stage0_BuildMethodTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app");
        _app.Modules.RegisterType("typedreturns", "noopbuild", typeof(NoopBuild));
        _app.Modules.RegisterType("typedreturns", "buildreturnstype", typeof(BuildReturnsType));
        _app.Modules.RegisterType("typedreturns", "buildfails", typeof(BuildFails));
        _app.Modules.RegisterType("typedreturns", "buildbareok", typeof(BuildBareOk));
        _app.Modules.RegisterType("typedreturns", "buildordered", typeof(BuildOrdered));
        BuildOrdered.InvocationLog.Clear();
    }

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static PrAction Make(string module, string actionName,
        params (string name, object? value)[] parameters)
        => new PrAction
        {
            Module = module,
            ActionName = actionName,
            Parameters = parameters.Select(p => new Data(p.name, p.value)).ToList()
        };

    private static StepActions ActionsOf(params PrAction[] actions)
    {
        var s = new StepActions();
        foreach (var a in actions) s.Add(a);
        return s;
    }

    // IClass declares Build() returning Task<Data>. Reflection check guards the
    // shape against accidental rename/signature drift.
    [Test]
    public async Task IClass_HasOptionalBuildMethod_ReturningTaskOfData()
    {
        var method = typeof(IClass).GetMethod("Build", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(Task<Data>));
        await Assert.That(method.GetParameters().Length).IsEqualTo(0);
    }

    // A handler without a Build override gets the IClass default impl — Ok() with null Value.
    [Test]
    public async Task IClass_BuildDefaultImpl_ReturnsDataOkNoValue()
    {
        var handler = (IClass)new NoopBuild();
        var result = await handler.Build();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    // RunBuildPass walks actions left-to-right; the per-action log records call order.
    [Test]
    public async Task BuilderValidate_CallsBuildOnEachAction_InOrder()
    {
        var actions = ActionsOf(
            Make("typedreturns", "buildordered", ("Marker", "first")),
            Make("typedreturns", "buildordered", ("Marker", "second")),
            Make("typedreturns", "buildordered", ("Marker", "third")));

        var errors = await Default.RunBuildPass(actions, _app.Modules, _app.User.Context);

        await Assert.That(errors).IsEmpty();
        await Assert.That(BuildOrdered.InvocationLog).IsEquivalentTo(new[] { "first", "second", "third" });
    }

    // Build() returning Ok(typeName) stamps the terminal variable.set's Type parameter.
    [Test]
    public async Task BuilderValidate_BuildReturnsOkWithTypeName_SetsTerminalVariableSetType()
    {
        var setAction = Make("variable", "set", ("Name", "x"), ("Value", "v"));
        var actions = ActionsOf(
            Make("typedreturns", "buildreturnstype"),
            setAction);

        var errors = await Default.RunBuildPass(actions, _app.Modules, _app.User.Context);

        await Assert.That(errors).IsEmpty();
        var typeParam = setAction.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        await Assert.That(typeParam).IsNotNull();
        await Assert.That(typeParam!.Value).IsEqualTo("foo");
    }

    // Build() returning Fail aborts and surfaces the error message in the errors list.
    [Test]
    public async Task BuilderValidate_BuildReturnsFail_SurfacesErrorAndFailsValidation()
    {
        var actions = ActionsOf(Make("typedreturns", "buildfails"));

        var errors = await Default.RunBuildPass(actions, _app.Modules, _app.User.Context);

        await Assert.That(errors).IsNotEmpty();
        await Assert.That(errors[0]).Contains("forced build failure");
    }

    // Bare Ok() means "no value — don't touch the terminal Type". Existing/absent Type stays.
    [Test]
    public async Task BuilderValidate_BuildReturnsBareOk_DoesNotTouchTerminalType()
    {
        var setAction = Make("variable", "set", ("Name", "x"), ("Value", "v"));
        var actions = ActionsOf(
            Make("typedreturns", "buildbareok"),
            setAction);

        var errors = await Default.RunBuildPass(actions, _app.Modules, _app.User.Context);

        await Assert.That(errors).IsEmpty();
        var typeParam = setAction.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        await Assert.That(typeParam).IsNull();
    }

    // NoopBuild has no override — the default-impl path through RunBuildPass succeeds.
    [Test]
    public async Task IClass_Build_IsOptional_HandlerWithoutOverrideCompiles()
    {
        var actions = ActionsOf(Make("typedreturns", "noopbuild"));

        var errors = await Default.RunBuildPass(actions, _app.Modules, _app.User.Context);

        await Assert.That(errors).IsEmpty();
    }

    // With two variable.set actions, the stamp must land on the trailing one.
    [Test]
    public async Task BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins()
    {
        var firstSet = Make("variable", "set", ("Name", "a"), ("Value", "v1"));
        var lastSet = Make("variable", "set", ("Name", "b"), ("Value", "v2"));
        var actions = ActionsOf(
            Make("typedreturns", "buildreturnstype"),
            firstSet,
            lastSet);

        var errors = await Default.RunBuildPass(actions, _app.Modules, _app.User.Context);

        await Assert.That(errors).IsEmpty();
        await Assert.That(firstSet.Parameters.Any(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase))).IsFalse();
        var lastType = lastSet.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        await Assert.That(lastType).IsNotNull();
        await Assert.That(lastType!.Value).IsEqualTo("foo");
    }
}
