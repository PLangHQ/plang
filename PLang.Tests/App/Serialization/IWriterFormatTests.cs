namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// IWriter grows a `string Format { get; }` property. Each writer returns its short token
// ("json"/"plang"/"text"/…). The TypedValueNode case calls TypeSerializers.Get(typeName, Format).

public class IWriterFormatTests
{
    [Test] public async Task JsonWriter_Format_IsJsonToken()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PlangWriter_Format_IsPlangToken()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TextWriter_Format_IsTextToken()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Writer_TypedValueNodeCase_CallsLookup_WithOwnFormatToken()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Writer_TypedValueNodeCase_FallsBackToStar_WhenSpecificMissing()
        => throw new global::System.NotImplementedException();
}
