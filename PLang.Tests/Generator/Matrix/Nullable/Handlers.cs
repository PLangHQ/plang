namespace App.modules.matrix.nullables;

[global::App.modules.Action("stringnullable")]
public partial class StringNullable : global::App.modules.IContext
{
    public partial global::App.Data.@this<string>? Tag { get; init; }
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult<global::App.Data.@this>(Tag ?? global::App.Data.@this.Null("tag"));
}

[global::App.modules.Action("intnullable")]
public partial class IntNullable : global::App.modules.IContext
{
    public partial global::App.Data.@this<int>? Maybe { get; init; }
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult<global::App.Data.@this>(Maybe ?? global::App.Data.@this.Null("maybe"));
}
