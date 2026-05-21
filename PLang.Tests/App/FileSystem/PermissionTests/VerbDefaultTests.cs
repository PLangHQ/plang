using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Write = global::App.FileSystem.Permission.Verb.Write;

namespace PLang.Tests.App.FileSystem.PermissionTests;

public class VerbDefaultTests
{
    [Test] public async Task NewVerb_DefaultsToAllNull()
    {
        var v = new Verb();
        await Assert.That(v.Read).IsNull();
        await Assert.That(v.Write).IsNull();
        await Assert.That(v.Delete).IsNull();
    }

    [Test] public async Task NewVerb_WithWriteOnly_OnlyWriteIsSet()
    {
        var v = new Verb { Write = new Write() };
        await Assert.That(v.Read).IsNull();
        await Assert.That(v.Write).IsNotNull();
        await Assert.That(v.Delete).IsNull();
    }
}
