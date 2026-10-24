using permission = global::app.type.item.permission;

namespace PLang.Tests.App.Errors;

// A value that can't be what it claims is an error naming it — never a quiet fallback.
public class NoSwallowedValueErrorsTests
{
    [Test] public async Task AGrepPatternThatIsNotARegex_IsAnInvalidPatternError()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-grep-" + Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var text = app.actor.list.User.Context.Ok("a(b\nc");
        var thrown = await Assert.That(() => { new global::app.data.code.Default().Grep(text, "a(b"); return Task.CompletedTask; })
            .Throws<global::app.error.AppException>();
        await Assert.That(thrown!.Error.Key).IsEqualTo("InvalidPattern");
    }

    [Test] public async Task AGlobGrantWithRegexCharacters_MatchesThemLiterally()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-glob-" + Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var grant = new permission.@this(user.Name, "/a(b/*", permission.@this.AllVerbs, permission.Match.glob, app.type.list[typeof(permission.@this)]);
        await Assert.That(grant.Allows(permission.@this.Request(user, "/a(b/x", permission.Verb.read))).IsTrue();
    }

    [Test] public async Task APropertyOfTheWrongType_ThrowsInsteadOfReadingAsAbsent()
    {
        var properties = new global::app.data.Properties { ["n"] = "not a number" };
        await Assert.That(async () => await properties.Get<int>("n")).Throws<FormatException>();
        await Assert.That(await properties.Get<int>("missing")).IsEqualTo(0);
    }
}
