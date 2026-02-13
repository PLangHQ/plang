using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.math;

[Action("modulo")]
public partial class Modulo : IContext
{
    public partial object A { get; init; }
    public partial object B { get; init; }

    public Task<Data> Run()
    {
        var divisor = MathHelper.ToDouble(B);
        if (divisor == 0)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError("Modulo by zero", "DivisionByZero")));

        var result = MathHelper.ToDouble(A) % divisor;
        return Task.FromResult(Data.Ok(MathHelper.PreserveType(result, A, B)));
    }
}
