using PLang.Attributes;

namespace PLang.Building.Model
{
	public class ErrorHandler
	{
		[DefaultValueAttribute(false)]
		public bool IgnoreErrors { get; set; } = false;
		[DefaultValueAttribute(null)]
		public Dictionary<string, string>? OnExceptionContainingTextCallGoal { get; set; } = null;
		[DefaultValueAttribute(false)]
		public bool ContinueToNextStep { get; set; } = false;
		[DefaultValueAttribute(false)]
		public bool EndGoal { get; set; } = false;
	}
}
