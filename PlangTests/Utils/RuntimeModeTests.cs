using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.EnvironmentModule;

namespace PLang.Utils.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class RuntimeModeTests
	{
		[TestCleanup]
		public void Cleanup()
		{
			RuntimeMode.Register([]);
		}

		[TestMethod]
		public void NoFlags_SchedulerEnabled_NotStandby()
		{
			RuntimeMode.Register(["--debug"]);

			Assert.IsFalse(RuntimeMode.Standby);
			Assert.IsTrue(RuntimeMode.SchedulerEnabled);
		}

		[TestMethod]
		public void NoScheduler_DisablesScheduler_NotStandby()
		{
			RuntimeMode.Register(["--no-scheduler"]);

			Assert.IsFalse(RuntimeMode.Standby);
			Assert.IsFalse(RuntimeMode.SchedulerEnabled);
		}

		[TestMethod]
		public void Standby_ImpliesNoScheduler()
		{
			RuntimeMode.Register(["--standby"]);

			Assert.IsTrue(RuntimeMode.Standby);
			Assert.IsFalse(RuntimeMode.SchedulerEnabled);
		}

		[TestMethod]
		public void RegisterStartupParameters_ReadsFlags()
		{
			RegisterStartupParameters.Register(["--standby"]);

			Assert.IsTrue(RuntimeMode.Standby);
			Assert.IsFalse(RuntimeMode.SchedulerEnabled);
		}

		[TestMethod]
		public void EnableScheduler_ClearsNoScheduler_StandbyStays()
		{
			RuntimeMode.Register(["--standby"]);

			RuntimeMode.SetSchedulerEnabled(true);

			Assert.IsTrue(RuntimeMode.SchedulerEnabled);
			Assert.IsTrue(RuntimeMode.Standby);
		}

		[TestMethod]
		public void RegisterWithoutFlags_ResetsEarlierState()
		{
			RuntimeMode.Register(["--standby"]);
			RuntimeMode.Register([]);

			Assert.IsFalse(RuntimeMode.Standby);
			Assert.IsTrue(RuntimeMode.SchedulerEnabled);
		}

		[TestMethod]
		public void RuntimeStats_PlangSection_HasStandbyAndSchedulerEnabled()
		{
			var plang = new PlangRuntimeStats(0, 0, 0, 0, 0, new(), null, 0, 0, null, 0, 0, 0, 0, new(),
				RuntimeMode.Standby, RuntimeMode.SchedulerEnabled);
			Assert.IsFalse(plang.Standby);
			Assert.IsTrue(plang.SchedulerEnabled);

			var json = Newtonsoft.Json.JsonConvert.SerializeObject(plang);
			StringAssert.Contains(json, "\"standby\":false");
			StringAssert.Contains(json, "\"scheduler_enabled\":true");
		}
	}
}
