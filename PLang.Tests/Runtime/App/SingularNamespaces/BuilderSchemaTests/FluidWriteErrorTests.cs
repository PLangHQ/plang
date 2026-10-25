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
        protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            throw new global::app.error.AppException(new global::app.error.@this("this value refuses its write", "WriteRefused", 400));
        }
    }

    // a value whose write waits on what hasn't arrived yet
    private sealed class Waiting(System.Threading.Tasks.Task arrives) : global::app.type.item.@this
    {
        protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
        {
            await arrives;
            writer.String("arrived");
        }
    }

    // a value whose comparison fails with a program's error; it drives every comparison it is in
    private sealed class Incomparable : global::app.type.item.@this
    {
        public override int Rank => int.MaxValue;

        protected override async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(
            global::app.type.item.@this other, global::app.actor.context.@this context)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            throw new global::app.error.AppException(new global::app.error.@this("this value refuses its comparison", "CompareRefused", 400));
        }
    }

    private static async Task<global::app.data.@this> Rendered(global::app.@this app, string template, global::app.type.item.@this value)
    {
        var ctx = app.actor.list.System.Context;
        await ctx.Variable.Set(new global::app.data.@this("x", value, context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        return await new global::app.module.ui.code.Fluid().Render(action);
    }

    [Test] public async Task AValueWhoseWriteFails_InAFilter_AnswersItsOwnError()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

        var result = await Rendered(app, "{{ x | upcase }}", new Refusing());

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("WriteRefused");
    }

    [Test] public async Task AValueWhoseWriteIsStillPending_InAFilter_AnswersTemplateValueUnread()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var arrives = new System.Threading.Tasks.TaskCompletionSource();

        var result = await Rendered(app, "{{ x | upcase }}", new Waiting(arrives.Task));
        arrives.SetResult();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TemplateValueUnread");
    }

    [Test] public async Task AValueWhoseComparisonFails_AnswersItsOwnError()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

        var result = await Rendered(app, "{% if x == \"a\" %}yes{% endif %}", new Incomparable());

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CompareRefused");
    }
}
