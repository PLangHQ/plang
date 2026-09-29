using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

// A spelled kind finds its format through the one walk: a format answers to its name, its
// extensions and its MIMEs, so another spelling of the same format is the same kind.
public class KindCanonicalisationTests
{
    [Test] public async Task Markdown_IsMd()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Kind("markdown").Name).IsEqualTo("md");
    }

    [Test] public async Task Jpeg_IsJpg()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Kind("jpeg").Name).IsEqualTo("jpg");
    }

    [Test] public async Task UnknownFrobnicate_IsItsOwnName()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Kind("frobnicate").Name).IsEqualTo("frobnicate");
    }

    [Test] public async Task SpelledTypeKind_FindsTheFormat()
    {
        await using var app = new global::app.@this("/test").Testing();
        var type = app.type.list[new global::app.type.@this("image", "jpeg"), app.actor.list.User.Context];
        await Assert.That(type.kind.Name).IsEqualTo("jpg");
    }

    [Test] public async Task RegisteredFormat_AnswersToItsMimeAndExtensions()
    {
        // A format added at runtime is found by the same walk — nothing hand-written beside it.
        await using var app = new global::app.@this("/test").Testing();
        app.type.list.Add(new global::app.type.kind.@this(
            new global::app.Attributes.FormatAttribute("frobx", "application/x-frobnicate", ".frobx", ".frob"), "binary"));
        await Assert.That(app.type.list.Kind("application/x-frobnicate").Name).IsEqualTo("frobx");
        await Assert.That(app.type.list.Kind(".frob").Name).IsEqualTo("frobx");
    }
}
