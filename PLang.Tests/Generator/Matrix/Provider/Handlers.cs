namespace App.modules.matrix.provider;

// Test provider interfaces — registered via app.Code.Register in the matrix runner.
public interface IFakeProvider : global::App.Code.ICode
{
    string Echo(string s);
}

public interface IUnregisteredProvider : global::App.Code.ICode
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

[global::App.modules.Action("providerprop")]
public partial class ProviderProp : global::App.modules.IContext
{
    [global::App.modules.Code]
    public partial IFakeProvider Fake { get; }

    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Fake.Echo("hi")));
}

[global::App.modules.Action("providermissing")]
public partial class ProviderMissing : global::App.modules.IContext
{
    [global::App.modules.Code]
    public partial IUnregisteredProvider Missing { get; }

    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Missing.Hello()));
}
