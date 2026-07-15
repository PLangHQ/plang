using app.module.action.build.code;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Unit tests for the catalog-description shape recognizer used by the validate
/// pipeline to skip parameters whose Value is schema metadata, not a real value.
///
/// Anchored on this fact: <see cref="app.module.@this.Describe"/> renders parameter
/// values as one of four shapes — <c>"X"</c>, <c>"X?"</c>, <c>"X = default"</c>,
/// <c>"%var% X"</c> (and combinations). When the catalog itself is fed back through
/// validate (BuilderValidateValid smoke test), every Value is one of these. Tests
/// here pin each shape end-to-end and the negative cases that prove the typeName
/// anchor blocks LLM-emitted real values from being misclassified.
/// </summary>
public class IsCatalogDescriptionTests
{
    // --- Match-true shapes ---

    [Test]
    public async Task Bare_TypeName_Matches()
        => await Assert.That(Default.IsCatalogDescription("int", "int")).IsTrue();

    [Test]
    public async Task Nullable_Suffix_Matches()
        => await Assert.That(Default.IsCatalogDescription("int?", "int")).IsTrue();

    [Test]
    public async Task TypeName_With_Default_Matches()
        => await Assert.That(Default.IsCatalogDescription("int = 1", "int")).IsTrue();

    [Test]
    public async Task Nullable_With_Default_Matches()
        => await Assert.That(Default.IsCatalogDescription("bool? = false", "bool")).IsTrue();

    [Test]
    public async Task Var_Prefix_Matches()
        => await Assert.That(Default.IsCatalogDescription("%var% string", "string")).IsTrue();

    [Test]
    public async Task Var_Prefix_With_Default_Matches()
        => await Assert.That(Default.IsCatalogDescription("%var% string = \"hi\"", "string")).IsTrue();

    [Test]
    public async Task Generic_TypeName_Matches()
        => await Assert.That(Default.IsCatalogDescription("list<int>", "list<int>")).IsTrue();

    [Test]
    public async Task Generic_TypeName_Nullable_Matches()
        => await Assert.That(Default.IsCatalogDescription("list<int>?", "list<int>")).IsTrue();

    [Test]
    public async Task Surrounding_Whitespace_Trimmed()
        => await Assert.That(Default.IsCatalogDescription("  int = 1  ", "int")).IsTrue();

    // --- Match-false (real LLM values cannot trip the guard) ---

    [Test]
    public async Task LiteralValue_DoesNotMatch_StringSchema()
        // The LLM emits "hello" for a string-typed slot. Must not be classified
        // as a description — it's a value to coerce as-is.
        => await Assert.That(Default.IsCatalogDescription("hello", "string")).IsFalse();

    [Test]
    public async Task TypeName_Mismatch_DoesNotMatch()
        // Value=description for `int` but schema slot is `string`. Cross-type
        // descriptions never match — typeName is the anchor.
        => await Assert.That(Default.IsCatalogDescription("int = 1", "string")).IsFalse();

    [Test]
    public async Task Empty_TypeName_DoesNotMatch()
        => await Assert.That(Default.IsCatalogDescription("int", "")).IsFalse();

    [Test]
    public async Task Trailing_Junk_After_TypeName_DoesNotMatch()
        // `intish` would prefix-match `int` if we ignored the trailing chars.
        // The shape grammar permits only "?", " = ...", or end-of-string.
        => await Assert.That(Default.IsCatalogDescription("intish", "int")).IsFalse();

    [Test]
    public async Task Number_Value_DoesNotMatch_IntSchema()
        // The string "5" is a concrete value, not a description.
        => await Assert.That(Default.IsCatalogDescription("5", "int")).IsFalse();
}
