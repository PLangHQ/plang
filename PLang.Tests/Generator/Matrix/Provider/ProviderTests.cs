namespace PLang.Tests.Generator.Matrix.Provider;

// Matrix entries for [Provider] properties — eager init from app.Providers in ExecuteAsync.
// v4 contract: provider properties are NOT parameter-sourced; they're injected before Run().
// Generator emits a non-lazy backing field assigned via Providers.Get<T>() before any Run() call.

public class ProviderPropTests
{
    // Registered provider → __<Name>_backing is set in ExecuteAsync before Run() runs.
    [Test] public async Task ProviderProp_Registered_InjectedBeforeRun() => Assert.Fail("Not implemented");

    // Provider property reads as the registered instance — same reference across reads (no per-read resolution).
    [Test] public async Task ProviderProp_ReadTwice_SameInstance() => Assert.Fail("Not implemented");
}

public class ProviderMissingTests
{
    // Unregistered provider → ExecuteAsync short-circuits with Data.FromError, Run() never invoked.
    [Test] public async Task ProviderMissing_Unregistered_ShortCircuitsWithError() => Assert.Fail("Not implemented");

    // Error returned describes which provider type was unresolvable.
    [Test] public async Task ProviderMissing_ErrorMessage_IdentifiesProviderType() => Assert.Fail("Not implemented");
}
