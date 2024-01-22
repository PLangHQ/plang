using PLang.Interfaces;
using System.Runtime.Caching;

namespace PLang.Services.CachingService
{
    public class InMemoryCaching : AppCache
    {
        public InMemoryCaching(PLangAppContext context) : base(context) { }

        public override async Task<object?> Get(string key)
        {
			return MemoryCache.Default.Get(key);
        }

        public override async Task Set(string key, object value, TimeSpan slidingExpiration)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.SlidingExpiration = slidingExpiration;
            MemoryCache.Default.Set(key, value, policy);
        }
        public override async Task Set(string key, object value, DateTimeOffset absoluteExpiration)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = absoluteExpiration;
            MemoryCache.Default.Set(key, value, policy);
        }

		public override async Task Remove(string key)
		{
			MemoryCache.Default.Remove(key);
		}
	}
}
