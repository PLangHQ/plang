using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 5 — Batch 7. Content-shape verbs (D9a / C9).
///
/// <c>ReadAsBase64()</c> and <c>ReadAsDataUri()</c> live on Path so AuthGate
/// fires inside the verb — handlers never reach for .Absolute. Replaces the
/// raw <c>File.ReadAllBytes</c>+<c>Convert.ToBase64String</c> shape in
/// <c>llm/code/OpenAi.cs</c>.
/// </summary>
public class ContentShapeVerbTests
{
    [Test] public async Task ReadAsBase64_InRoot_ReturnsBase64OfFileBytes()
    {
        // bytes → Convert.ToBase64String, returned in Data<string>.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadAsBase64_OutOfRoot_DeniedAnswer_DoesNotReadFile()
    {
        // "n" answer → Data.Fail; no bytes returned even if file exists OS-side.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadAsBase64_GatesUnderReadVerb_NotWriteOrExecute()
    {
        // Authorize prompt uses Read — content extraction is a read, not execute.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadAsDataUri_InRoot_ReturnsDataUriWithCorrectMimePrefix()
    {
        // "data:image/png;base64,iVBOR..." — MIME derived from path extension.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadAsDataUri_OnUnknownExtension_FallsBackToOctetStream()
    {
        // No MIME mapping → data:application/octet-stream;base64,...
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadAsDataUri_OutOfRoot_DeniedAnswer_ReturnsDataFail()
    {
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
