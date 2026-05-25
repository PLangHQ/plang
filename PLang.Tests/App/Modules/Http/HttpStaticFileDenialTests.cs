using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Modules.Http;

/// <summary>
/// Stage 5 — Batch 9. <c>http/code/Default.cs</c> static-file denial paths.
///
/// Most adversarial surface in the audit: untrusted HTTP input → filesystem
/// read. The IFileProvider wrapper holds <c>Path</c> internally and routes
/// reads through <c>path.ReadText()</c>.
/// </summary>
public class HttpStaticFileDenialTests
{
    [Test] public async Task StaticFile_RequestWithDotDotTraversal_DeniedByAuthGate()
    {
        // GET /static/../../../etc/passwd → AuthGate denial, 403/404 response.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task StaticFile_RequestForInRootFile_ServedSilently()
    {
        // GET /static/index.html where it exists in served root → no Ask, content returned.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
