using PLang.Runtime2.Engine.Channels;

namespace PLang.Tests.Runtime2.Serialization;

public class SerializerRegistryTests
{
    [Test]
    public async Task Constructor_RegistersDefaultSerializers()
    {
        var registry = new EngineSerializers();

        await Assert.That(registry.GetByContentType("application/json")).IsNotNull();
        await Assert.That(registry.GetByContentType("text/plain")).IsNotNull();
    }

    [Test]
    public async Task Default_ReturnsJsonSerializer()
    {
        var registry = new EngineSerializers();

        var defaultSerializer = registry.Default;

        await Assert.That(defaultSerializer.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task Default_CanBeSet()
    {
        var registry = new EngineSerializers();
        var textSerializer = registry.Text;

        registry.Default = textSerializer;

        await Assert.That(registry.Default).IsEqualTo(textSerializer);
    }

    [Test]
    public async Task Default_SetNull_ThrowsArgumentNullException()
    {
        var registry = new EngineSerializers();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await Task.Run(() => registry.Default = null!);
        });
    }

    [Test]
    public async Task Json_ReturnsJsonSerializer()
    {
        var registry = new EngineSerializers();

        var json = registry.Json;

        await Assert.That(json.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task Text_ReturnsTextSerializer()
    {
        var registry = new EngineSerializers();

        var text = registry.Text;

        await Assert.That(text.ContentType).IsEqualTo("text/plain");
    }

    [Test]
    public async Task Register_AddsSerializer()
    {
        var registry = new EngineSerializers();
        var customSerializer = new CustomSerializer();

        registry.Register(customSerializer);

        await Assert.That(registry.GetByContentType("custom/type")).IsEqualTo(customSerializer);
    }

    [Test]
    public async Task Register_AddsToExtensionLookup()
    {
        var registry = new EngineSerializers();
        var customSerializer = new CustomSerializer();

        registry.Register(customSerializer);

        await Assert.That(registry.GetByExtension(".custom")).IsEqualTo(customSerializer);
    }

    [Test]
    public async Task GetByContentType_ExactMatch_ReturnsSerializer()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByContentType("application/json");

        await Assert.That(serializer).IsNotNull();
        await Assert.That(serializer!.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task GetByContentType_WithCharset_ReturnsSerializer()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByContentType("application/json; charset=utf-8");

        await Assert.That(serializer).IsNotNull();
        await Assert.That(serializer!.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task GetByContentType_AlternativeJsonType_ReturnsJsonSerializer()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByContentType("text/json");

        await Assert.That(serializer).IsNotNull();
        await Assert.That(serializer!.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task GetByContentType_CaseInsensitive()
    {
        var registry = new EngineSerializers();

        var lower = registry.GetByContentType("application/json");
        var upper = registry.GetByContentType("APPLICATION/JSON");
        var mixed = registry.GetByContentType("Application/Json");

        await Assert.That(lower).IsNotNull();
        await Assert.That(upper).IsNotNull();
        await Assert.That(mixed).IsNotNull();
    }

    [Test]
    public async Task GetByContentType_Unknown_ReturnsNull()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByContentType("unknown/type");

        await Assert.That(serializer).IsNull();
    }

    [Test]
    public async Task GetByExtension_ExactMatch_ReturnsSerializer()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByExtension(".json");

        await Assert.That(serializer).IsNotNull();
    }

    [Test]
    public async Task GetByExtension_WithoutDot_AddsDot()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByExtension("json");

        await Assert.That(serializer).IsNotNull();
    }

    [Test]
    public async Task GetByExtension_TextExtension_ReturnsTextSerializer()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByExtension(".txt");

        await Assert.That(serializer).IsNotNull();
        await Assert.That(serializer!.ContentType).IsEqualTo("text/plain");
    }

    [Test]
    public async Task GetByExtension_CaseInsensitive()
    {
        var registry = new EngineSerializers();

        var lower = registry.GetByExtension(".json");
        var upper = registry.GetByExtension(".JSON");

        await Assert.That(lower).IsNotNull();
        await Assert.That(upper).IsNotNull();
    }

    [Test]
    public async Task GetByExtension_Unknown_ReturnsNull()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetByExtension(".xyz");

        await Assert.That(serializer).IsNull();
    }

    [Test]
    public async Task GetOrDefault_WithContentType_ReturnsSerializer()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetOrDefault("text/plain");

        await Assert.That(serializer.ContentType).IsEqualTo("text/plain");
    }

    [Test]
    public async Task GetOrDefault_NullContentType_ReturnsDefault()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetOrDefault(null);

        await Assert.That(serializer).IsEqualTo(registry.Default);
    }

    [Test]
    public async Task GetOrDefault_EmptyContentType_ReturnsDefault()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetOrDefault("");

        await Assert.That(serializer).IsEqualTo(registry.Default);
    }

    [Test]
    public async Task GetOrDefault_UnknownContentType_ReturnsDefault()
    {
        var registry = new EngineSerializers();

        var serializer = registry.GetOrDefault("unknown/type");

        await Assert.That(serializer).IsEqualTo(registry.Default);
    }

    [Test]
    public async Task ContentTypes_ReturnsAllRegisteredTypes()
    {
        var registry = new EngineSerializers();

        var types = registry.ContentTypes.ToList();

        await Assert.That(types).Contains("application/json");
        await Assert.That(types).Contains("text/plain");
        await Assert.That(types).Contains("text/json");
    }

    [Test]
    public async Task Extensions_ReturnsAllRegisteredExtensions()
    {
        var registry = new EngineSerializers();

        var extensions = registry.Extensions.ToList();

        await Assert.That(extensions).Contains(".json");
        await Assert.That(extensions).Contains(".txt");
    }

    private class CustomSerializer : ISerializer
    {
        public string ContentType => "custom/type";
        public string FileExtension => ".custom";

        public Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);

        public Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
            => Task.FromResult<T?>(default);

        public string Serialize(object? value, Type? type = null) => value?.ToString() ?? "";

        public object? Deserialize(string data, Type type) => null;

        public T? Deserialize<T>(string data) => default;
    }
}
