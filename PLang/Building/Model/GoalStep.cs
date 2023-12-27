using Newtonsoft.Json;
using PLang.Building.Events;

namespace PLang.Building.Model
{

	public class GoalStep
	{
		public GoalStep()
		{
			Custom = new Dictionary<string, object>();
		}

		public string Text { get; set; }
		public string? Comment { get; set; }
		public string ModuleType { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public string PrFileName { get; set; }
		public string RelativePrPath { get; set; }
		[JsonIgnore]
		public string AbsolutePrFilePath { get; set; }
		[JsonIgnore]
		public string AppStartupPath { get; set; }

		public int Indent { get; set; }
		public bool Execute { get; set; }
		public bool RunOnce { get; set; }
		[JsonIgnore]
		public DateTime? Executed { get; set; }
		public DateTime Generated { get; set; }
		
		[DefaultValue("true")]
		public bool WaitForExecution { get; set; } = true;
		public ErrorHandler? ErrorHandler { get; set; }
		public RetryHandler? RetryHandler { get; set; }
		public CachingHandler? CacheHandler { get; set; }
		public CancellationHandler? CancellationHandler { get ; set; }
		[JsonIgnore]
		public string? PreviousText { get; set; }
		[JsonIgnore]
		public bool Reload { get; set; }
		[JsonIgnore]
		public GoalStep NextStep { get; set; }
		[JsonIgnore]
		public Goal Goal { get; set; }
		//public LlmQuestion LlmQuestion { get; set; }
		public Dictionary<string, object> Custom { get; set; } = new Dictionary<string, object>();
		public int Number { get; set; }
		public int LineNumber { get; set; }
		public LlmQuestion LlmQuestion { get; set; }
	}
}
