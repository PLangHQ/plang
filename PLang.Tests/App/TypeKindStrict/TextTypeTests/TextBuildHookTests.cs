using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TextTypeTests;

// `text` mirrors `image`, text-backed. PLang/app/type/text/this.Build.cs
// has `static string? Build(object?)` extracting the file extension as the kind.
// Same shape as image.Build (which already exists today).

public class TextBuildHookTests
{
    [Test] public async Task Build_ReadmeDotMd_ReturnsMd()
    {
        // text.Build("readme.md") → "md" (lowercase, no dot).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_NotesNoExtension_ReturnsNull()
    {
        // text.Build("notes") → null. No extension = no kind hint.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_VarReference_ReturnsNull()
    {
        // text.Build("%var%") → null. Build hooks are literal-only; deferred to
        // runtime for %var% references (same as number.Build and image.Build today).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_PageWithQueryString_ReturnsHtmlLowercase()
    {
        // text.Build("page.HTML?v=1") → "html". Strip query/fragment; lowercase.
        // Same shape as image.Build for "a.jpg?v=1" today.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_Null_ReturnsNull()
    {
        // null-safety. text.Build(null) → null without throwing.
        // image.Build is null-safe today; text mirrors it.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Build_RelativePathString_ReturnsExtension()
    {
        // text.Build("../report.md") → "md". The hook is dumb
        // extension extraction (Path.GetExtension-equivalent); no path-traversal
        // weirdness, no security concerns at this layer.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
