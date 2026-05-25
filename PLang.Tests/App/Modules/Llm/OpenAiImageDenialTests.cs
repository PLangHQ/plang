using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Modules.Llm;

/// <summary>
/// Stage 5 — Batch 9. <c>llm/code/OpenAi.cs</c> image attachment denial.
///
/// D9a — image attachment lifts to <c>path.ReadAsBase64()</c>. Out-of-root
/// image path → AuthGate denial; no bytes ship to the LLM provider.
/// </summary>
public class OpenAiImageDenialTests
{
    [Test] public async Task ImageAttachment_PathOutsideRoot_DeniedAnswer_NotIncludedInRequest()
    {
        // ReadAsBase64 fails → request to OpenAI never includes the image content.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ImageAttachment_PathInRoot_BytesShipBase64Encoded()
    {
        // In-root image attaches as base64 in the request payload, no Ask.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
