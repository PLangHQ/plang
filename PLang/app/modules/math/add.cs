using app.variables;
using ExampleSpec = app.builder.Types.Spec.Example;
using ActionSpec = app.builder.Types.Spec.Action;

namespace app.modules.math;

[Action("add")]
public partial class Add : IContext
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        new ExampleSpec(
            "add 5 and 3, write to %sum%",
            new[]
            {
                new ActionSpec("math",     "add", new() { ["A"] = 5, ["B"] = 3 }),
                new ActionSpec("variable", "set", new() { ["Name"] = "%sum%", ["Value"] = "%!data%" }),
            }),
        new ExampleSpec(
            "set %count% = %count% + 1",
            new[]
            {
                new ActionSpec("math",     "add", new() { ["A"] = "%count%", ["B"] = 1 }),
                new ActionSpec("variable", "set", new() { ["Name"] = "%count%", ["Value"] = "%!data%" }),
            }),
    };

    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public Task<data.@this<object>> Run()
    {
        var result = MathHelper.ToDouble(A.Value) + MathHelper.ToDouble(B.Value);
        return Task.FromResult(data.@this<object>.Ok(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
