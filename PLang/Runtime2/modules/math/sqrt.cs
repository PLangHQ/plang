using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        var input = MathHelper.ToDouble(Value);
        if (input < 0)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError("Cannot take square root of negative number", "InvalidInput")));

        var result = Math.Sqrt(input);
        return Task.FromResult(Data.Ok(result));
    }
}
