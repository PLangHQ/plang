using PLang.Runtime2.Engine;

namespace PLang.Runtime2.Engine.Context;

/// <summary>
/// Represents an actor in the system with its own context and IO channels.
/// </summary>
public sealed class Actor : IAsyncDisposable
{
    /// <summary>
    /// Name of the actor ("System", "Service", or "User").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The PLang execution context owned by this actor.
    /// </summary>
    public PLangContext Context { get; }

    /// <summary>
    /// Named channels owned by this actor.
    /// </summary>
    public PLang.Runtime2.Engine.Channels.EngineChannels Channels { get; }

    /// <summary>
    /// Back-reference to the engine.
    /// </summary>
    public Engine Engine { get; }

    /// <summary>
    /// Resolves an actor by name using the engine.
    /// Convention: types with this signature are auto-resolved by the source generator.
    /// </summary>
    public static Actor? Resolve(string name, Engine engine) => engine.GetActor(name).Actor;

    /// <summary>
    /// Valid values for LLM action summaries.
    /// Convention: types with this property get their values shown in builder summaries.
    /// </summary>
    public static string[] ValidValues => ["user", "service", "system"];

    public Actor(string name, Engine engine)
    {
        Name = name;
        Engine = engine;
        Context = new PLangContext(engine)
        {
            CallStack = new CallStack()
        };
        Context.Actor = this;
        Channels = new PLang.Runtime2.Engine.Channels.EngineChannels(engine);
    }

    public async ValueTask DisposeAsync()
    {
        Context.Dispose();
        await Channels.DisposeAsync();
    }
}
