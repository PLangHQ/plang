using app.actor.context;
using app.variable;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests Default directly — configure behavior, lifecycle.
/// </summary>
public class DefaultHttpProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_prov_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.System.Context;

    // (Removed Provider_Configure_* tests — the `configure` action dissolved; redirect/timeout/
    // baseurl are now per-request `[Default]` properties resolved by the setting cascade, and the
    // per-(follow,max) client cache removed the "can't change after first request" guard.)

    [Test]
    public async Task Provider_Dispose_DoesNotThrow()
    {
        var provider = new Default();
        provider.Dispose();
        // Double dispose should also be safe
        provider.Dispose();
    }
}
