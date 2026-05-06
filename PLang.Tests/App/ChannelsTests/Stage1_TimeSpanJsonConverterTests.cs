namespace PLang.Tests.App.ChannelsTests;

// Stage 1 — TimeSpan ISO 8601 JsonConverter (PT30S, PT5M, PT1H30M).
// Architect plan.md L264: "ISO 8601 duration string" via XmlConvert.ToTimeSpan.
// Coverage rows: Stage 1 — TimeSpan JsonConverter writes/reads/rejects.

public class Stage1_TimeSpanJsonConverterTests
{
    [Test]
    public async Task TimeSpanConverter_Write_ProducesPT30S_For30Seconds()
    {
        // TimeSpan.FromSeconds(30) serialises to "PT30S".
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task TimeSpanConverter_Read_ParsesPT5M_To5Minutes()
    {
        // "PT5M" deserialises to TimeSpan.FromMinutes(5).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task TimeSpanConverter_Read_RejectsMalformedInput()
    {
        // "not-a-duration" → JsonException (typed throw), not silent fallback to default.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
