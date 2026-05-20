using TUnit.Core;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Stage 2b — Batch 6: `Path.Authorize(verb)` consults the actor's permission
/// view, asks the channel on miss, signs + stores on grant, surfaces
/// PermissionDenied on refusal, recurses on bad input.
/// Mocks `actor.Permission.Find/Add` and the channel's `Ask`.
public class PathAuthorizeTests
{
    [Test] public Task Authorize_GrantExists_ReturnsOk_NoChannelAsk()           { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task Authorize_StatefulAnswerA_SignsWithAlwaysExpiry_Adds_ReturnsOk() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Authorize_StatefulAnswerY_SignsWithoutExpiry_Adds_ReturnsOk() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Authorize_StatefulAnswerN_ReturnsFail_PermissionDenied() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Authorize_StatefulAnswerGarbage_RecursesWithInvalidPrefix() { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task Authorize_StatelessChannel_BubblesDataAskUnchanged()     { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task Authorize_ConstructedPermission_HasExpectedAppIdActorPathVerbMatch() { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task PermissionDenied_Error_CarriesConstructedPermission()    { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task PermissionDenied_Error_RoundTripsThroughErrorShape()     { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
