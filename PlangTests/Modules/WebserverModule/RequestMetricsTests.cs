using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebserverModule;

namespace PLangTests.Modules.WebserverModule
{
	[TestClass]
	public class RequestMetricsTests
	{
		private static readonly DateTime T0 = new(2026, 11, 10, 20, 41, 10, DateTimeKind.Utc);

		[TestInitialize]
		public void Init() => RequestMetrics.Reset();

		private static List<Dictionary<string, object?>> Minutes(Dictionary<string, object?> s) => (List<Dictionary<string, object?>>)s["minutes"]!;
		private static List<Dictionary<string, object?>> Routes(Dictionary<string, object?> s) => (List<Dictionary<string, object?>>)s["routes"]!;
		private static Dictionary<string, object?> Kinds(Dictionary<string, object?> s) => (Dictionary<string, object?>)s["kinds"]!;

		[TestMethod]
		public void SlotFor_IsLogSpacedAndCapped()
		{
			Assert.AreEqual(0, RequestMetrics.SlotFor(0.2));
			Assert.AreEqual(0, RequestMetrics.SlotFor(1));
			Assert.AreEqual(4, RequestMetrics.SlotFor(2.4));
			Assert.AreEqual(RequestMetrics.Slots, RequestMetrics.SlotFor(600000));
		}

		[TestMethod]
		public void Counts_StatusClasses_AndMax()
		{
			RequestMetrics.Record("goal", "/a", 200, 10, T0);
			RequestMetrics.Record("goal", "/a", 302, 20, T0);
			RequestMetrics.Record("goal", "/a", 404, 5, T0);
			RequestMetrics.Record("goal", "/a", 500, 4100, T0);
			var s = RequestMetrics.Snapshot(T0.AddMinutes(-5), T0);
			var m = Minutes(s).Single();
			Assert.AreEqual(4L, m["count"]);
			Assert.AreEqual(1L, m["s2xx"]);
			Assert.AreEqual(1L, m["s3xx"]);
			Assert.AreEqual(1L, m["s4xx"]);
			Assert.AreEqual(1L, m["s5xx"]);
			Assert.AreEqual(4100L, m["max_ms"]);
			Assert.AreEqual("2026-11-10T20:41:00Z", m["t"]);
		}

		[TestMethod]
		public void Percentiles_AreWithinTwelvePercent()
		{
			for (int i = 1; i <= 1000; i++) RequestMetrics.Record("goal", "/a", 200, i, T0);
			var m = Minutes(RequestMetrics.Snapshot(T0.AddMinutes(-5), T0)).Single();
			Assert.AreEqual(500, (long)m["p50_ms"]!, 500 * 0.13);
			Assert.AreEqual(950, (long)m["p95_ms"]!, 950 * 0.13);
			Assert.AreEqual(990, (long)m["p99_ms"]!, 990 * 0.13);
		}

		[TestMethod]
		public void Poll_IsCountedUnderKindsOnly()
		{
			RequestMetrics.Record("poll", "/x", 200, 30000, T0);
			RequestMetrics.Record("goal", "/a", 200, 10, T0);
			var s = RequestMetrics.Snapshot(T0.AddMinutes(-5), T0);
			Assert.AreEqual(1L, Kinds(s)["poll"]);
			Assert.AreEqual(1L, Minutes(s).Single()["count"]);
			Assert.AreEqual(1, Routes(s).Count);
			Assert.IsTrue((long)Minutes(s).Single()["max_ms"]! < 100);
		}

		[TestMethod]
		public void Routes_OrderedByCount_AndCappedAtTwenty()
		{
			for (int r = 0; r < 30; r++)
				for (int i = 0; i <= r; i++) RequestMetrics.Record("goal", "/r" + r, 200, 3, T0);
			var routes = Routes(RequestMetrics.Snapshot(T0.AddMinutes(-5), T0));
			Assert.AreEqual(20, routes.Count);
			Assert.AreEqual("/r29", routes[0]["route"]);
			Assert.AreEqual(30L, routes[0]["count"]);
		}

		[TestMethod]
		public void RouteKeys_CappedAt200_RestGoToOther()
		{
			for (int r = 0; r < 250; r++) RequestMetrics.Record("goal", "/k" + r, 200, 3, T0);
			var s = RequestMetrics.Snapshot(T0.AddMinutes(-5), T0);
			var all = Routes(s);
			Assert.IsTrue(all.Any(x => (string)x["route"]! == "other"));
			Assert.AreEqual(250L, Minutes(s).Single()["count"]);
		}

		[TestMethod]
		public void Window_ExcludesOlderMinutes_AndEmptyMinutesAreLeftOut()
		{
			RequestMetrics.Record("goal", "/a", 200, 3, T0.AddMinutes(-90));
			RequestMetrics.Record("goal", "/a", 200, 3, T0);
			var s = RequestMetrics.Snapshot(T0.AddMinutes(-60), T0);
			Assert.AreEqual(1, Minutes(s).Count);
			var all = RequestMetrics.Snapshot(T0.AddMinutes(-120), T0);
			Assert.AreEqual(2, Minutes(all).Count);
		}

		[TestMethod]
		public void Bucket_IsResetWhenRingWrapsAroundADay()
		{
			RequestMetrics.Record("goal", "/a", 200, 3, T0.AddDays(-1));
			RequestMetrics.Record("goal", "/a", 500, 3, T0);
			var m = Minutes(RequestMetrics.Snapshot(T0.AddMinutes(-5), T0)).Single();
			Assert.AreEqual(1L, m["count"]);
			Assert.AreEqual(1L, m["s5xx"]);
			Assert.AreEqual(0, Minutes(RequestMetrics.Snapshot(T0.AddDays(-1).AddMinutes(-1), T0.AddDays(-1).AddMinutes(1))).Count(x => (string)x["t"]! == "2026-11-09T20:41:00Z"));
		}

		[TestMethod]
		public void ConcurrentRecords_LoseNothing()
		{
			Parallel.For(0, 50000, i => RequestMetrics.Record(i % 3 == 0 ? "static" : "goal", i % 3 == 0 ? "static" : "/a", 200, i % 200, T0));
			var s = RequestMetrics.Snapshot(T0.AddMinutes(-5), T0);
			Assert.AreEqual(50000L, Minutes(s).Single()["count"]);
			Assert.AreEqual(50000L, Kinds(s).Where(k => k.Key != "poll").Sum(k => (long)k.Value!));
		}

		[TestMethod]
		public void NoUserData_InSnapshotKeys()
		{
			RequestMetrics.Record("goal", "/product/%id%", 200, 3, T0);
			var s = RequestMetrics.Snapshot(T0.AddMinutes(-5), T0);
			CollectionAssert.AreEquivalent(new[] { "minutes", "routes", "kinds" }, s.Keys.ToList());
			CollectionAssert.AreEquivalent(new[] { "route", "count", "s5xx", "p50_ms", "p95_ms", "p99_ms" }, Routes(s).Single().Keys.ToList());
		}
	}
}
