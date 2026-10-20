using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Render = global::app.module.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// A value a template shows whose write fails answers the render with its own error: a filter asks liquid for the
/// value's text, and the write's error is the render's, never a "waits on I/O" refusal in its place.
/// </summary>
public class FluidWriteErrorTests
{
    // a value whose write fails with a program's error
    private sealed class Refusing : global::app.type.item.@this
    {
        public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
            global::app.View mode, global::app.actor.context.@this? context)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            throw new global::app.error.AppException(new global::app.error.@this("this value refuses its write", "WriteRefused", 400));
        }
    }

    [Test] public async Task AValueWhoseWriteFails_InAFilter_AnswersItsOwnError()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        await ctx.Variable.Set(new global::app.data.@this("x", new Refusing(), context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)"{{ x | upcase }}",
            IsFile = (global::app.type.item.@bool.@this)false,
        };

        var result = await new global::app.module.ui.code.Fluid().Render(action);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("WriteRefused");
    }
}
