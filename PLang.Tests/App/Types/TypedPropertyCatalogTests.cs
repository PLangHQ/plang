namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// The catalog the LLM reads emits, per type, its properties annotated with their types,
// so member access type-checks (`image(path) => …, Path(path)`; `path.Exists`).
// Kind vocabulary appears only for developer-meaningful kinds (number's int/decimal/double/long).
//
// Stage 1 lands the catalog mechanism (Entry.Kinds + Properties rendering). The
// image / number-specific assertions in this file are exercised against synthetic
// fixture types so the wiring is verified without waiting for Stage 3/5.

public class TypedPropertyCatalogTests
{
    [global::app.Attributes.PlangType("kind-fixture-image")]
    public sealed class FixtureImage
    {
        public static string Shape => "string";
        public static string? Build(object? value) => "jpg";
        [global::app.LlmBuilder]
        public global::app.type.path.@this? Path { get; init; }
    }

    [global::app.Attributes.PlangType("kind-fixture-number")]
    public sealed class FixtureNumber
    {
        public static string Shape => "string";
        public static System.Collections.Generic.IReadOnlyList<string> Kinds { get; }
            = new[] { "int", "decimal", "double", "long" };
    }

    [global::app.Attributes.PlangType("kind-fixture-bare")]
    public sealed class FixtureBare
    {
        public static string Shape => "string";
    }

    private global::app.type.list.@this _types = null!;

    [Before(Test)]
    public void Setup()
    {
        _types = new global::app.type.list.@this();
        _types.Assemblies.Add(typeof(TypedPropertyCatalogTests).Assembly);
    }

    private global::app.builder.type.Entry? FindEntry(string name)
    {
        foreach (var e in _types.BuildTypeEntries(null))
            if (e.Name == name) return e;
        return null;
    }

    [Test]
    public async Task Catalog_ImageEntry_ListsPathPropertyWithItsType()
    {
        var image = FindEntry("kind-fixture-image");
        await Assert.That(image).IsNotNull();
        await Assert.That(image!.Properties).IsNotNull();
        var pathProp = image.Properties!.FirstOrDefault(p => p.Name == "path");
        await Assert.That(pathProp).IsNotNull();
        await Assert.That(pathProp!.TypeName).IsEqualTo("path");
    }

    [Test]
    public async Task Catalog_TypeProperties_RenderTypeAnnotation_PerProperty()
    {
        // BuildTypeEntries surfaces each [LlmBuilder]-marked property with its
        // PLang type name (the typed-property annotation that lets LLM-driven
        // dot navigation type-check).
        var image = FindEntry("kind-fixture-image");
        await Assert.That(image).IsNotNull();
        await Assert.That(image!.Properties!.Any(p => p.TypeName == "path")).IsTrue();
    }

    [Test]
    public async Task Catalog_KindVocabulary_ShownForNumber_IntDecimalDoubleLong()
    {
        var number = FindEntry("kind-fixture-number");
        await Assert.That(number).IsNotNull();
        await Assert.That(number!.Kinds).IsNotNull();
        await Assert.That(number.Kinds!).Contains("int");
        await Assert.That(number.Kinds!).Contains("decimal");
        await Assert.That(number.Kinds!).Contains("double");
        await Assert.That(number.Kinds!).Contains("long");
    }

    [Test]
    public async Task Catalog_KindVocabulary_NotShownForImage()
    {
        // Image has a Build(value)→kind hook but no developer-meaningful kind
        // vocabulary (the LLM doesn't pick jpg/png/gif — the file does).
        // No static Kinds property ⇒ Entry.Kinds null ⇒ catalog renders no
        // "(kinds: …)" suffix.
        var image = FindEntry("kind-fixture-image");
        await Assert.That(image!.Kinds).IsNull();
    }

    [Test]
    public async Task Catalog_HighLevelTypeAppearsAlone_WhenNoKind()
    {
        // A bare scalar type (no Kinds, no properties) appears in the catalog
        // with just its shape — no kind suffix, no property list.
        var bare = FindEntry("kind-fixture-bare");
        await Assert.That(bare).IsNotNull();
        await Assert.That(bare!.Kinds).IsNull();
        await Assert.That(bare.Properties).IsNull();
    }
}
