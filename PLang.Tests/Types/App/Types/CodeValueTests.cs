using code = global::app.type.code.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// app/type/code/this.cs — Source, Language, IBooleanResolvable = source non-empty.
// Kind is the language ("csharp"/"python"/…); text fallback when language not detected.

public class CodeValueTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task Code_FromSourceAndLanguage_StoresBoth()
    {
        var c = new code("Console.WriteLine();", "csharp", app.type.list[typeof(code)]);
        await Assert.That(c.Source).IsEqualTo("Console.WriteLine();");
        await Assert.That(c.Language).IsEqualTo("csharp");
    }

    [Test] public async Task Code_Resolve_String_DetectsLanguageOrDefaultsToText()
    {
        await using var app = global::PLang.Tests.Session.App(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-code-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        await Assert.That(code.Resolve("using System;", app.actor.list.User.Context)!.Language).IsEqualTo("csharp");
        await Assert.That(code.Resolve("def foo:\n  print(1)", app.actor.list.User.Context)!.Language).IsEqualTo("python");
        await Assert.That(code.Resolve("function x() {}", app.actor.list.User.Context)!.Language).IsEqualTo("javascript");
        await Assert.That(code.Resolve("plain text", app.actor.list.User.Context)!.Language).IsEqualTo("text");
        await Assert.That(code.Resolve("just some prose", app.actor.list.User.Context)!.Language).IsEqualTo("text");
    }

    [Test] public async Task Code_IBooleanResolvable_NonEmptySource_Truthy()
        => await Assert.That(await new code("x", "text", app.type.list[typeof(code)]).AsBooleanAsync(app.actor.list.User.Context)).IsTrue();

    [Test] public async Task Code_IBooleanResolvable_EmptySource_Falsy()
        => await Assert.That(await new code("", "text", app.type.list[typeof(code)]).AsBooleanAsync(app.actor.list.User.Context)).IsFalse();

    [Test] public async Task Code_PlangTypeAttribute_Registered()
    {
        var types = new global::app.type.list.@this(global::PLang.Tests.Session.Code);
        await Assert.That(types.Clr("code")).IsEqualTo(typeof(code));
    }
}
