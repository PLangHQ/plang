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
		[System.ComponentModel.Description("Sliding = 0, Absolute = 1")]
		public int CachingType { get; set; } = 0;
		[System.ComponentModel.Description("Location = \"memory\"|\"disk\". Default is \"memory\"")]
		public string? Location { get; set; }
	}
}
