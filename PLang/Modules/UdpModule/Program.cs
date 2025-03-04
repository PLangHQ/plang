using PLang.Attributes;
using PLang.Interfaces;

namespace PLang.Modules.UdpModule
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
		
	}
}
