using PLang.Attributes;

namespace PLang.Building.Model
{

	public class CachingHandler
	{
		[DefaultValueAttribute(50)]
		public long TimeInMilliseconds { get; set; }
		[DefaultValueAttribute(null)]
		public string? CacheKey { get; set; } = null;
		[DefaultValueAttribute(0)]
		public int CachingType { get; set; } = 0;
	}
}
