using System.Text.Json;
using app.Channels.Serializers;

namespace PLang.Tests.App.ChannelsTests;

// Stage 1 — TimeSpan ISO 8601 JsonConverter (PT30S, PT5M, PT1H30M).
// Architect plan.md L264: "ISO 8601 duration string" via XmlConvert.ToTimeSpan.

public class Stage1_TimeSpanJsonConverterTests
{
    private static JsonSerializerOptions Options() => new()
    {
        Converters = { new TimeSpanIso8601() }
    };

    [Test]
    public async Task TimeSpanConverter_Write_ProducesPT30S_For30Seconds()
    {
        var json = JsonSerializer.Serialize(TimeSpan.FromSeconds(30), Options());
        await Assert.That(json).IsEqualTo("\"PT30S\"");
    }

    [Test]
    public async Task TimeSpanConverter_Read_ParsesPT5M_To5Minutes()
    {
        var ts = JsonSerializer.Deserialize<TimeSpan>("\"PT5M\"", Options());
        await Assert.That(ts).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task TimeSpanConverter_Read_RejectsMalformedInput()
    {
        await Assert.That(() => JsonSerializer.Deserialize<TimeSpan>("\"not-a-duration\"", Options()))
            .Throws<JsonException>();
    }
}
