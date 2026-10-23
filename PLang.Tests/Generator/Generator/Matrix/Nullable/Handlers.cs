namespace app.module.matrix.nullables;

[global::app.module.Action("stringnullable")]
public partial class StringNullable : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this>? Tag { get; init; }
    public Task<global::app.data.@this> Start() =>
        Task.FromResult<global::app.data.@this>(Tag ?? Context.Null("tag"));
}

[global::app.module.Action("intnullable")]
public partial class IntNullable : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.number.@this>? Maybe { get; init; }
    public Task<global::app.data.@this> Start() =>
        Task.FromResult<global::app.data.@this>(Maybe ?? Context.Null("maybe"));
}
