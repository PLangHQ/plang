using PLang.Attributes;

namespace PLang.Building.Model
{
	public class RetryHandler
	{
		[DefaultValueAttribute(null)]
		public int? RetryCount { get; set; }
		[DefaultValueAttribute(null)]
		public int? RetryDelayInMilliseconds { get; set; }

	}
}
