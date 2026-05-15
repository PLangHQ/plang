namespace app.modules;

/// <summary>
/// Structural types (Goal, Step, Action) that need cached per-execution Data wrappers.
/// The object owns its own Data representation (OBP).
/// </summary>
public interface IDataWrappable
{
    Data.@this AsData(Actor.Context.@this context);
}
