using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.convert;

[Action("tolong")]
public partial class ToLong : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        try
        {
            var result = Convert.ToInt64(Value, System.Globalization.CultureInfo.InvariantCulture);
            return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.FromName("long")));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Cannot convert to long: {ex.Message}", "ConversionError")));
        }
    }
}
