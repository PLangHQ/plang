namespace PLang.Tests.App.TypedReturnsTests;

// Stage 4 — Per-action Build() implementations.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 4, items 1-3)
// Plan: .bot/typed-action-returns/architect/plan.md (A.6)

public class Stage4_BuildMethodImplsTests
{
    // --- file.read.Build() ---

    [Test]
    public async Task FileRead_Build_LiteralCsvPath_ReturnsOkWithCsv()
        // Build() on file.read{Path="foo.csv"} → Data.Ok("csv").
        => Assert.Fail("Not implemented");

    [Test]
    public async Task FileRead_Build_LiteralJsonPath_ReturnsOkWithJson()
        // Build() on file.read{Path="data.json"} → Data.Ok("json").
        => Assert.Fail("Not implemented");

    [Test]
    public async Task FileRead_Build_LiteralUnknownExtension_FallsBackToOk()
        // Build() on file.read{Path="foo.zzz"} → Data.Ok() (no value; defer to runtime).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task FileRead_Build_NonLiteralPath_ReturnsBareOk()
        // Build() on file.read{Path="%p%"} → Data.Ok() (no value).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task FileRead_Build_LiteralMissingFile_WritesBuildWarning()
        // Build() on file.read{Path="missing.csv"} (file does not exist) — writes BuildWarning to Channel("builder").
        => Assert.Fail("Not implemented");

    [Test]
    public async Task FileRead_Build_LiteralMissingFile_StillReturnsOkWithInferredType()
        // Even with the warning, Build() returns Data.Ok("csv") — the missing file is non-fatal.
        => Assert.Fail("Not implemented");

    // --- llm.query.Build() ---

    [Test]
    public async Task LlmQuery_Build_WithSchema_ReturnsOkWithJson()
        // Build() on llm.query{Schema="<...>"} → Data.Ok("json").
        => Assert.Fail("Not implemented");

    [Test]
    public async Task LlmQuery_Build_WithFormatNoSchema_ReturnsOkWithFormatValue()
        // Build() on llm.query{Format="md"} → Data.Ok("md").
        => Assert.Fail("Not implemented");

    [Test]
    public async Task LlmQuery_Build_NeitherSchemaNorFormat_ReturnsBareOk()
        // Build() on llm.query with no Schema/Format → Data.Ok().
        => Assert.Fail("Not implemented");

    // --- http.request / http.upload .Build() ---

    [Test]
    public async Task HttpRequest_Build_LiteralUrlWithExtension_InfersTypeFromExtension()
        // Build() on http.request{Url="https://api/x.json"} → Data.Ok("json").
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpUpload_Build_NonLiteralUrl_ReturnsBareOk()
        // Build() on http.upload{Url="%endpoint%"} → Data.Ok().
        => Assert.Fail("Not implemented");
}
