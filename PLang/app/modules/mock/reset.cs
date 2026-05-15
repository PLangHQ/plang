using app.Variables;
using app.Events;

namespace app.modules.mock;

[System.ComponentModel.Description("Remove a specific mock or all active mocks, clearing their interceptors and call history")]
[Action("reset", Cacheable = false)]
public partial class Reset : IContext
{
    public partial data.@this<types.MockHandle>? Mock { get; init; }

    public Task<data.@this> Run()
    {
        if (Mock?.Value != null)
        {
            Context.Events.Unregister(Mock.Value.EventBindingId);
            Mock.Value.Calls.Clear();
        }
        else
        {
            // Clear all mocks — remove all BeforeAction bindings tagged as mock
            var bindings = Context.Events.GetBindings(EventType.BeforeAction);
            foreach (var binding in bindings)
            {
                if (binding.Targets.OfType<types.MockHandle>().Any())
                {
                    Context.Events.Unregister(binding.Id);
                }
            }
        }
        return Task.FromResult(Data());
    }
}
