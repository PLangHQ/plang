using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TextType = global::app.type.text.@this;

namespace PLang.Tests.App.TypeKindStrict.TextTypeTests;

// `text` mirrors `image`, text-backed. `text.Build` extracts the file extension
// as the kind — same shape as image.Build.
public class TextBuildHookTests
{
    [Test] public async Task Build_ReadmeDotMd_ReturnsMd()
        => await Assert.That(TextType.Build("readme.md")).IsEqualTo("md");

    [Test] public async Task Build_NotesNoExtension_ReturnsNull()
        => await Assert.That(TextType.Build("notes")).IsNull();

    [Test] public async Task Build_VarReference_ReturnsNull()
        => await Assert.That(TextType.Build("%var%")).IsNull();

    [Test] public async Task Build_PageWithQueryString_ReturnsHtmlLowercase()
        => await Assert.That(TextType.Build("page.HTML?v=1")).IsEqualTo("html");

    [Test] public async Task Build_Null_ReturnsNull()
        => await Assert.That(TextType.Build(null)).IsNull();

    [Test] public async Task Build_RelativePathString_ReturnsExtension()
        => await Assert.That(TextType.Build("../report.md")).IsEqualTo("md");
}
