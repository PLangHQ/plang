using PLang.Attributes;
using PLang.Models;

namespace PLang.Building.Model
{
	public class ErrorHandler
	{
		[DefaultValueAttribute(false)]
		[System.ComponentModel.Description("This will cause the code execution to continue to the next step")]
		public bool IgnoreError { get; set; } = false;

		[DefaultValueAttribute(null)]
		public string? Message { get; set; }
		[DefaultValueAttribute(null)]
		public int? StatusCode { get; set; }

		[DefaultValueAttribute(null)]
		[System.ComponentModel.Description("Key can be defined by user")]
		public string? Key { get; set; }

		[DefaultValueAttribute(null)]
		public GoalToCallInfo? GoalToCall { get; set; }
		
		[DefaultValueAttribute(null)]
		public RetryHandler? RetryHandler { get; set; }
		[DefaultValueAttribute(false)]
		[System.ComponentModel.Description("When user wants to run retry on the step before executing GoalToCall")]
		public bool RunRetryBeforeCallingGoalToCall { get; set; }

	}
}
