using PLang.Attributes;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using System.Diagnostics;
using System.Runtime.Serialization;
using static PLang.Modules.BaseBuilder;
using static PLang.Utils.StepHelper;

namespace PLang.Building.Model
{

	public class GoalStep : VariableContainer
	{
		public GoalStep()
		{
			
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
		
		[LlmIgnore]
		public string RelativePrPath { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string AbsolutePrFilePath { get; set; }

		[LlmIgnore]
		public string InstructionHash { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public bool HasChanged { get; set; } = true;

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string AppStartupPath { get; set; }

		[LlmIgnore]
		public int Indent { get; set; }
		
		[LlmIgnore]
		public bool Execute { get; set; }

		[LlmIgnore]
		public bool RunOnce { get; set; }
		[LlmIgnore]
		public string Confidence { get; set; }
		[LlmIgnore]
		public string Inconsistency { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public DateTime? Executed { get; set; }

		[LlmIgnore]
		public DateTime Generated { get; set; }

		[DefaultValue("true")]
		public bool WaitForExecution { get; set; } = true;
		public string? LoggerLevel { get; set; }
		public List<ErrorHandler>? ErrorHandlers { get; set; }
		[LlmIgnore] 
		public bool Retry { get; set; }
		[LlmIgnore]
		public int RetryCount { get; set; } = 1;

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string? BuildProcess { get; set; } = null;

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public List<IError> ValidationErrors { get; set; } = new();

		public CachingHandler? CacheHandler { get; set; }
		public CancellationHandler? CancellationHandler { get; set; }
		
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public string? PreviousText { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public bool Reload { 
			get; 
			set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public CallbackInfo? Callback { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public GoalStep? NextStep
		{
			get
			{
				if (Goal.GoalSteps.Count > Index + 1)
				{
					return Goal.GoalSteps[Index + 1];
				}
				else
				{
					return null;
				}
			}
		}

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public GoalStep? PreviousStep {
			get
			{
				if (Index - 1 >= 0)
				{
					return Goal.GoalSteps[Index - 1];
				}
				else
				{
					return null;
				}
			}
		}

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public Goal Goal { get; set; }

		[LlmIgnore]
		public int Number
		{
			get;
			set;
		}

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public int Index
		{
			get;
			set;
		}

		[LlmIgnore]
		public int LineNumber { get; set; }

		[LlmIgnore]
		public LlmRequest LlmRequest { get; set; }

		[LlmIgnore]
		public EventBinding? EventBinding { get; set; } = null;

		[LlmIgnore]
		public bool IsEvent { get; set; } = false;
		
		[LlmIgnore]
		public string Hash { get; set; }

		[LlmIgnore]
		public string BuilderVersion { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public object? PrFile { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public Instruction? Instruction { get; set; }

		[LlmIgnore]
		public string RelativeGoalPath { get; set; }
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public bool IsValid { get; set; } = false;
		protected override GoalStep? GetStep()
		{
			return this;
		}
		protected override Goal? GetParent()
		{
			return Goal;
		}

		[IgnoreWhenInstructed]
		public string UniqueId { get; set; }

		[IgnoreWhenInstructed]
		public Stopwatch Stopwatch { get; set; }

		public string UserIntent { get; internal set; }

		public (IGenericFunction? Function, IError? Error) GetFunction(IPLangFileSystem fileSystem)
		{
			var result = InstructionCreator.Create(AbsolutePrFilePath, fileSystem);
			if (result.Error != null || result.Instruction == null) return (null,  result.Error);

			Instruction = result.Instruction!;
			Instruction.Step = this;
			result.Instruction!.Function.Instruction = Instruction;

			return (result.Instruction!.Function, null);
		}

		protected override void SetVariableOnEvent(Variable goalVariable)
		{
			if (Goal.IsEvent && Goal.ParentGoal != null)
			{
				Goal.ParentGoal.AddVariable(goalVariable);
			}
		}
	}
}
