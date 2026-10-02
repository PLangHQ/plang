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
