namespace app.module.matrix.withdefault;

public enum MatrixEnum { A, B, C }

[global::app.module.Action("stringwithdefault")]
public partial class StringWithDefault : global::app.module.IContext
{
    [global::app.module.Default("hello")]
    public partial global::app.data.@this<global::app.type.text.@this> Greeting { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Greeting);
}

[global::app.module.Action("intwithdefault")]
public partial class IntWithDefault : global::app.module.IContext
{
    [global::app.module.Default(42)]
    public partial global::app.data.@this<global::app.type.number.@this> Count { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Count);
}

[global::app.module.Action("enumwithdefault")]
public partial class EnumWithDefault : global::app.module.IContext
{
    [global::app.module.Default(MatrixEnum.A)]
    public partial global::app.data.@this<global::app.type.choice.@this<MatrixEnum>> Choice { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Choice);
}

[global::app.module.Action("boolwithdefault")]
public partial class BoolWithDefault : global::app.module.IContext
{
    [global::app.module.Default(false)]
    public partial global::app.data.@this<global::app.type.@bool.@this> Flag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Flag);
}
