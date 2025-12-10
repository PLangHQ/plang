using PLang.Attributes;
using PLang.Models;
using System.ComponentModel;

namespace PLang.Modules.PlangModule.Data;


public record PrApp(string Name, string Guid)
{
	public string? Description { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	public List<PrGoal> Goals { get; set; } = [];
	[Newtonsoft.Json.JsonIgnore]
	public List<PrGoal> SetupGoals { get; set; } = [];

	[LlmIgnore]
	[IgnoreWhenInstructed]
	public string AbsolutePath { get; internal set; }
}

public record PrGoal(string Name, string Description, IReadOnlyList<PrStep> Steps)
{
	public string PrFileName = "00. goal.pr";
	public string Path { get; internal set; }
	public string PrPath { get; internal set; }
	public string FolderPath { get; internal set; }
	public string PrFolderPath { get; internal set; }
	[LlmIgnore]
	public string? Comment { get; set; }

	[LlmIgnore]
	[IgnoreWhenInstructed]
	public string AbsolutePath { get; internal set; }

	public bool IsSetup { get; set; }
	public bool IsEvent { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	public PrApp App { get; set; }
}

[Description("Descripes a step formatted a json")]
public record PrStep(
	string Text,
	int Index,
	string Reasoning,
	string Module,
	string FormalizedText,
	string? SecondModule = null,
	int Indents = 0,
	IReadOnlyList<PrComment>? LlmComments = null,
	[param: LlmIgnore]
	string? DeveloperComment = null,
	List<StepEventHandler>? EventHandlers = null, 
	List<StepErrorHandler>? ErrorHandlers = null,
	CacheHandler? CacheHandler = null
)
{
	public PrFunction? Function { get; set; } = null;

}

public enum CachingType { Sliding , Absolute }
public enum CachingLocation { Memory, Disk }
public record CacheHandler(string CacheKey, long TimeInMilliseconds, CachingType CachingType = CachingType.Sliding, CachingLocation Location = CachingLocation.Memory);

public enum EventType { Before, After };

public record StepEventHandler(EventType EventType, GoalToCallInfo GoalToCall);

public record StepErrorHandler(
	string? Message = null,
	[param: Description("contains|equals|not_equal")]
	string MessageComparer = "contains",
	int? StatusCode = null,
	string? Key = null,
	string? ExceptionType = null,
	bool IgnoreError = false,
	RetryHandler? RetryHandler = null
	);

public record RetryHandler(int RetryCount, long WaitInMsForNextRetry);

public enum PrCommentLevel { Improvement, Info, Warning, Error }
[Description("Comments about the user input. e.g. is sql invalid, parameter names wrong, etc.")]
public record PrComment(string Text, PrCommentLevel Level);
public record PrFunction(string Name, Dictionary<string, object> Parameters);
