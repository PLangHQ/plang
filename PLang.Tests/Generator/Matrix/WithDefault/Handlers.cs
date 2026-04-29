namespace App.modules.matrix.withdefault;

public enum MatrixEnum { A, B, C }

[global::App.modules.Action("stringwithdefault")]
public partial class StringWithDefault : global::App.modules.IContext
{
    [global::App.modules.Default("hello")]
    public partial global::App.Data.@this<string> Greeting { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Greeting);
}

[global::App.modules.Action("intwithdefault")]
public partial class IntWithDefault : global::App.modules.IContext
{
    [global::App.modules.Default(42)]
    public partial global::App.Data.@this<int> Count { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Count);
}

[global::App.modules.Action("enumwithdefault")]
public partial class EnumWithDefault : global::App.modules.IContext
{
    [global::App.modules.Default(MatrixEnum.A)]
    public partial global::App.Data.@this<MatrixEnum> Choice { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Choice);
}

[global::App.modules.Action("boolwithdefault")]
public partial class BoolWithDefault : global::App.modules.IContext
{
    [global::App.modules.Default(false)]
    public partial global::App.Data.@this<bool> Flag { get; init; }
    public Task<global::App.Data.@this> Run() => Task.FromResult<global::App.Data.@this>(Flag);
}
