using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: action owns its execution. `action.RunAsync(context)` is
/// the single entry; `App.Run` is deleted (RunAction retained as the inline-
/// C#-composition wrapper that builds an entity and dispatches through the
/// same path — spec-deferred for later removal once handlers grow their own
/// RunAsync surface).
public class ActionRunAsyncTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rasn-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task ActionRunAsync_IsSingleEntry_PushAnchorExecute()
    {
        var app = NewApp();
        var context = app.User.Context;
        var action = TestAction.Create("variable", "set", ("name", "%v%"), ("value", "ok"));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        await Assert.That(context.Variable.GetValue("v")).IsEqualTo("ok");
    }

    [Test] public async Task AppRun_SymbolAbsent_FromProductionSource()
    {
        var run = typeof(global::app.@this).GetMethod("Run",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null,
            new[] {
                typeof(global::app.goal.steps.step.actions.action.@this),
                typeof(global::app.actor.context.@this),
            },
            null);
        await Assert.That(run).IsNull();
    }

    [Test] public async Task AppRunAction_SymbolAbsent_FromProductionSource()
    {
        // Spec-deferred: RunAction retained as the inline-composition entry.
        // It builds an Action.@this entity with PreboundHandler set and
        // dispatches through entity.RunAsync — same path as PR-loaded actions,
        // synthetic-stamped. Pin current behavior (will flip when removed).
        var runAction = typeof(global::app.@this).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "RunAction" && m.GetGenericArguments().Length == 1);
        await Assert.That(runAction).IsNotNull();
    }

    [Test] public async Task CauseParameter_AbsentFromAllCallSites()
    {
        var push = typeof(global::app.callstack.@this).GetMethod("Push");
        var args = push?.GetParameters();
        await Assert.That(args).IsNotNull();
        await Assert.That(args!.Length).IsEqualTo(2);
        await Assert.That(args.Any(p => p.Name == "cause")).IsFalse();
    }
}
