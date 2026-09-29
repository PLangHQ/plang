using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// app.variable is the variable type: its list is the asker's memory, reached through navigation's
// context; C# has no asker at app.variable, so its list says to use context.Variable.
public class VariableAccessorTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    [Test] public async Task AppVariableList_IsTheAskersMemory()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("user", "ingi");

        var read = await Read("%!app.variable.list%", ctx);
        await read.IsSuccess();
        var list = (global::app.type.item.list.@this<global::app.type.item.variable.@this>)read.Peek()!;
        await Assert.That(list.Items().Any(v => v.Name == "user")).IsTrue();
    }

    [Test] public async Task AppVariable_Key_IsThatVariable()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("user", "ingi");

        var read = await Read("%!app.variable.user%", ctx);
        await read.IsSuccess();
        await Assert.That(read.Peek()).IsTypeOf<global::app.type.item.variable.@this>();
        await Assert.That(((global::app.type.item.variable.@this)read.Peek()!).Name).IsEqualTo("user");
    }

    [Test] public async Task AppVariable_Name_IsItsName()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("user", "ingi");

        var read = await Read("%!app.variable.user.name%", ctx);
        await read.IsSuccess();
        await Assert.That(read.Peek()?.ToString()).IsEqualTo("user");
    }

    // The type of one variable is its value's: %user% holds a text.
    [Test] public async Task AppVariable_Type_IsItsValuesType()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("user", "ingi");

        var read = await Read("%!app.variable.user.type%", ctx);
        await read.IsSuccess();
        await Assert.That(read.Peek()?.ToString()).IsEqualTo("text");
    }

    [Test] public async Task AppVariable_UnknownKey_IsNotFound()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app.variable.nobody%", app.actor.list.User.Context);
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("NotFound");
    }

    [Test] public async Task AppVariable_TheCallsOwnShadowsTheActors()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("user", "outer");
        await using (ctx.Variable.Calls.Push([new global::app.data.@this("place", "here", context: ctx)]))
        {
            var names = ctx.Variable.list.Items().Select(v => v.Name).ToList();
            await Assert.That(names.IndexOf("place")).IsEqualTo(0);
            await Assert.That(names.Contains("user")).IsTrue();
        }
    }

    [Test] public async Task CSharpAppVariableList_HasNoAsker_Throws()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(() => { _ = app.variable.list; return Task.CompletedTask; })
            .Throws<InvalidOperationException>();
    }
}
