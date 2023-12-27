namespace PLang.Building.Model
{

	public class CachingHandler
	{
		public long TimeInMilliseconds { get; set; }
		public string? CacheKey { get; set; } = null;
		public int CachingType { get; set; } = 0;
	}
}
