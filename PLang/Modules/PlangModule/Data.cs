using PLang.Attributes;
using PLang.Building.Model;
using PLang.Models;
using PLang.Variables;
using System.ComponentModel;
using System.Text;

namespace PLang.Modules.PlangModule.Data;


public record PrApp(string Name, string Guid)
{
	public string? Description { get; set; }
	public string PrPath { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	public List<PrGoal> Goals { get; set; } = [];
	[Newtonsoft.Json.JsonIgnore]
	public List<PrGoal> SetupGoals { get; set; } = [];

	[Newtonsoft.Json.JsonIgnore]
	public GoalToCallInfo GoalToCall { get; set; }

	[LlmIgnore]
	[IgnoreWhenInstructed]
	[Newtonsoft.Json.JsonIgnore]
	public string AbsolutePath { get; internal set; }
}

public record PrGoal(string Name, IReadOnlyList<PrStep> Steps)
{
	[BuilderSchemeAttribute]
	public string? Description { get; set; }

	public string PrFileName = $"{Name}.pr";
	public string Path { get; internal set; }
	public string PrPath { get; internal set; }
	public string FolderPath { get; internal set; }
	public string PrFolderPath { get; internal set; }
	
	public string? DeveloperComment { get; set; }
	public List<PrErrorHandler>? ErrorHandlers { get; set; }
	public IReadOnlyList<PrEventHandler>? BeforeEvents { get; set; }

	public IReadOnlyList<PrEventHandler>? AfterEvents { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[IgnoreWhenInstructed]
	public string AbsolutePath { get; internal set; }

	public bool IsSetup { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	public PrApp App { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	public string Text { get
		{
			int stepIndex = 0;
			StringBuilder sb = new();

			if (!string.IsNullOrWhiteSpace(DeveloperComment)) AppendLine(sb, $"/ {this.DeveloperComment}\n");
			AppendLine(sb, this.Name);

			foreach (var step in Steps)
			{
				if (!string.IsNullOrWhiteSpace(step.DeveloperComment)) AppendLine(sb, $"/ {step.DeveloperComment}");
				AppendLine(sb, "- ".PadLeft(step.Indent, ' ') + step.Text, stepIndex++);
			}
			return sb.ToString();

		} }

	public void AppendLine(StringBuilder sb, string txt, int? index = null)
	{
		if (index != null) sb.Append($"{index}.");
		sb.AppendLine(txt);
	}
}

public record LlmStep(string Reasoning, PrFunction Function,
	IReadOnlyList<LlmComment>? LlmComments = null,
	RunAndForget? PrRunAndForget = null, 
	CacheHandler? CacheHandler = null,
	IReadOnlyList<PrEventHandler>? BeforeEventHandlers = null,
	IReadOnlyList<PrEventHandler>? IntervalEventHandlers = null,

	IReadOnlyList<PrEventHandler>? AfterEventHandlers = null,
	IReadOnlyList<PrErrorHandler>? ErrorHandlers = null, CancellationHandler? CancellationHandler = null);

public record PrStep(
	string Text,
	string FormalizedText,
	int Index,
	string Reasoning,
	string Module,
	PrFunction Function,
	int Indent,
	string? DeveloperComment = null,
	IReadOnlyList<LlmComment>? LlmComments = null,
	IReadOnlyList<PrEventHandler>? BeforeEventHandlers = null,

	IReadOnlyList<PrEventHandler>? IntervalEventHandlers = null,
	IReadOnlyList<PrEventHandler>? AfterEventHandlers = null,
	IReadOnlyList<PrErrorHandler>? ErrorHandlers = null,
	CacheHandler? CacheHandler = null,
	RunAndForget? PrRunAndForget = null,

	List<RuntimeVariable>? RuntimeVariables = null,
	List<object>? Llm = null
);

public enum CachingType { Sliding , Absolute }
public enum CachingLocation { Memory, Disk }
public record CacheHandler(string CacheKey, long TimeInMilliseconds, CachingType CachingType = CachingType.Sliding, CachingLocation Location = CachingLocation.Memory);

public enum EventType { Before, After, Interval };
public enum MatchType { Exact, Regex, StartsWith, Contains }
public record PropertyMatch(string Value, MatchType Type = MatchType.Exact);
public record PrEventHandler(GoalToCallInfo GoalToCall, long? IntervalInMsForInterval = null, 
	Dictionary<string, PropertyMatch>? WhenPropertyEquals = null);

public record StepEventHandler(EventType EventType, long IntervalInMsForInterval, GoalToCallInfo GoalToCall);

public record PrErrorHandler(
	GoalToCallInfo GoalToCall, 
	string? Message = null,
	[param: Description("null|contains|equals|not_equal")]
	string? MessageComparer = null,
	int? StatusCode = null,
	[param: Description(@"Key=""*"" when catch all errors")]
	string? Key = null,
	string? ExceptionType = null,
	bool IgnoreError = false,
	RetryHandler? RetryHandler = null
	);

public enum RetryType { Before, After };
public record RetryHandler(int RetryCount, long WaitInMsForNextRetry, RetryType RetryBeforeOrAfterErrorHandler = RetryType.Before);

public enum PrCommentLevel { Improvement, Inconsistency, Info, Warning, Error }
[Description("Comments about the user input. e.g. is sql invalid, parameter names wrong, etc.")]
public record LlmComment(string Text, PrCommentLevel Level);
public record PrFunction(string Name, Dictionary<string, object> Parameters);

[Description("Also know as async goal or dont wait")]
public record RunAndForget(long WaitForMsBeforeRun = 50, GoalToCallInfo? AfterExecutionRunGoal = null);

