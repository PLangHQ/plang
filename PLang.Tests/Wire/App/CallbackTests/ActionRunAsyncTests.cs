using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: action owns its execution. `action.Start(context)` is
/// the single entry; `App.Run` is deleted (Run retained as the inline-
/// C#-composition wrapper that builds an entity and dispatches through the
/// same path — spec-deferred for later removal once handlers grow their own
/// RunAsync surface).
public class ActionRunAsyncTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rasn-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

    [Test] public async Task ActionRunAsync_IsSingleEntry_PushAnchorExecute()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var action = context.Action("variable.set(Name=%v%, Value=\"ok\")");
        var result = await action.Start(context);
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("v"))).IsEqualTo("ok");
    }

    [Test] public async Task AppRun_SymbolAbsent_FromProductionSource()
    {
        var run = typeof(global::app.@this).GetMethod("Run",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null,
            new[] {
                typeof(global::app.goal.step.action.@this),
                typeof(global::app.actor.context.@this),
            },
            null);
        await Assert.That(run).IsNull();
    }

    [Test] public async Task AppRunAction_SymbolAbsent_FromProductionSource()
    {
        // A composed action runs through its own door — new action(seed, context).Start(context) —
        // so app has no Run<TAction> to dispatch it.
        var runAction = typeof(global::app.@this).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Run" && m.IsGenericMethodDefinition);
        await Assert.That(runAction).IsNull();
    }

    [Test] public async Task CauseParameter_AbsentFromAllCallSites()
    {
        // one Push per frame kind — goal, step, action, a frame that binds names — none takes a cause
        var pushes = typeof(global::app.call.list.@this).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name == "Push").ToList();
        await Assert.That(pushes.Count).IsEqualTo(4);
        await Assert.That(pushes.SelectMany(m => m.GetParameters()).Any(p => p.Name == "cause")).IsFalse();
    }
}
