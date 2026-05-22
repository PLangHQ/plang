using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 4 — the <c>[PathScheme]</c> marker attribute (<c>app.types.path.PathSchemeAttribute</c>).
/// It is DEFINED on this branch and applied to the built-in scheme handlers for
/// documentation, but NOT consumed: built-ins are registered explicitly by name at App
/// startup. The attribute exists as the contract a future <c>code.load</c> will reflect
/// over to discover scheme handlers in third-party DLLs.
///
/// These tests reflect over the attribute and over the built-in handlers. Reflection is
/// string-tolerant where it must outlive a rename, but the attribute type itself is new
/// and stable — a direct <c>typeof</c> is fine once stage 4 lands.
/// </summary>
public class PathSchemeAttributeTests
{
    /// <summary>Intent: <c>[PathScheme]</c> is declared
    /// <c>[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]</c> — class-only,
    /// repeatable. AllowMultiple is required so HttpPath can carry both <c>http</c> and
    /// <c>https</c>. Read the <c>AttributeUsageAttribute</c> off the attribute type.</summary>
    [Test] public async Task PathSchemeAttribute_HasClassTarget_AllowMultiple()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: the attribute exposes exactly one public string property
    /// (<c>Scheme</c>) and a single-string constructor that sets it. Pins the shape future
    /// reflection-based registration depends on.</summary>
    [Test] public async Task PathSchemeAttribute_ExposesSingleSchemeString()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: a class decorated with two <c>[PathScheme]</c> attributes yields
    /// both scheme strings via <c>GetCustomAttributes</c>. Use the nested
    /// <see cref="TwoSchemeFixture"/> below as the subject so the test does not depend on
    /// HttpPath having landed yet.</summary>
    [Test] public async Task Reflection_FindsBothSchemes_OnMultiplyDecoratedClass()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>FilePath</c> (<c>app.types.path.file.@this</c>) is decorated
    /// <c>[PathScheme("file")]</c>. Documentation-only — the built-in is registered by
    /// explicit name regardless — but the attribute must be present for the future
    /// <c>code.load</c> contract.</summary>
    [Test] public async Task FilePath_Carries_PathSchemeFile_Attribute()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: every <c>[PathScheme]</c>-decorated handler exposes a public
    /// single-string constructor (<c>public @this(string raw)</c>) — the signature the
    /// scheme registry's factory delegate and future reflection-based registration both
    /// rely on. Assert it on <c>FilePath</c>.</summary>
    [Test] public async Task SchemeHandler_Exposes_PublicSingleStringConstructor()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>
    /// Test-only subject for <see cref="Reflection_FindsBothSchemes_OnMultiplyDecoratedClass"/>.
    /// Stands in for a real multi-scheme handler so the reflection assertion does not
    /// couple to HttpPath's existence. The coder applies the real <c>[PathScheme]</c>
    /// attribute here once stage 4 defines it.
    /// </summary>
    private sealed class TwoSchemeFixture { }
}
