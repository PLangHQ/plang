using App.Variables;
using App.Events;

namespace App.modules.mock;

[Action("reset", Cacheable = false)]
public partial class Reset : IContext
{
    public partial Data.@this<types.MockHandle>? Mock { get; init; }

    public Task<Data.@this> Run()
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
