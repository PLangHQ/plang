using PLang.Runtime2.Core;

namespace PLang.Runtime2.Context;

/// <summary>
/// Trust levels for actors in the system.
/// Higher values indicate more trust/permissions.
/// </summary>
public enum TrustLevel
{
    /// <summary>
    /// End user - least trusted.
    /// </summary>
    User = 1,

    /// <summary>
    /// External service - intermediate trust.
    /// </summary>
    Service = 2,

    /// <summary>
    /// System/app operator - most trusted.
    /// </summary>
    System = 3
}

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// Actors have different trust levels: System (highest), Service, User (lowest).
/// </summary>
public sealed class Actor : IAsyncDisposable
{
    /// <summary>
    /// Name of the actor ("System", "Service", or "User").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Trust level of this actor.
    /// </summary>
    public TrustLevel TrustLevel { get; }

    /// <summary>
    /// The PLang execution context owned by this actor.
    /// </summary>
    public PLangContext Context { get; }

    /// <summary>
    /// IO channels owned by this actor.
    /// </summary>
    public IO.IO IO { get; }

    /// <summary>
    /// Back-reference to the engine.
    /// </summary>
    public Engine Engine { get; }

    public Actor(string name, TrustLevel trustLevel, Engine engine)
    {
        Name = name;
        TrustLevel = trustLevel;
        Engine = engine;
        Context = new PLangContext(engine.AppContext)
        {
            CallStack = new CallStack()
        };
        Context.Actor = this;
        IO = new IO.IO(engine.Serializers);
    }

    public async ValueTask DisposeAsync()
    {
        Context.Dispose();
        await IO.DisposeAsync();
    }
}
