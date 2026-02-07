using PLang.Runtime2.Core;

namespace PLang.Runtime2.Context;

public sealed class LoadEvents
{
    private readonly EventType _type;
    private readonly EventScope _system;
    private readonly EventScope _user;

    public LoadEvents(EventType type, EventScope system, EventScope user)
    {
        _type = type;
        _system = system;
        _user = user;
    }

    public async Task<Return> Run(PLangContext context, object target, string? goalName = null)
    {
        // Run system handlers first
        foreach (var binding in _system.Events.GetBindings(_type))
        {
            var result = await binding.Handler(context);
            if (!result.Success && binding.StopOnError) return result;
        }

        // Then user handlers
        foreach (var binding in _user.Events.GetBindings(_type))
        {
            var result = await binding.Handler(context);
            if (!result.Success && binding.StopOnError) return result;
        }

        return new Return();
    }
}

public sealed class MergedEvents
{
    private readonly EventScope _system;
    private readonly EventScope _user;
    private readonly Core.Events? _appEvents;

    public LoadEvents OnBeforeGoalLoad { get; }
    public LoadEvents OnAfterGoalLoad { get; }
    public LoadEvents OnBeforeStepLoad { get; }
    public LoadEvents OnAfterStepLoad { get; }
    public LoadEvents OnBeforeActionLoad { get; }
    public LoadEvents OnAfterActionLoad { get; }

    public MergedEvents(EventScope system, EventScope user, Core.Events? appEvents = null)
    {
        _system = system;
        _user = user;
        _appEvents = appEvents;
        OnBeforeGoalLoad = new LoadEvents(EventType.OnBeforeGoalLoad, system, user);
        OnAfterGoalLoad = new LoadEvents(EventType.OnAfterGoalLoad, system, user);
        OnBeforeStepLoad = new LoadEvents(EventType.OnBeforeStepLoad, system, user);
        OnAfterStepLoad = new LoadEvents(EventType.OnAfterStepLoad, system, user);
        OnBeforeActionLoad = new LoadEvents(EventType.OnBeforeActionLoad, system, user);
        OnAfterActionLoad = new LoadEvents(EventType.OnAfterActionLoad, system, user);
    }

    public IReadOnlyList<EventBinding> GetMatchingBindings(EventType type, string? goalName = null, string? stepText = null)
    {
        var systemBindings = _system.Events.GetMatchingBindings(type, goalName, stepText);
        var userBindings = _user.Events.GetMatchingBindings(type, goalName, stepText);

        var merged = new List<EventBinding>(systemBindings.Count + userBindings.Count);
        merged.AddRange(systemBindings);
        merged.AddRange(userBindings);

        // Also include app-level events for backward compatibility
        if (_appEvents != null)
        {
            var appBindings = _appEvents.GetMatchingBindings(type, goalName, stepText);
            merged.AddRange(appBindings);
        }

        merged.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return merged;
    }
}
