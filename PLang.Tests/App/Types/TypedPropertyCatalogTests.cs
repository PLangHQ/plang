namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// The catalog the LLM reads emits, per type, its properties annotated with their types,
// so member access type-checks (`image(path) => …, Path(path)`; `path.Exists`).
// Kind vocabulary appears only for developer-meaningful kinds (number's int/decimal/double/long).

public class TypedPropertyCatalogTests
{
    [Test] public async Task Catalog_ImageEntry_ListsPathPropertyWithItsType()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Catalog_TypeProperties_RenderTypeAnnotation_PerProperty()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Catalog_KindVocabulary_ShownForNumber_IntDecimalDoubleLong()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Catalog_KindVocabulary_NotShownForImage()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Catalog_HighLevelTypeAppearsAlone_WhenNoKind()
        => throw new global::System.NotImplementedException();
}
