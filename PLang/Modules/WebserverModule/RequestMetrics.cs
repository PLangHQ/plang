using System.Collections.Concurrent;

namespace PLang.Modules.WebserverModule
{
	public static class RequestMetrics
	{
		public const int Minutes = 1440;
		public const int Slots = 50;
		public const int MaxRoutes = 200;
		private const double Ratio = 1.25;
		private static readonly double LogRatio = Math.Log(Ratio);
		private static readonly double[] Bounds = BuildBounds();

		public const string KindGoal = "goal";
		public const string KindPlang = "plang";
		public const string KindStatic = "static";
		public const string KindNotFound = "not_found";
		public const string KindPoll = "poll";

		private sealed class Hist
		{
			public readonly int[] Slot = new int[Slots + 1];
			public long Count;
			public long S5xx;
		}

		private sealed class Bucket
		{
			public long Minute = -1;
			public long Count, S2xx, S3xx, S4xx, S5xx, MaxMs;
			public long KGoal, KPlang, KStatic, KNotFound, KPoll;
			public readonly int[] Slot = new int[Slots + 1];

			public void Clear()
			{
				Count = S2xx = S3xx = S4xx = S5xx = MaxMs = 0;
				KGoal = KPlang = KStatic = KNotFound = KPoll = 0;
				Array.Clear(Slot);
			}
		}

		private sealed class Hour
		{
			public readonly long Id;
			public readonly ConcurrentDictionary<string, Hist> Routes = new();
			public Hour(long id) { Id = id; }
		}

		private static readonly Bucket[] ring = CreateRing();
		private static readonly Hour?[] hours = new Hour?[24];

		private static Bucket[] CreateRing()
		{
			var r = new Bucket[Minutes];
			for (int i = 0; i < r.Length; i++) r[i] = new Bucket();
			return r;
		}

		private static double[] BuildBounds()
		{
			var b = new double[Slots];
			for (int i = 0; i < Slots; i++) b[i] = Math.Pow(Ratio, i);
			return b;
		}

		public static int SlotFor(double ms)
		{
			if (ms <= 1) return 0;
			int i = (int)Math.Ceiling(Math.Log(ms) / LogRatio);
			if (i < 0) i = 0;
			return i >= Slots ? Slots : i;
		}

		public static void Record(string kind, string? route, int status, double ms, DateTime utcNow)
		{
			long minute = utcNow.Ticks / TimeSpan.TicksPerMinute;
			var b = ring[(int)(minute % Minutes)];
			if (Volatile.Read(ref b.Minute) != minute)
			{
				lock (b)
				{
					if (b.Minute != minute)
					{
						b.Clear();
						Volatile.Write(ref b.Minute, minute);
					}
				}
			}

			if (kind == KindPoll)
			{
				Interlocked.Increment(ref b.KPoll);
				return;
			}

			Interlocked.Increment(ref b.Count);
			if (status >= 500) Interlocked.Increment(ref b.S5xx);
			else if (status >= 400) Interlocked.Increment(ref b.S4xx);
			else if (status >= 300) Interlocked.Increment(ref b.S3xx);
			else Interlocked.Increment(ref b.S2xx);

			switch (kind)
			{
				case KindPlang: Interlocked.Increment(ref b.KPlang); break;
				case KindStatic: Interlocked.Increment(ref b.KStatic); break;
				case KindNotFound: Interlocked.Increment(ref b.KNotFound); break;
				default: Interlocked.Increment(ref b.KGoal); break;
			}

			long whole = (long)Math.Ceiling(ms);
			long seen;
			while (whole > (seen = Volatile.Read(ref b.MaxMs)))
			{
				if (Interlocked.CompareExchange(ref b.MaxMs, whole, seen) == seen) break;
			}
			int slot = SlotFor(ms);
			Interlocked.Increment(ref b.Slot[slot]);

			var h = HourFor(minute / 60);
			string key = route ?? "other";
			if (!h.Routes.TryGetValue(key, out var hist))
			{
				if (h.Routes.Count >= MaxRoutes) key = "other";
				hist = h.Routes.GetOrAdd(key, _ => new Hist());
			}
			Interlocked.Increment(ref hist.Slot[slot]);
			Interlocked.Increment(ref hist.Count);
			if (status >= 500) Interlocked.Increment(ref hist.S5xx);
		}

		private static Hour HourFor(long hourId)
		{
			int i = (int)(hourId % 24);
			var h = Volatile.Read(ref hours[i]);
			if (h != null && h.Id == hourId) return h;
			var fresh = new Hour(hourId);
			while (true)
			{
				h = Volatile.Read(ref hours[i]);
				if (h != null && h.Id == hourId) return h;
				if (Interlocked.CompareExchange(ref hours[i], fresh, h) == h) return fresh;
			}
		}

		public static void Reset()
		{
			foreach (var b in ring) lock (b) { b.Clear(); b.Minute = -1; }
			Array.Clear(hours);
		}

		private static long Percentile(int[] slots, long total, double p)
		{
			if (total <= 0) return 0;
			double rank = p * total;
			long run = 0;
			for (int i = 0; i <= Slots; i++)
			{
				long n = slots[i];
				if (n == 0) continue;
				if (run + n >= rank)
				{
					double lo = i == 0 ? 0 : Bounds[i - 1];
					double hi = i < Slots ? Bounds[i] : Bounds[Slots - 1] * Ratio;
					double frac = (rank - run) / n;
					return (long)Math.Round(lo + (hi - lo) * frac);
				}
				run += n;
			}
			return (long)Math.Round(Bounds[Slots - 1] * Ratio);
		}

		public static Dictionary<string, object?> Snapshot(DateTime utcSince, DateTime utcNow)
		{
			long toMinute = utcNow.Ticks / TimeSpan.TicksPerMinute;
			long fromMinute = Math.Max(utcSince.Ticks / TimeSpan.TicksPerMinute, toMinute - (Minutes - 1));

			var minutes = new List<Dictionary<string, object?>>();
			long kGoal = 0, kPlang = 0, kStatic = 0, kNotFound = 0, kPoll = 0;
			for (long m = fromMinute; m <= toMinute; m++)
			{
				var b = ring[(int)(m % Minutes)];
				if (Volatile.Read(ref b.Minute) != m) continue;
				long count = Volatile.Read(ref b.Count);
				kGoal += b.KGoal; kPlang += b.KPlang; kStatic += b.KStatic; kNotFound += b.KNotFound; kPoll += b.KPoll;
				if (count == 0) continue;
				var copy = new int[Slots + 1];
				for (int i = 0; i <= Slots; i++) copy[i] = Volatile.Read(ref b.Slot[i]);
				minutes.Add(new Dictionary<string, object?>
				{
					["t"] = new DateTime(m * TimeSpan.TicksPerMinute, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ"),
					["count"] = count,
					["s2xx"] = b.S2xx,
					["s3xx"] = b.S3xx,
					["s4xx"] = b.S4xx,
					["s5xx"] = b.S5xx,
					["p50_ms"] = Percentile(copy, count, 0.50),
					["p95_ms"] = Percentile(copy, count, 0.95),
					["p99_ms"] = Percentile(copy, count, 0.99),
					["max_ms"] = b.MaxMs
				});
			}

			var merged = new Dictionary<string, (int[] Slots, long Count, long S5xx)>();
			for (long hr = fromMinute / 60; hr <= toMinute / 60; hr++)
			{
				var h = Volatile.Read(ref hours[(int)(hr % 24)]);
				if (h == null || h.Id != hr) continue;
				foreach (var kv in h.Routes)
				{
					if (!merged.TryGetValue(kv.Key, out var acc)) acc = (new int[Slots + 1], 0, 0);
					for (int i = 0; i <= Slots; i++) acc.Slots[i] += Volatile.Read(ref kv.Value.Slot[i]);
					merged[kv.Key] = (acc.Slots, acc.Count + Volatile.Read(ref kv.Value.Count), acc.S5xx + Volatile.Read(ref kv.Value.S5xx));
				}
			}

			var routes = merged
				.OrderByDescending(r => r.Value.Count)
				.Take(20)
				.Select(r => new Dictionary<string, object?>
				{
					["route"] = r.Key,
					["count"] = r.Value.Count,
					["s5xx"] = r.Value.S5xx,
					["p50_ms"] = Percentile(r.Value.Slots, r.Value.Count, 0.50),
					["p95_ms"] = Percentile(r.Value.Slots, r.Value.Count, 0.95),
					["p99_ms"] = Percentile(r.Value.Slots, r.Value.Count, 0.99)
				}).ToList();

			return new Dictionary<string, object?>
			{
				["minutes"] = minutes,
				["routes"] = routes,
				["kinds"] = new Dictionary<string, object?>
				{
					["goal"] = kGoal,
					["plang"] = kPlang,
					["static"] = kStatic,
					["not_found"] = kNotFound,
					["poll"] = kPoll
				}
			};
		}
	}
}
