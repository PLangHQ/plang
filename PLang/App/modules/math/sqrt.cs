using App.Variables;

namespace App.modules.math;

[Action("sqrt")]
public partial class Sqrt : IContext
{
    public partial object Value { get; init; }

    public Task<Data.@this> Run()
    {
        var input = MathHelper.ToDouble(Value);
        if (input < 0)
            return Task.FromResult(App.Data.@this.FromError(
                new App.Errors.ValidationError("Cannot take square root of negative number", "InvalidInput")));

        var result = Math.Sqrt(input);
        return Task.FromResult(App.Data.@this.Ok(result));
    }
}
