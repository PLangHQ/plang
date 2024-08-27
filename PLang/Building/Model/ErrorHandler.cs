using PLang.Attributes;
using PLang.Models;

namespace PLang.Building.Model
{
	public class ErrorHandler
	{
		[DefaultValueAttribute(false)]
		public bool IgnoreError { get; set; } = false;

		[DefaultValueAttribute(null)]
		public string? Message { get; set; }
		[DefaultValueAttribute(null)]
		public int? StatusCode { get; set; }

		[DefaultValueAttribute(null)]
		[System.ComponentModel.Description("Default keys in the system are StepError, ProgramError, StepError. Other keys can be defined by user")]
		public string? Key { get; set; }

		[DefaultValueAttribute(null)]
		public GoalToCall? GoalToCall { get; set; }
		[DefaultValueAttribute(null)]
		public Dictionary<string, object?>? GoalToCallParameters { get; set; }

		[DefaultValueAttribute(null)]
		public RetryHandler? RetryHandler { get; set; }
		
	}
}
