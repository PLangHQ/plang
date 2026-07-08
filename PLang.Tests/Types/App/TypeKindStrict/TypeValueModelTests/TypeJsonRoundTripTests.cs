using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

public class TypeJsonRoundTripTests
{
    [Test] public async Task Deserialize_JustName_Works()
    {
        var entity = JsonSerializer.Deserialize<global::app.type.@this>("{\"name\":\"text\"}")!;
        await Assert.That(entity.Name).IsEqualTo("text");
        await Assert.That(entity.Kind?.Name).IsNull();
        await Assert.That(entity.Strict).IsFalse();
    }

    [Test] public async Task Deserialize_FullDict_Works()
    {
        var entity = JsonSerializer.Deserialize<global::app.type.@this>(
            "{\"name\":\"image\",\"kind\":\"gif\",\"strict\":true}")!;
        System.Console.WriteLine($"ENTITY: Name='{entity.Name}' Kind={(entity.Kind == null ? "null" : "'" + entity.Kind?.Name + "'")} Strict={entity.Strict}");
        await Assert.That(entity.Name).IsEqualTo("image");
        await Assert.That(entity.Kind?.Name).IsEqualTo("gif");
        await Assert.That(entity.Strict).IsTrue();
    }
}
