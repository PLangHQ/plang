using app.actor.context;

namespace app.module;

/// <summary>
/// Capability interface giving action handlers access to the current execution context.
/// Injected automatically by the source generator — handlers never set this manually.
/// </summary>
public interface IContext
{
    actor.context.@this Context { get; set; }
}
