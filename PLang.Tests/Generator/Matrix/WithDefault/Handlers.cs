namespace app.modules.matrix.withdefault;

public enum MatrixEnum { A, B, C }

[global::app.modules.action("stringwithdefault")]
public partial class StringWithDefault : global::app.modules.IContext
{
    [global::app.modules.default("hello")]
    public partial global::app.data.@this<string> Greeting { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Greeting);
}

[global::app.modules.action("intwithdefault")]
public partial class IntWithDefault : global::app.modules.IContext
{
    [global::app.modules.default(42)]
    public partial global::app.data.@this<int> Count { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Count);
}

[global::app.modules.action("enumwithdefault")]
public partial class EnumWithDefault : global::app.modules.IContext
{
    [global::app.modules.default(MatrixEnum.A)]
    public partial global::app.data.@this<MatrixEnum> Choice { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Choice);
}

[global::app.modules.action("boolwithdefault")]
public partial class BoolWithDefault : global::app.modules.IContext
{
    [global::app.modules.default(false)]
    public partial global::app.data.@this<bool> Flag { get; init; }
    public Task<global::app.data.@this> Run() => Task.FromResult<global::app.data.@this>(Flag);
}
