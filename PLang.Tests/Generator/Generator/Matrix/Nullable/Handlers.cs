namespace app.module.matrix.nullables;

[global::app.module.Action("stringnullable")]
public partial class StringNullable : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.text.@this>? Tag { get; init; }
    public Task<global::app.data.@this> Run() =>
        Task.FromResult<global::app.data.@this>(Tag ?? global::app.data.@this.Null("tag"));
}

[global::app.module.Action("intnullable")]
public partial class IntNullable : global::app.module.IContext
{
    public partial global::app.data.@this<global::app.type.item.number.@this>? Maybe { get; init; }
    public Task<global::app.data.@this> Run() =>
        Task.FromResult<global::app.data.@this>(Maybe ?? global::app.data.@this.Null("maybe"));
}
