using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Interfaces;
using PLang.Modules.CachingModule;

namespace PLangTests.Modules.CachingModule
{
	[TestClass]
	public class ProgramTests
	{
		[TestMethod]
		public async Task SetCacheWithAbsoluteTime_Test()
		{
			var appCache = Substitute.For<IAppCache>();
			var p = new Program(appCache);

			string key = "key";
			string value = "value";
			var time = 60;

			await p.SetForAbsoluteExpiration(key, value, time);

			await appCache.Received(1).Set(key, value, Arg.Any<DateTimeOffset>());
		}

		[TestMethod]
		public async Task SetCacheWithSlidingTime_Test()
		{
			var appCache = Substitute.For<IAppCache>();
			var p = new Program(appCache);

			string key = "key";
			string value = "value";
			var time = 60;

			await p.SetForSlidingExpiration(key, value, time);

			await appCache.Received(1).Set(key, value, Arg.Any<TimeSpan>());
		}

		[TestMethod]
		public async Task GetCache()
		{
			var appCache = Substitute.For<IAppCache>();
			var p = new Program(appCache);

			string key = "key";

			var result = await p.Get(key);

			await appCache.Received(1).Get(key);
		}
	}
}
