using permission = global::app.type.item.permission;

namespace PLang.Tests.App.Errors;

// A value that can't be what it claims is an error naming it — never a quiet fallback.
public class NoSwallowedValueErrorsTests
{
    [Test] public async Task AGrepPatternThatIsNotARegex_IsAnInvalidPatternError()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-grep-" + Guid.NewGuid().ToString("N")[..8])).Testing();
        var text = app.actor.list.User.Context.Ok("a(b\nc");
        var thrown = await Assert.That(() => { new global::app.data.code.Default().Grep(text, "a(b"); return Task.CompletedTask; })
            .Throws<global::app.error.AppException>();
        await Assert.That(thrown!.Key).IsEqualTo("InvalidPattern");
    }

    [Test] public async Task ARegexGrantThatIsNotARegex_FailsWhereItIsMade()
    {
        var thrown = await Assert.That(() => { _ = new permission.@this("me", "a(b", permission.@this.AllVerbs, permission.Match.Regex); return Task.CompletedTask; })
            .Throws<global::app.error.AppException>();
        await Assert.That(thrown!.Key).IsEqualTo("InvalidPermissionPattern");
    }

    [Test] public async Task AGlobGrantWithRegexCharacters_MatchesThemLiterally()
    {
        var grant = new permission.@this("me", "/a(b/*", permission.@this.AllVerbs, permission.Match.Glob);
        await Assert.That(grant.Covers(permission.@this.Request("me", "/a(b/x", permission.Verb.Read))).IsTrue();
    }

    [Test] public async Task APropertyOfTheWrongType_ThrowsInsteadOfReadingAsAbsent()
    {
        var properties = new global::app.data.Properties { ["n"] = "not a number" };
        await Assert.That(async () => await properties.Get<int>("n")).Throws<FormatException>();
        await Assert.That(await properties.Get<int>("missing")).IsEqualTo(0);
    }
}
