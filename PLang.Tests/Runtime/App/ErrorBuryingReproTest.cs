namespace PLang.Tests.App;

// Regression: when a Data slot of type string is fed an IError value
// (e.g. error.throw Message=%!error% on a build pipeline that just
// captured a NullReferenceException), the conversion-failure wrapper
// must NOT become the primary displayed error. The original IError
// stays as primary; the conversion failure rides on its ErrorChain.
public class ErrorBuryingReproTest
{
    private static System.NullReferenceException ThrownNRE()
    {
        try { object? x = null; var _ = x!.GetType(); return null!; }
        catch (System.NullReferenceException nre) { return nre; }
    }

    [Test] public async Task ErrorAsStringSlot_OriginalErrorStaysPrimary_ConversionFailureGoesOnChain()
    {
        var nre = ThrownNRE();
        var rootError = new global::app.error.ServiceError(
            $"builder.validateStepActions: NullReferenceException: {nre.Message}",
            "NullReferenceException", 500)
        { Exception = nre };

        var d = new global::app.data.@this("!error", rootError);
        var resolved = await d.As<global::app.type.text.@this>(null);

        await resolved.IsFailure();
        // The primary error is the original IError, not the conversion wrapper.
        await Assert.That(resolved.Error!.Key).IsEqualTo("NullReferenceException");
        // The conversion failure rides on the chain — visible but demoted. (Born-native: text
        // is a wrapper type, not a CLR primitive, so the failure surfaces as TypeMismatch.)
        await Assert.That(resolved.Error.ErrorChain.Count).IsEqualTo(1);
        await Assert.That(resolved.Error.ErrorChain[0].Key).IsEqualTo("TypeMismatch");

        // Format() puts the NullReferenceException header at the very top,
        // before any "Error during error handling" footer. If this ever
        // flips, the user is back to reading the conversion scaffolding as
        // the apparent bug.
        var format = resolved.Error.Format();
        var nreHeaderAt = format.IndexOf("NullReferenceException(500)", System.StringComparison.Ordinal);
        var convHeaderAt = format.IndexOf("TypeMismatch(400)", System.StringComparison.Ordinal);
        await Assert.That(nreHeaderAt).IsGreaterThanOrEqualTo(0);
        await Assert.That(convHeaderAt).IsGreaterThan(nreHeaderAt);
    }
}
