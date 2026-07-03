using app.actor.context;

namespace app.module;

/// <summary>
/// Capability interface giving action handlers access to the current execution context —
/// and the context-carrier marker that value types (dict/list) use to propagate a wired
/// scope onto their entries. The generated action's primary constructor initializes Context
/// (born-with-context, never null); the setter stays for the propagation carriers.
/// </summary>
public interface IContext
{
    actor.context.@this Context { get; set; }
}
