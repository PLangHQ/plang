using System.Globalization;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.convert;

[Action("todouble")]
public partial class ToDouble : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        try
        {
            var result = Value is string str
                ? double.Parse(str, CultureInfo.InvariantCulture)
                : Convert.ToDouble(Value, CultureInfo.InvariantCulture);
            return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.FromName("double")));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Cannot convert to double: {ex.Message}", "ConversionError")));
        }
    }
}
