using System.Text.Json.Serialization;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.Core;

public sealed partial class Action
{
    [JsonIgnore]
    public EntityEvents Events { get; } = new();
    [Store, LlmBuilder, Debug, Default]
    public string Class { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public string Method { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public List<Data> Parameters { get; init; } = new();

    [Store, LlmBuilder, Debug, Default]
    public List<Data>? Return { get; init; }
}
