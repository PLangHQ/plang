namespace app.module;

/// <summary>
/// An action that is a clause of the action before it — <c>on.error</c>, <c>on.cache</c>, <c>on.timeout</c>. Its
/// module mints its program actions as clauses (<see cref="global::app.goal.step.action.clause.@this"/>): bound on
/// the action before them when the program is read, never started as a step.
/// </summary>
public interface IClause
{
    /// <summary>Binds <paramref name="clause"/> — the program's on.* action — on <paramref name="action"/>'s own
    /// events: the ones this kind of clause answers, on the side it answers them.</summary>
    void Bind(global::app.goal.step.action.@this clause, global::app.goal.step.action.@this action);
}
