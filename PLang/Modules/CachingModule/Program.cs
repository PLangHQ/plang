using PLang.Attributes;
using PLang.Interfaces;

namespace PLang.Modules.CachingModule
{
	public class Program : BaseProgram
	{
		private readonly IAppCache appCache;

		public Program(IAppCache appCache)
		{
			this.appCache = appCache;
		}
		[MethodSettings(CanBeCached = false, CanBeAsync = false)]
		public async Task<object?> Get(string key)
		{
			return await appCache.Get(key);
		}
		[MethodSettings(CanBeCached = false)]
		public async Task SetForSlidingExpiration(string key, object value, int timeInSeconds = 60 * 10)
		{
			TimeSpan slidingExpiration = TimeSpan.FromSeconds(timeInSeconds);
			await appCache.Set(key, value, (TimeSpan) slidingExpiration);
		}
		[MethodSettings(CanBeCached = false)]
		public async Task SetForAbsoluteExpiration(string key, object value, int timeInSeconds = 60 * 10)
		{
			DateTimeOffset absoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(timeInSeconds);
			await appCache.Set(key, value, absoluteExpiration);
		}
		[MethodSettings(CanBeCached = false)]
		public async Task RemoveCache(string key)
		{
			await appCache.Remove(key);
		}
	}
}
