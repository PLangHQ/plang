using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public class Return
{
    public object? Value { get; set; }
    public IError? Error { get; set; }
    public bool Success => Error == null;

    public static implicit operator bool(Return r) => r.Success;

    public override string ToString() =>
        Success ? Value?.ToString() ?? "Success" : $"Error: {Error?.Message}";

    public T? GetValue<T>() => Value is T typed ? typed : default;

    public Return Merge(Return other)
    {
        if (other.Value == null) return this;

        var myData = Value as List<Data> ?? new();
        var otherData = other.Value as List<Data> ?? new();

        foreach (var data in otherData)
        {
            var existing = myData.FindIndex(d => string.Equals(d.Name, data.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                myData[existing] = data;
            else
                myData.Add(data);
        }

        return new Return { Value = myData };
    }
}
