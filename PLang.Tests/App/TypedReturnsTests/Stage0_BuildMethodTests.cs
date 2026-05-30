using System.Reflection;
using app.module;
using app.module.builder.code;
using app.module.typedreturns;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: IClass exposes a Build() method that the validate pass invokes
// per action. A handler's Build() can return Data.Ok(typeName) to stamp the
// type on the step's terminal variable.set, Data.Fail to abort validation,
// or bare Data.Ok() to contribute nothing.

// [NotInParallel]: BuildOrdered.InvocationLog is a process-static List that all
// instances of this fixture share.  TUnit parallelises siblings by default;
// without this, the Clear() in [Before(Test)] races a sibling test's Add() and
// BuilderValidate_CallsBuildOnEachAction_InOrder reads an empty log.
[NotInParallel]
public class Stage0_BuildMethodTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app");
        _app.Module.RegisterType("typedreturns", "noopbuild", typeof(NoopBuild));
        _app.Module.RegisterType("typedreturns", "buildreturnstype", typeof(BuildReturnsType));
        _app.Module.RegisterType("typedreturns", "buildfails", typeof(BuildFails));
        _app.Module.RegisterType("typedreturns", "buildbareok", typeof(BuildBareOk));
        _app.Module.RegisterType("typedreturns", "buildordered", typeof(BuildOrdered));
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
        await result.IsSuccess();
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

        var errors = await Default.RunBuildPass(actions, _app.Module, _app.User.Context);

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

        var errors = await Default.RunBuildPass(actions, _app.Module, _app.User.Context);

        await Assert.That(errors).IsEmpty();
        var typeParam = setAction.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        await Assert.That(typeParam).IsNotNull();
        // The stamp is a structured type entity; a bare-string Build() return is
        // canonicalised into one. The terminal variable.set carries {name:"foo"}.
        await Assert.That(((global::app.type.@this)typeParam!.Value!).Name).IsEqualTo("foo");
    }

    // Build() returning Fail aborts and surfaces the error message in the errors list.
    [Test]
    public async Task BuilderValidate_BuildReturnsFail_SurfacesErrorAndFailsValidation()
    {
        var actions = ActionsOf(Make("typedreturns", "buildfails"));

        var errors = await Default.RunBuildPass(actions, _app.Module, _app.User.Context);

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

        var errors = await Default.RunBuildPass(actions, _app.Module, _app.User.Context);

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

        var errors = await Default.RunBuildPass(actions, _app.Module, _app.User.Context);

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

        var errors = await Default.RunBuildPass(actions, _app.Module, _app.User.Context);

        await Assert.That(errors).IsEmpty();
        await Assert.That(firstSet.Parameters.Any(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase))).IsFalse();
        var lastType = lastSet.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        await Assert.That(lastType).IsNotNull();
        await Assert.That(((global::app.type.@this)lastType!.Value!).Name).IsEqualTo("foo");
    }
}
