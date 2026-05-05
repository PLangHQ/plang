namespace PLang.Tests.App.Serializers;

public class MimeRegistrationTests
{
    [Test]
    public async Task Channels_LookupSerializerByMimeType_RoutesAccordingly()
    {
        // App.Channels.Serializers.GetByMimeType(string) returns the matching serializer.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Channels_UnregisteredMimeType_RaisesError()
    {
        // No silent fallback — names + integrity model says hard error.
        // (Default; coder/architect may flip to fallback during review.)
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ApplicationPlangData_Mime_RegisteredAtAppBoot()
    {
        // PlangDataSerializer registers for application/plang+data during App boot.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
