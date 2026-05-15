namespace app.modules.matrix.nullables;

[global::app.modules.action("stringnullable")]
public partial class StringNullable : global::app.modules.IContext
{
    public partial global::app.data.@this<string>? Tag { get; init; }
    public Task<global::app.data.@this> Run() =>
        Task.FromResult<global::app.data.@this>(Tag ?? global::app.data.@this.Null("tag"));
}

[global::app.modules.action("intnullable")]
public partial class IntNullable : global::app.modules.IContext
{
    public partial global::app.data.@this<int>? Maybe { get; init; }
    public Task<global::app.data.@this> Run() =>
        Task.FromResult<global::app.data.@this>(Maybe ?? global::app.data.@this.Null("maybe"));
}
