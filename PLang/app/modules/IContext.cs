using app.Actor.Context;

namespace app.modules;

/// <summary>
/// Capability interface giving action handlers access to the current execution context.
/// Injected automatically by the source generator — handlers never set this manually.
/// </summary>
public interface IContext
{
    Actor.Context.@this Context { get; set; }
}
