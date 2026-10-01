using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using status = global::app.type.item.status.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// <c>status</c> — an outcome's code and its reason. Made from a number, its text is the code's standard reason; it
/// compares with a number by its code; it writes as its code when its text says nothing more.
/// </summary>
public class StatusTests
{
    private static async Task<string> Json(global::app.@this app, object value)
    {
        var ctx = app.actor.list.User.Context;
        using var text = new System.IO.MemoryStream();
        await (await app.type.list.Mime("text/plain").Encode(text, ctx.Ok(value), ctx)).IsSuccess();
        return System.Text.Encoding.UTF8.GetString(text.ToArray());
    }

    [Test] public async Task FromANumber_TheTextIsTheStandardReason()
    {
        status notFound = 404;
        await Assert.That(notFound.Code.ToInt32()).IsEqualTo(404);
        await Assert.That(notFound.Text.ToString()).IsEqualTo("Not Found");
        await Assert.That(notFound.Ok.Value).IsFalse();
        await Assert.That(((status)204).Ok.Value).IsTrue();
    }

    [Test] public async Task ComparesWithANumber_ByItsCode()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var serverError = new global::app.data.@this("s", (status)503, context: ctx);
        await Assert.That(await serverError.Compare(ctx.Ok(500))).IsEqualTo(global::app.data.Comparison.Greater);
        await Assert.That(await ctx.Ok(503).Compare(serverError)).IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await serverError.Compare(ctx.Ok(600))).IsEqualTo(global::app.data.Comparison.Less);
    }

    [Test] public async Task WritesAsItsCode_UnlessItsTextIsItsOwn()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(await Json(app, (status)404)).IsEqualTo("404");
        await Assert.That(await Json(app, new status(200, "All good"))).IsEqualTo("{\"code\":200,\"text\":\"All good\"}");
    }

    // An error is born with a status from the number it is given; %!error.status.code% reads it.
    [Test] public async Task AnError_HasItsStatus()
    {
        var error = new global::app.error.ServiceError("gone", "Gone", 410);
        await Assert.That(error.Status.Code.ToInt32()).IsEqualTo(410);
        await Assert.That(error.Status.Text.ToString()).IsEqualTo("Gone");
        await Assert.That(new global::app.error.Error("x").Status.Code.ToInt32()).IsEqualTo(400);
    }
}
