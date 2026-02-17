using System.Globalization;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.convert;

[Action("todatetime")]
public partial class ToDateTime : IContext
{
    public partial object Value { get; init; }
    public partial string? Format { get; init; }

    public Task<Data> Run()
    {
        try
        {
            DateTime result;

            if (Value is DateTime dt)
            {
                result = dt;
            }
            else if (Value is DateTimeOffset dto)
            {
                result = dto.DateTime;
            }
            else if (Value is string str)
            {
                result = Format != null
                    ? DateTime.ParseExact(str, Format, CultureInfo.InvariantCulture)
                    : DateTime.Parse(str, CultureInfo.InvariantCulture);
            }
            else
            {
                result = Convert.ToDateTime(Value);
            }

            return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.FromName("datetime")));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Cannot convert to DateTime: {ex.Message}", "ConversionError")));
        }
    }
}
