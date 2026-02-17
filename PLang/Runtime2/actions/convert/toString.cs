using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.convert;

[Action("tostring")]
public partial class ToString : IContext
{
    public partial object? Value { get; init; }
    public partial string? Format { get; init; }

    public Task<Data> Run()
    {
        string result;

        if (Value == null)
        {
            result = "";
        }
        else if (Value is IFormattable formattable)
        {
            result = formattable.ToString(Format, System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            result = Value.ToString() ?? "";
        }

        return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.String));
    }
}
