using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.convert;

[Action("tobool")]
public partial class ToBool : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        bool result;

        if (Value is bool b)
        {
            result = b;
        }
        else if (Value is string str)
        {
            result = str.Equals("true", StringComparison.OrdinalIgnoreCase)
                || str == "1"
                || str.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            try
            {
                result = Convert.ToBoolean(Value);
            }
            catch
            {
                result = Value != null;
            }
        }

        return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.FromName("bool")));
    }
}
