using PLang.Attributes;
using PLang.Events;
using PLang.Models;
using System.Runtime.Serialization;

namespace PLang.Building.Model
{

    public class GoalStep
	{
		public GoalStep()
		{
			Custom = new Dictionary<string, object>();
		}

		public string Text { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string? LlmText { get; set; }
		public string? Comment { get; set; }
		public string ModuleType { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public string PrFileName { get; set; }
		public string RelativePrPath { get; set; }
		
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsolutePrFilePath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string AppStartupPath { get; set; }

		public int Indent { get; set; }
		public bool Execute { get; set; }
		public bool RunOnce { get; set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public DateTime? Executed { get; set; }
		public DateTime Generated { get; set; }

		[DefaultValue("true")]
		public bool WaitForExecution { get; set; } = true;
		public ErrorHandler? ErrorHandler { get; set; }
		public RetryHandler? RetryHandler { get; set; }
		public CachingHandler? CacheHandler { get; set; }
		public CancellationHandler? CancellationHandler { get; set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public string? PreviousText { get; set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public bool Reload { get; set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public GoalStep NextStep { get; set; }
		[Newtonsoft.Json.JsonIgnore]
[IgnoreDataMemberAttribute]

		[System.Text.Json.Serialization.JsonIgnore]
		public Goal Goal { get; set; }

		public Dictionary<string, object> Custom { get; set; } = new Dictionary<string, object>();
		public int Number { get; set; }
		public int LineNumber { get; set; }
		public LlmRequest LlmRequest { get; set; }
	}
}
