namespace app.modules.matrix.markers;

[global::app.modules.action("icontexthandler")]
public partial class IContextHandler : global::app.modules.IContext
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Context != null));
}

[global::app.modules.action("ichannelhandler")]
public partial class IChannelHandler : global::app.modules.IContext, global::app.modules.IChannel
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Channel != null));
}

[global::app.modules.action("iactionhandler")]
public partial class IActionHandler : global::app.modules.IContext, global::app.modules.IAction
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Action != null));
}

[global::app.modules.action("istephandler")]
public partial class IStepHandler : global::app.modules.IContext, global::app.modules.IStep
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Step != null));
}

[global::app.modules.action("istatichandler")]
public partial class IStaticHandler : global::app.modules.IContext, global::app.modules.IStatic
{
    public Task<global::app.data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Static != null));
}
