namespace app.modules.matrix.nullables;

[global::app.modules.Action("stringnullable")]
public partial class StringNullable : global::app.modules.IContext
{
    public partial global::app.Data.@this<string>? Tag { get; init; }
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult<global::app.Data.@this>(Tag ?? global::app.Data.@this.Null("tag"));
}

[global::app.modules.Action("intnullable")]
public partial class IntNullable : global::app.modules.IContext
{
    public partial global::app.Data.@this<int>? Maybe { get; init; }
    public Task<global::app.Data.@this> Run() =>
        Task.FromResult<global::app.Data.@this>(Maybe ?? global::app.Data.@this.Null("maybe"));
}
