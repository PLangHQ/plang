using app.@event;

namespace app.module.action.mock;

[Action("reset", Cacheable = false)]
public partial class Reset : IContext
{
    public partial data.@this<global::app.mock.@this>? Mock { get; init; }

    public Task<data.@this> Start()
    {
        var mock = Mock?.Peek() as global::app.mock.@this;
        if (mock != null)
        {
            Unbind(mock.EventBindingId);
            mock.Calls.Clear();
        }
        else
        {
            // Clear all mocks — remove all BeforeAction bindings tagged as mock
            var bindings = Context.Events.GetBindings(Trigger.BeforeAction);
            foreach (var binding in bindings)
            {
                if (binding.Targets.OfType<global::app.mock.@this>().Any())
                    Unbind(binding.Id);
            }
        }
        return Task.FromResult(Data());
    }

    // Takes the registered binding off, and the on.start binding it holds.
    private void Unbind(string id)
    {
        foreach (var bound in Context.Events.list.FirstOrDefault(b => b.Id == id)?.Targets.OfType<global::app.@event.binding.@this>() ?? [])
            bound.Remove();
        Context.Events.Unregister(id);
    }
}
