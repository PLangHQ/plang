using App.Variables;

namespace App.modules.math;

[Action("divide")]
public partial class Divide : IContext
{
    public partial Data.@this A { get; init; }
    public partial Data.@this B { get; init; }

    public Task<Data.@this> Run()
    {
        var divisor = MathHelper.ToDouble(B.Value);
        if (divisor == 0)
            return Task.FromResult(Error(
                new App.Errors.ValidationError("Division by zero", "DivisionByZero")));

        var result = MathHelper.ToDouble(A.Value) / divisor;
        return Task.FromResult(Data(MathHelper.PreserveType(result, A.Value, B.Value)));
    }
}
