using app.variable;
using app.@event;

namespace app.module.mock;

[Action("reset", Cacheable = false)]
public partial class Reset : IContext
{
    public partial data.@this<global::app.mock.@this>? Mock { get; init; }

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
                if (binding.Targets.OfType<global::app.mock.@this>().Any())
                {
                    Context.Events.Unregister(binding.Id);
                }
            }
        }
        return Task.FromResult(Data());
    }
}
