using System.Globalization;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.convert;

[Action("toint")]
public partial class ToInt : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        try
        {
            var result = Convert.ToInt32(Value, CultureInfo.InvariantCulture);
            return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.Int));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Cannot convert to int: {ex.Message}", "ConversionError")));
        }
    }
}
