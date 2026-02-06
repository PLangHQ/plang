using System.Text.Json.Serialization;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

/// <summary>
/// Represents a step within a goal for Runtime2.
/// </summary>
public sealed class Step
{
    public int Index { get; init; }

    public string Text { get; init; } = "";

    public int LineNumber { get; init; }

    public int Indent { get; init; }

    public string? Comment { get; init; }

    /// <summary>
    /// Actions to execute for this step.
    /// </summary>
    public List<IAction> Actions { get; init; } = new();

    public string? OnErrorGoal { get; init; }

    public string? Hash { get; init; }

    public string? PreviousHash { get; init; }

    public string? Intent { get; init; }

    public Data? Data { get; init; }

    public ErrorHandler? OnError { get; init; }

    public CacheSettings? Cache { get; init; }

    /// <summary>
    /// Timeout in seconds after which to cancel execution.
    /// </summary>
    public int? Timeout { get; init; }

    public List<Info> Errors { get; init; } = new();

    public List<Info> Warnings { get; init; } = new();

    public bool WaitForExecution { get; init; } = true;

    [JsonIgnore]
    public Goal? Goal { get; set; }

    public Step Clone()
    {
        return new Step
        {
            Index = Index,
            Text = Text,
            LineNumber = LineNumber,
            Indent = Indent,
            Comment = Comment,
            Actions = Actions.Select(a => (IAction)new Action
            {
                Class = a.Class,
                Method = a.Method,
                Parameters = new List<Data>(a.Parameters),
                Return = new Return { Variables = a.Return.Variables != null ? new List<Data>(a.Return.Variables) : null }
            }).ToList(),
            OnErrorGoal = OnErrorGoal,
            WaitForExecution = WaitForExecution,
            Goal = Goal,
            Hash = Hash,
            PreviousHash = PreviousHash,
            Intent = Intent,
            Data = Data != null ? new Data(Data.Name, Data.Value, Data.TypeInfo) : null,
            OnError = OnError,
            Cache = Cache,
            Timeout = Timeout,
            Errors = new List<Info>(Errors),
            Warnings = new List<Info>(Warnings)
        };
    }

    public override string ToString() => $"[{Index}] {Text}";
}
