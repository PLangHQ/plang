using Microsoft.Extensions.Caching.Memory;
using PLang.Attributes;
using PLang.Utils;

namespace PLang.Interfaces
{
	public interface IAppCache
	{
		Task<object?> Get(string key);
		Task Remove(string key);
		Task Set(string key, object value, DateTimeOffset absoluteExpiration);
		Task Set(string key, object value, TimeSpan slidingExpiration);
	}

	public abstract class AppCache : IAppCache
	{
		private readonly PLangAppContext context;

		protected AppCache(PLangAppContext context)
		{
			this.context = context;
		}
		
		[VisibleInheritationAttribute]
		public void Use() {
			context.AddOrReplace(ReservedKeywords.Inject_Caching, this.GetType().FullName);
		}

		public abstract Task<object?> Get(string key);
		public abstract Task Set(string key, object value, DateTimeOffset absoluteExpiration);
		public abstract Task Set(string key, object value, TimeSpan slidingExpiration);
		public abstract Task Remove(string key);

	}
}
