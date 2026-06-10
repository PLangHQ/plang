using code = global::app.type.code.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// app/type/code/this.cs — Source, Language, IBooleanResolvable = source non-empty.
// Kind is the language ("csharp"/"python"/…); text fallback when language not detected.

public class CodeValueTests
{
    [Test] public async Task Code_FromSourceAndLanguage_StoresBoth()
    {
        var c = new code("Console.WriteLine();", "csharp");
        await Assert.That(c.Source).IsEqualTo("Console.WriteLine();");
        await Assert.That(c.Language).IsEqualTo("csharp");
    }

    [Test] public async Task Code_Resolve_String_DetectsLanguageOrDefaultsToText()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-code-" + System.Guid.NewGuid().ToString("N")[..8]));
        var c1 = code.Resolve("using System;", app.User.Context);
        await Assert.That(c1!.Language).IsEqualTo("csharp");
        var c2 = code.Resolve("plain text", app.User.Context);
        await Assert.That(c2!.Language).IsEqualTo("text");
    }

    [Test] public async Task Code_Build_RecognizedSnippet_ReturnsLanguageKind()
    {
        await Assert.That(code.Build("using System;")).IsEqualTo("csharp");
        await Assert.That(code.Build("def foo:\n  print(1)")).IsEqualTo("python");
        await Assert.That(code.Build("function x() {}")).IsEqualTo("javascript");
    }

    [Test] public async Task Code_Build_UnrecognizedSnippet_ReturnsText()
        => await Assert.That(code.Build("just some prose")).IsEqualTo("text");

    [Test] public async Task Code_IBooleanResolvable_NonEmptySource_Truthy()
        => await Assert.That(await new code("x", "text").AsBooleanAsync()).IsTrue();

    [Test] public async Task Code_IBooleanResolvable_EmptySource_Falsy()
        => await Assert.That(await new code("", "text").AsBooleanAsync()).IsFalse();

    [Test] public async Task Code_PlangTypeAttribute_Registered()
    {
        var types = new global::app.type.catalog.@this();
        await Assert.That(types.ResolveType("code")).IsEqualTo(typeof(code));
    }
}
