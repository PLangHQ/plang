using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Models;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Model;

public record Instruction(object Action)
{
    public string? Text { get; set; }
    public bool Reload { get; set; }
    public LlmRequest LlmRequest { get; set; }
    public bool RunOnBuild { get; set; }

    public GenericFunction[] GetFunctions()
    {
        if (Action == null) return new GenericFunction[0];

        if (Action.GetType() == typeof(JArray))
            return JsonConvert.DeserializeObject<GenericFunction[]>(Action.ToString());

        if (Action.GetType() == typeof(JObject))
        {
            var gf = JsonConvert.DeserializeObject<GenericFunction>(Action.ToString());
            return new[] { gf };
        }

        if (Action.ToString().EndsWith("[]")) return Action as GenericFunction[];
        return new[] { Action as GenericFunction };
    }
}