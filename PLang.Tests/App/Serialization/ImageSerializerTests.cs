namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5 (the format-asymmetric proof)
// image/serializer/text.cs → path placeholder.
// image/serializer/protobuf.cs → raw bytes (stub until protobuf writer ships).
// image/serializer/Default.cs → base64 (covers json + plang).
// One Image instance, three wire shapes by writer Format token.

public class ImageSerializerTests
{
    [Test] public async Task Image_TextFormat_RendersPathPlaceholder()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_TextFormat_Base64Source_PlaceholderIsBareLabel()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_JsonFormat_DefaultFallback_RendersBase64()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_PlangFormat_DefaultFallback_RendersBase64()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_ProtobufFormat_RendersRawBytes_StubInPlace()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_RoundTrip_JsonBase64_PreservesBytesAndMime()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_SerializerCoverage_PassesPlngGate()
        => throw new global::System.NotImplementedException();
}
