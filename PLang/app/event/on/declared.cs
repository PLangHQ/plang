namespace app.@event.on;

/// <summary>An event a plang type declares for its items (<c>[Events("closed")]</c> on the window type): made by its
/// name, so a module names its events and never makes an event class of its own.</summary>
public sealed class declared : global::app.@event.@this
{
    internal declared(@this on, string name, binding.list.before before, binding.list.after after) : base(on, name, before, after) { }
}
