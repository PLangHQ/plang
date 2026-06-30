namespace app.module.matrix.provider;

// Test provider interfaces — registered via app.Code.Register in the matrix runner.
public interface IFakeProvider : global::app.module.code.ICode
{
    string Echo(string s);
}

public interface IUnregisteredProvider : global::app.module.code.ICode
{
    string Hello();
}

public sealed class FakeProvider : IFakeProvider
{
    public string Name => "fake";
    public bool IsDefault { get; set; } = true;
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }
    public string Echo(string s) => $"echo:{s}";
}

[global::app.module.Action("providerprop")]
public partial class ProviderProp : global::app.module.IContext
{
    [global::app.module.Code]
    public partial IFakeProvider Fake { get; }

    public Task<global::app.data.@this> Run() =>
        Task.FromResult(Context.Ok(Fake.Echo("hi")));
}

[global::app.module.Action("providermissing")]
public partial class ProviderMissing : global::app.module.IContext
{
    [global::app.module.Code]
    public partial IUnregisteredProvider Missing { get; }

    public Task<global::app.data.@this> Run() =>
        Task.FromResult(Context.Ok(Missing.Hello()));
}
