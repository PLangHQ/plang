using PLang.Runtime2.Engine.Providers;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests DefaultHttpProvider lifecycle — lazy client creation, configuration, disposal.
/// </summary>
public class DefaultHttpProviderTests
{
    [Test]
    public async Task Provider_LazyCreatesHttpClient_OnFirstSend()
    {
        // HttpClient not created until first SendAsync call
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Provider_Configure_AcceptsValidConfig()
    {
        // Configure with valid Config returns Data.Ok()
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Provider_Configure_RejectsNonConfigSettings()
    {
        // Configure with wrong ISettings type → Data.Fail("InvalidConfig")
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Provider_Dispose_CleansUpHttpClient()
    {
        // Dispose nulls internal client, subsequent sends would create new client
        Assert.Fail("Not implemented");
    }
}
