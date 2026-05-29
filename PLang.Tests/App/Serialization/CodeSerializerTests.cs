namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5
// code/serializer/Default.cs → writer.String(Source). HTML wrap (<pre><code>) deferred
// until an HTML writer ships. The Default covers json + plang + text uniformly.

public class CodeSerializerTests
{
    [Test] public async Task Code_DefaultFormat_EmitsStringSource()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_JsonFormat_ViaStar_RoundTripsSourceAndLanguage()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_PlangFormat_ViaStar_RoundTripsSourceAndLanguage()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_TextFormat_PlainString_NoHtmlMarkup()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Code_SerializerCoverage_PassesPlngGate()
        => throw new global::System.NotImplementedException();
}
