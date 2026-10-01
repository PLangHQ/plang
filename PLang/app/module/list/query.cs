using Query = global::app.module.list.type.query.@this;

namespace app.module.list;

/// <summary>
/// What a query takes from a list — <c>filter %users% where type is "student" and age &gt; 20, group by name,
/// distinct, order by age</c> — in one step. The parts run in SQL's order unless the action's setting
/// (<c>%!list.query.setting.execution%</c>) says <c>written</c>. The list is unchanged: the answer is a new list.
/// </summary>
[Action("query", Cacheable = false)]
public partial class query : IContext
{
    [IsNotNull]
    public partial data.@this<app.type.item.list.@this> List { get; init; }

    [IsNotNull]
    public partial data.@this<Query> Query { get; init; }

    [Default(global::app.module.list.type.query.execution.sql)]
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.list.type.query.execution>> Execution { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await List.Use(list => Query.Use(query => Execution.Use(order => query.Run(list, order, Context)))));
}
