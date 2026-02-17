using System.Text;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.convert;

[Action("tobase64")]
public partial class ToBase64 : IContext
{
    public partial object Value { get; init; }

    public Task<Data> Run()
    {
        string result;

        if (Value is byte[] bytes)
        {
            result = Convert.ToBase64String(bytes);
        }
        else if (Value is string str)
        {
            result = Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
        }
        else
        {
            result = Convert.ToBase64String(Encoding.UTF8.GetBytes(Value.ToString() ?? ""));
        }

        return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.String));
    }
}
