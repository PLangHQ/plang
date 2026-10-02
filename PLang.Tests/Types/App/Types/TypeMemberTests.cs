namespace PLang.Tests.App.Types;

/// <summary>
/// A type's plang-visible members, as its catalog lists them: each member its class marks, read as it is (a property,
/// or a method that asks only for its asker) or called with its arguments — each a name and a plang type — answering a
/// plang type. The asker's context and a cancellation are never a step's argument.
/// </summary>
public class TypeMemberTests
{
    private static global::app.type.property.@this Member(global::app.@this app, string type, string name)
        => app.type.list[type].Property!.Single(p => p.Name == name);

    [Test]
    public async Task TextsMembers_AreListed_ReadOrCalled()
    {
        await using var app = new global::app.@this("/app").Testing();

        var length = Member(app, "text", "length");
        var replace = Member(app, "text", "replace");
        var upper = Member(app, "text", "toUpper");

        await Assert.That(length.IsMethod).IsFalse();
        await Assert.That(length.Type.Name).IsEqualTo("number");
        await Assert.That(replace.IsMethod).IsTrue();
        await Assert.That(string.Join(", ", replace.Arguments!.Select(a => $"{a.Name}: {a.Type.Name}"))).IsEqualTo("old: text, new: text");
        await Assert.That(replace.Type.Name).IsEqualTo("text");
        await Assert.That(upper.IsMethod).IsTrue();
        await Assert.That(upper.Arguments!).IsEmpty();
    }

    // grep is one member, its lines optional; it answers the matching lines, grepCount a number.
    [Test]
    public async Task TextsGrep_IsOneMember_WithOptionalLines_AnsweringTypedValues()
    {
        await using var app = new global::app.@this("/app").Testing();

        var greps = app.type.list["text"].Property!.Where(p => p.Name == "grep").ToList();
        var count = Member(app, "text", "grepCount");

        await Assert.That(greps.Count).IsEqualTo(1);
        await Assert.That(string.Join(", ", greps[0].Arguments!.Select(a => $"{a.Name}{(a.Nullable ? "?" : "")}: {a.Type.Name}")))
            .IsEqualTo("pattern: text, lines?: number");
        await Assert.That(greps[0].Type.Name).IsEqualTo("list");
        await Assert.That(count.Type.Name).IsEqualTo("number");
    }

    // %x.grep("b")% and %x.grep("b", 1)% call the one member; its answer is the matching lines.
    [Test]
    public async Task Grep_IsCalled_WithOrWithoutItsLines()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("x", new global::app.data.@this("x", "a\nb\nc", context: ctx));

        var plain = await new global::app.type.item.variable.parser.@this("%x.grep(\"b\")%").Variable.Single().Start(ctx);
        var around = await new global::app.type.item.variable.parser.@this("%x.grep(\"b\", 1)%").Variable.Single().Start(ctx);
        var count = await new global::app.type.item.variable.parser.@this("%x.grepCount(\"b\")%").Variable.Single().Start(ctx);

        await plain.IsSuccess();
        var lines = (global::app.type.item.list.@this<global::app.type.item.text.@this>)(await plain.Value())!;
        await Assert.That(string.Join("|", lines.Items())).IsEqualTo("2: b");
        await around.IsSuccess();
        await Assert.That(((global::app.type.item.list.@this<global::app.type.item.text.@this>)(await around.Value())!).Items().Count()).IsEqualTo(3);
        await Assert.That((await count.Value())?.ToString()).IsEqualTo("1");
    }

    // A method that asks only for its asker's context is read as it is: %p.relative%.
    [Test]
    public async Task AMethodAskingOnlyForItsAsker_IsReadAsAProperty()
    {
        await using var app = new global::app.@this("/app").Testing();

        var relative = Member(app, "path", "relative");

        await Assert.That(relative.IsMethod).IsFalse();
        await Assert.That(relative.Type.Name).IsEqualTo("path");
    }
}
