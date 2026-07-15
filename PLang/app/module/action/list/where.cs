using app.variable;
using app.module.action.condition;

namespace app.module.action.list;

/// <summary>
/// Filters by a scoped predicate. <c>where</c> is a dict+list capability: the
/// predicate's bare field name resolves against the <em>subject</em>.
///
/// <para>On a <b>list</b> the subject is each element — <c>where %users% age &gt; 20</c>
/// keeps the elements whose <c>age</c> passes (list.where = dict.where per element).
/// On a <b>dict</b> the subject is the dict itself — <c>where %user% age &gt; 20</c>
/// keeps or drops the one dict (bare <c>age</c> → <c>%self.age%</c>). A scalar has
/// no fields to scope into, so <c>5 where age &gt; 20</c> is a clean error.</para>
/// </summary>
[Action("where", Cacheable = false)]
public partial class Where : IContext
{
    [IsNotNull]
    public partial data.@this<app.variable.@this> ListName { get; init; }
    /// <summary>The bare field name the predicate scopes against (e.g. "age").</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Field { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<Operator>> Operator { get; init; }
    /// <summary>The right-hand comparison value of the predicate.</summary>
    public partial data.@this Value { get; init; }

    public async Task<data.@this> Run()
    {
        var subject = await Context.Variable.Get((await ListName.Value()) as app.variable.@this);
        var field = (await Field.Value())!.Clr<string>()!;
        Operator op = (await Operator.Value())!;
        var subjectVal = await subject.Value();

        if (subjectVal is app.type.item.list.@this list)
        {
            // list.where delegates to dict.where per element — subject is each element.
            var kept = new app.type.item.list.@this(Context);
            foreach (var item in list.Items)
                if (await Keep(item, field, op)) kept.Add(item);
            return Context.Ok(kept, Context.Type.Create("list"));
        }

        if (subjectVal is app.type.item.dict.@this)
        {
            // dict.where is the leaf — subject is the dict itself, kept or dropped.
            bool keep = await Keep(subject, field, op);
            return Context.Ok(keep ? subjectVal : null,
                Context.Type.Create("dict"));
        }

        // The apex has no fields to scope into — `5 where age > 20` is meaningless.
        return Context.Error(new app.error.ValidationError(
            $"'where {field} …' needs a list or dict to scope into — '{(await ListName.Value())}' is a {subject.Type.Name}, which has no fields.",
            "WhereOnApex"));
    }

    private async Task<bool> Keep(data.@this subject, string field, Operator op)
        => await op.Evaluate(await subject.Get(field), Value);
}
