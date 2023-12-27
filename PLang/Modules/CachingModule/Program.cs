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

		public async Task<object> Get(string key)
		{
			return await appCache.Get(key);
		}

		public async Task SetForSlidingExpiration(string key, object value, TimeSpan? slidingExpiration = null)
		{
			if (slidingExpiration == null) slidingExpiration = TimeSpan.FromMinutes(10);
			await appCache.Set(key, value, (TimeSpan) slidingExpiration);
		}
		public async Task SetForAbsoluteExpiration(string key, object value, DateTimeOffset absoluteExpiration)
		{
			await appCache.Set(key, value, absoluteExpiration);
		}

		public async Task RemoveCache(string key)
		{
			await appCache.Remove(key);
		}
	}
}
