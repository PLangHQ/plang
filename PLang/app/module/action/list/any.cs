namespace app.module.action.list;

/// <summary>
/// Checks if any item in a list matches a condition on a property.
/// Usage: any %list% where "level" != "high", write to %hasNonHigh%
/// </summary>
[Action("any")]
public partial class Any : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<global::app.data.Operator>> Operator { get; init; }
    public partial data.@this Value { get; init; }

    // The value compared to is what the step gave: a %variable% that holds nothing is the answer.
    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() => data.@this<global::app.type.item.@bool.@this>.From(
        await Value.Given(value => ListName.Use(name => name.Use<app.type.item.list.@this>(Context, list => list.Any(Key, Operator, value, Context)))));
}
