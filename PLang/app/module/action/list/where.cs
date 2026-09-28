using app.module.action.condition;

namespace app.module.action.list;

/// <summary>
/// Filters by a scoped predicate: the predicate's bare field name resolves against the subject — each element
/// of a list (<c>where %users% age &gt; 20</c> keeps the elements whose <c>age</c> passes), a dict itself (kept
/// or dropped). The value answers (<c>item.Where</c>); a scalar has no fields to scope into.
/// </summary>
[Action("where", Cacheable = false)]
public partial class Where : IContext
{
    [IsNotNull]
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    /// <summary>The bare field name the predicate scopes against (e.g. "age").</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Field { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<Operator>> Operator { get; init; }
    /// <summary>The right-hand comparison value of the predicate.</summary>
    public partial data.@this Value { get; init; }

    public Task<data.@this> Start() => ListName.Use(name => name.Use<app.type.item.@this>(Context,
        subject => subject.Where(Field, Operator, Value, Context)));
}
