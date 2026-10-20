namespace PLang.Tests.App;

// Regression: when a Data slot of type string is fed an global::app.error.@this value
// (e.g. error.throw Message=%!error% on a build pipeline that just
// captured a NullReferenceException), the conversion-failure wrapper
// must NOT become the primary displayed error. The original global::app.error.@this
// stays as primary; the conversion failure rides on its causing list.
public class ErrorBuryingReproTest : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static System.NullReferenceException ThrownNRE()
    {
        try { object? x = null; var _ = x!.GetType(); return null!; }
        catch (System.NullReferenceException nre) { return nre; }
    }

    [Test] public async Task ErrorAsStringSlot_OriginalErrorStaysPrimary_ConversionFailureGoesOnChain()
    {
        var nre = ThrownNRE();
        var rootError = new global::app.error.ServiceError(
            $"build.validate: NullReferenceException: {nre.Message}",
            "NullReferenceException", 500)
        { Exception = nre };

        var d = new global::app.data.@this("!error", rootError, context: app.actor.list.User.Context);
        var resolved = d.As<global::app.type.item.text.@this>(await d.Value<global::app.type.item.text.@this>());

        await resolved.IsFailure();
        // The primary error is the original global::app.error.@this, not the conversion wrapper.
        await Assert.That(resolved.Error!.Key).IsEqualTo("NullReferenceException");
        // The conversion failure rides on the chain — visible but demoted. (Born-native: text
        // is a wrapper type, not a CLR primitive, so the failure surfaces as TypeMismatch.)
        await Assert.That(resolved.Error.list.Count).IsEqualTo(1);
        await Assert.That(resolved.Error.list[0].Key).IsEqualTo("TypeMismatch");

    }

    // An error asked as what it already is passes through: nothing refused, nothing on its chain.
    [Test] public async Task ErrorAskedAsItselfOrItem_PassesThrough()
    {
        var rootError = new global::app.error.ServiceError("the build failed", "BuildFailed", 500);
        var d = new global::app.data.@this("!error", rootError, context: app.actor.list.User.Context);

        await Assert.That(await d.Value<global::app.type.item.@this>()).IsSameReferenceAs(rootError);
        await Assert.That(rootError.Refuses(new global::app.type.@this(typeof(global::app.error.@this)), d)).IsFalse();
        await Assert.That(rootError.list.Count).IsEqualTo(0);
    }

    // Every type's birth answers the same: number has its own courier too.
    [Test] public async Task ErrorAsNumberSlot_OriginalErrorStaysPrimary_ConversionFailureGoesOnChain()
    {
        var rootError = new global::app.error.ServiceError("the build failed", "BuildFailed", 500);

        var d = new global::app.data.@this("!error", rootError, context: app.actor.list.User.Context);
        var resolved = d.As<global::app.type.item.number.@this>(await d.Value<global::app.type.item.number.@this>());

        await resolved.IsFailure();
        await Assert.That(resolved.Error!.Key).IsEqualTo("BuildFailed");
        await Assert.That(resolved.Error.list.Count).IsEqualTo(1);
        await Assert.That(resolved.Error.list[0].Key).IsEqualTo("TypeMismatch");
    }
}
