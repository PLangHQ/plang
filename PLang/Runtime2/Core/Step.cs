using System.Text.Json.Serialization;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.Core;

/// <summary>
/// Represents a step within a goal for Runtime2.
/// </summary>
public sealed partial class Step
{
    [Store, LlmBuilder, Debug, Default]
    public int Index { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string Text { get; init; } = "";

    [Store, Debug, Default]
    public int LineNumber { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public int Indent { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    [Store, Debug, Default]
    public Actions Actions { get; init; } = new();

    [Store, Debug, Default]
    public string? OnErrorGoal { get; init; }

    [Store, Debug]
    public string? Hash { get; init; }

    [Store, Debug]
    public string? PreviousHash { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string? Intent { get; init; }

    [Store, Debug, Default]
    public ErrorHandler? OnError { get; init; }

    [Store, Debug, Default]
    public CacheSettings? Cache { get; init; }

    [Store, Debug, Default]
    public int? Timeout { get; init; }

    [Store, Debug]
    public List<Info> Errors { get; init; } = new();

    [Store, Debug]
    public List<Info> Warnings { get; init; } = new();

    [Store, Debug, Default]
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
            Actions = new Actions(Actions.Select(a => new Action
            {
                Module = a.Module,
                ActionName = a.ActionName,
                Parameters = new List<Data>(a.Parameters),
                Return = a.Return != null ? new List<Data>(a.Return) : null
            })),
            OnErrorGoal = OnErrorGoal,
            WaitForExecution = WaitForExecution,
            Goal = Goal,
            Hash = Hash,
            PreviousHash = PreviousHash,
            Intent = Intent,
            OnError = OnError,
            Cache = Cache,
            Timeout = Timeout,
            Errors = new List<Info>(Errors),
            Warnings = new List<Info>(Warnings)
        };
    }

    public override string ToString() => $"[{Index}] {Text}";
}
