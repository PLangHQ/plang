namespace app.modules.matrix.provider;

// Test provider interfaces — registered via app.Code.Register in the matrix runner.
public interface IFakeProvider : global::app.modules.code.ICode
{
    string Echo(string s);
}

public interface IUnregisteredProvider : global::app.modules.code.ICode
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

[global::app.modules.Action("providerprop")]
public partial class ProviderProp : global::app.modules.IContext
{
    [global::app.modules.Code]
    public partial IFakeProvider Fake { get; }

    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Fake.Echo("hi")));
}

[global::app.modules.Action("providermissing")]
public partial class ProviderMissing : global::app.modules.IContext
{
    [global::app.modules.Code]
    public partial IUnregisteredProvider Missing { get; }

    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Missing.Hello()));
}
