using app.Variables;

namespace app.modules.math;

[System.ComponentModel.Description("Return the remainder of A divided by B")]
[Action("modulo")]
public partial class Modulo : IContext
{
    public partial data.@this A { get; init; }
    public partial data.@this B { get; init; }

    public Task<data.@this> Run()
    {
        var divisor = MathHelper.ToDouble(B.Value);
        if (divisor == 0)
            return Task.FromResult(Error(
                new app.Errors.ValidationError("Modulo by zero", "DivisionByZero")));

        var result = MathHelper.ToDouble(A.Value) % divisor;
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
