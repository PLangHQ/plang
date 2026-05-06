namespace App.modules.matrix.markers;

[global::App.modules.Action("icontexthandler")]
public partial class IContextHandler : global::App.modules.IContext
{
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Context != null));
}

[global::App.modules.Action("ichannelhandler")]
public partial class IChannelHandler : global::App.modules.IContext, global::App.modules.IChannel
{
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Channel != null));
}

[global::App.modules.Action("iactionhandler")]
public partial class IActionHandler : global::App.modules.IContext, global::App.modules.IAction
{
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Action != null));
}

[global::App.modules.Action("istephandler")]
public partial class IStepHandler : global::App.modules.IContext, global::App.modules.IStep
{
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Step != null));
}

[global::App.modules.Action("istatichandler")]
public partial class IStaticHandler : global::App.modules.IContext, global::App.modules.IStatic
{
    public Task<global::App.Data.@this> Run() =>
        Task.FromResult(global::App.Data.@this.Ok(Static != null));
}
