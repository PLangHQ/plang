using System.Runtime.Serialization;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Events;
using PLang.Models;

namespace PLang.Building.Model;

public class GoalStep
{
    public GoalStep()
    {
        Custom = new Dictionary<string, object>();
    }

    public string Text { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string? LlmText { get; set; }

    public string? Comment { get; set; }
    public string ModuleType { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string PrFileName { get; set; }
    public string RelativePrPath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AbsolutePrFilePath { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string AppStartupPath { get; set; }

    public int Indent { get; set; }
    public bool Execute { get; set; }
    public bool RunOnce { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime? Executed { get; set; }

    public DateTime Generated { get; set; }

    [DefaultValue("true")] public bool WaitForExecution { get; set; } = true;

    public string? LoggerLevel { get; set; }
    public List<ErrorHandler>? ErrorHandlers { get; set; }
    public CachingHandler? CacheHandler { get; set; }
    public CancellationHandler? CancellationHandler { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PreviousText { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Reload { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public GoalStep? NextStep
    {
        get
        {
            if (Goal.GoalSteps.Count > Index + 1) return Goal.GoalSteps[Index + 1];

            return null;
        }
    }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public Goal Goal { get; set; }

    public Dictionary<string, object> Custom { get; set; } = new();

    public int Number { get; set; }

    [JsonIgnore]
    [IgnoreDataMemberAttribute]
    [System.Text.Json.Serialization.JsonIgnore]
    public int Index { get; set; }

    public int LineNumber { get; set; }
    public LlmRequest LlmRequest { get; set; }
    public EventBinding? EventBinding { get; set; } = null;
    public bool IsEvent { get; set; } = false;
    public string Hash { get; set; }
    public string BuilderVersion { get; set; }
}