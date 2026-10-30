namespace PLang.Tests;

/// <summary>What a test asks of an actor's events beside what plang itself binds there.</summary>
public static class EventListExtensions
{
    /// <summary>What a program bound on this actor, in the order bound: its events less the system events, the ones a goal
    /// under /system/ made (the os's Hello among them, which every root app's actors load).</summary>
    public static IEnumerable<global::app.@event.@this> Bound(this global::app.@event.list.@this events)
        => events.Items().Where(@event => @event.action?.Step?.Goal.IsSystem != true);
}
