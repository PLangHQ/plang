namespace app.module.action.mock;

/// <summary>Takes a mock off — the action it mocked runs as itself again — and clears its record of calls.</summary>
[Action("reset", Cacheable = false)]
public partial class Reset : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.@event.binding.mock.@this> Mock { get; init; }

    public async Task<data.@this> Start()
    {
        var mock = (await Mock.Value())!;
        mock.Remove();
        mock.Calls.Clear();
        return Data();
    }
}
