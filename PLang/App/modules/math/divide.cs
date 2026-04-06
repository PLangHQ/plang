using App.Engine.Variables;

namespace App.modules.math;

[Action("divide")]
public partial class Divide : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data> Run()
    {
        var divisor = MathHelper.ToDouble(B);
        if (divisor == 0)
            return Task.FromResult(Data.FromError(
                new App.Engine.Errors.ValidationError("Division by zero", "DivisionByZero")));

        var result = MathHelper.ToDouble(A) / divisor;
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
