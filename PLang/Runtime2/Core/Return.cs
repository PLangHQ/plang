using System.Text.Json.Serialization;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed class Return
{
    public List<Data>? Variables { get; set; }

    [JsonIgnore]
    public ErrorInfo? Error { get; set; }

    [JsonIgnore]
    public Properties? Properties { get; set; }

    [JsonIgnore]
    public List<Info>? Warnings { get; set; }
}
