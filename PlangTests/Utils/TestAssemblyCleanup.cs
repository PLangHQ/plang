using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Services.SettingsService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Utils
{
	[TestClass]
	public static class TestAssemblyCleanup
	{
		public static void CloseAllConnectionsAndClearCache()
		{
			foreach (var entry in SqliteSettingsRepository.InMemoryDbCache)
			{
				entry.Value.Close(); 
			}

			SqliteSettingsRepository.InMemoryDbCache.Clear(); 
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup()
		{

			CloseAllConnectionsAndClearCache();
		}
	}
}
