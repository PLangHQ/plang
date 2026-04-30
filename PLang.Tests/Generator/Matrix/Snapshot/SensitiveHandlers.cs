namespace App.modules.matrix.snapshot;

// Handler that mixes a [Sensitive] parameter (e.g. an API key) with a non-sensitive
// one. The snapshot captured on error must mask the sensitive one's PrValue and
// FinalValue while leaving the non-sensitive one in plaintext.

[global::App.modules.Action("sensitivesnapshot")]
public partial class SensitiveSnapshot : global::App.modules.IContext
{
    [global::App.SensitiveAttribute]
    public partial global::App.Data.@this<string> ApiKey { get; init; }
    public partial global::App.Data.@this<string> Endpoint { get; init; }

    public Task<global::App.Data.@this> Run()
    {
        // Touch both so backing fields are set — exercises the FinalValue branch.
        var _ = ApiKey.Value;
        var __ = Endpoint.Value;
        return Task.FromResult(global::App.Data.@this.FromError(
            new global::App.Errors.ServiceError("forced failure", "TestError", 500)));
    }
}
