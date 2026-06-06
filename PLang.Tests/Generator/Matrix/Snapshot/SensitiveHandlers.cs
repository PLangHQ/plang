namespace app.module.matrix.snapshot;

// Handler that mixes a [Sensitive] parameter (e.g. an API key) with a non-sensitive
// one. The snapshot captured on error must mask the sensitive one's PrValue and
// FinalValue while leaving the non-sensitive one in plaintext.

[global::app.module.Action("sensitivesnapshot")]
public partial class SensitiveSnapshot : global::app.module.IContext
{
    [global::app.SensitiveAttribute]
    public partial global::app.data.@this<global::app.type.text.@this> ApiKey { get; init; }
    public partial global::app.data.@this<global::app.type.text.@this> Endpoint { get; init; }

    public Task<global::app.data.@this> Run()
    {
        // Touch both so backing fields are set — exercises the FinalValue branch.
        var _ = ApiKey.Value;
        var __ = Endpoint.Value;
        return Task.FromResult(global::app.data.@this.FromError(
            new global::app.error.ServiceError("forced failure", "TestError", 500)));
    }
}
