namespace App.modules.matrix.provider;

// Test provider interfaces — registered via app.Providers.Register in the matrix runner.
public interface IFakeProvider : global::App.Providers.IProvider
{
    string Echo(string s);
}

public interface IUnregisteredProvider : global::App.Providers.IProvider
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
    [global::App.modules.Provider]
    public partial IFakeProvider Fake { get; }

    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Fake.Echo("hi")));
}

[global::App.modules.Action("providermissing")]
public partial class ProviderMissing : global::App.modules.IContext
{
    [global::App.modules.Provider]
    public partial IUnregisteredProvider Missing { get; }

    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Missing.Hello()));
}
