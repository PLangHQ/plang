namespace PLang.Tests;

/// <summary>What a test asks of an actor's events beside what plang itself binds there.</summary>
public static class EventListExtensions
{
    /// <summary>What a program bound on this actor, in the order bound: its events less the system events (the os's Hello
    /// among them, which every root app's actors load).</summary>
    public static IEnumerable<global::app.@event.@this> Bound(this global::app.@event.list.@this events)
        => events.Items().Where(@event => !@event.IsSystem());

    /// <summary>Whether a goal of plang's own made the event: one under /system/, or a file in the os's system folder (the
    /// test parent's root is "/", under which the os's goals are written by their whole location).</summary>
    public static bool IsSystem(this global::app.@event.@this @event)
        => @event.action?.Step?.Goal is { } goal
           && (goal.IsSystem || goal.Path?.Absolute is { } file
               && file.StartsWith(@event.Actor.App.filesystem.os.Combine("system").Absolute + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
