namespace app.modules.matrix.withdefault;

public enum MatrixEnum { A, B, C }

[global::app.modules.Action("stringwithdefault")]
public partial class StringWithDefault : global::app.modules.IContext
{
    [global::app.modules.Default("hello")]
    public partial global::app.Data.@this<string> Greeting { get; init; }
    public Task<global::app.Data.@this> Run() => Task.FromResult<global::app.Data.@this>(Greeting);
}

[global::app.modules.Action("intwithdefault")]
public partial class IntWithDefault : global::app.modules.IContext
{
    [global::app.modules.Default(42)]
    public partial global::app.Data.@this<int> Count { get; init; }
    public Task<global::app.Data.@this> Run() => Task.FromResult<global::app.Data.@this>(Count);
}

[global::app.modules.Action("enumwithdefault")]
public partial class EnumWithDefault : global::app.modules.IContext
{
    [global::app.modules.Default(MatrixEnum.A)]
    public partial global::app.Data.@this<MatrixEnum> Choice { get; init; }
    public Task<global::app.Data.@this> Run() => Task.FromResult<global::app.Data.@this>(Choice);
}

[global::app.modules.Action("boolwithdefault")]
public partial class BoolWithDefault : global::app.modules.IContext
{
    [global::app.modules.Default(false)]
    public partial global::app.Data.@this<bool> Flag { get; init; }
    public Task<global::app.Data.@this> Run() => Task.FromResult<global::app.Data.@this>(Flag);
}
