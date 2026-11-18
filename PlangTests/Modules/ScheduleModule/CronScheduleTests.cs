using Microsoft.VisualStudio.TestTools.UnitTesting;
using NCrontab;
using static PLang.Modules.ScheduleModule.Program;

namespace PLangTests.Modules.ScheduleModule
{
	[TestClass]
	public class CronScheduleTests
	{
		private static readonly DateTime Start = new DateTime(2026, 11, 18, 10, 0, 0);

		[TestMethod]
		public void GetNextRun_FiveFields_MinutePrecision()
		{
			Assert.AreEqual(new DateTime(2026, 11, 18, 10, 1, 0), GetNextRun("* * * * *", Start.AddSeconds(30)));
			Assert.AreEqual(new DateTime(2026, 11, 18, 10, 1, 0), GetNextRun("* * * * *", Start));
			Assert.AreEqual(new DateTime(2026, 11, 18, 10, 5, 0), GetNextRun("*/5 * * * *", Start.AddMinutes(2).AddSeconds(10)));
			Assert.AreEqual(new DateTime(2026, 11, 18, 11, 0, 0), GetNextRun("0 * * * *", Start));
			Assert.AreEqual(new DateTime(2026, 11, 19, 8, 0, 0), GetNextRun("0 8 * * *", Start));
		}

		[TestMethod]
		public void GetNextRun_SixFields_SecondsFirst()
		{
			Assert.AreEqual(Start.AddSeconds(5), GetNextRun("*/5 * * * * *", Start.AddSeconds(2)));
			Assert.AreEqual(Start.AddSeconds(30), GetNextRun("*/15 * * * * *", Start.AddSeconds(15)));
			Assert.AreEqual(new DateTime(2026, 11, 18, 10, 1, 0), GetNextRun("*/15 * * * * *", Start.AddSeconds(45)));
			// 6 fields with 0 seconds is the same as the 5-field expression
			Assert.AreEqual(GetNextRun("*/5 * * * *", Start.AddSeconds(7)), GetNextRun("0 */5 * * * *", Start.AddSeconds(7)));
		}

		[TestMethod]
		public void ParseCron_Invalid_Throws()
		{
			Assert.ThrowsException<CrontabException>(() => ParseCron("every 5 seconds"));
			Assert.ThrowsException<CrontabException>(() => ParseCron("* * * * * * *"));
			Assert.ThrowsException<CrontabException>(() => ParseCron("0 0/5 * * * ?"));
		}

		[TestMethod]
		public void GetDueJobs_OnlyReachedNextRun()
		{
			var jobs = new List<CronJob>
			{
				new CronJob("a.pr", "* * * * *", "A", Start.AddSeconds(-1)),
				new CronJob("b.pr", "* * * * *", "B", Start),
				new CronJob("c.pr", "* * * * *", "C", Start.AddSeconds(1)),
				new CronJob("d.pr", "* * * * *", "D", null),
			};

			var due = GetDueJobs(jobs, Start);

			CollectionAssert.AreEqual(new[] { "a.pr", "b.pr" }, due.Select(p => p.AbsolutePrFilePath).ToArray());
		}

		// Runs the scheduler's decision on a 1 s tick for the given span: a job runs when due and its next run counts from the tick.
		// tickOffset puts every tick that much after the whole second, as the loop wakes just after it.
		private static List<DateTime> SimulateTicks(string cron, TimeSpan span, TimeSpan tickOffset)
		{
			var runs = new List<DateTime>();
			var jobs = new List<CronJob> { new CronJob("job.pr", cron, "Job", GetNextRun(cron, Start)) };
			for (var now = Start + tickOffset; now <= Start + span; now = now.AddSeconds(1))
			{
				foreach (var job in GetDueJobs(jobs, now))
				{
					runs.Add(now);
					jobs[jobs.IndexOf(job)] = job with { NextRun = GetNextRun(job.CronCommand, now) };
				}
			}
			return runs;
		}

		[TestMethod]
		public void OneSecondTick_EveryMinute_RunsOncePerMinute()
		{
			var runs = SimulateTicks("* * * * *", TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)), TimeSpan.FromMilliseconds(10));

			Assert.AreEqual(5, runs.Count);
			for (int i = 0; i < runs.Count; i++)
			{
				Assert.AreEqual(Start.AddMinutes(i + 1).AddMilliseconds(10), runs[i]);
			}
		}

		[TestMethod]
		public void OneSecondTick_EveryMinute_TickOnTheBoundary_NoDoubleRun()
		{
			var runs = SimulateTicks("* * * * *", TimeSpan.FromMinutes(3), TimeSpan.Zero);

			CollectionAssert.AreEqual(new[] { Start.AddMinutes(1), Start.AddMinutes(2), Start.AddMinutes(3) }, runs);
		}

		[TestMethod]
		public void OneSecondTick_Every5Seconds_RunsEvery5Seconds()
		{
			var runs = SimulateTicks("*/5 * * * * *", TimeSpan.FromSeconds(31), TimeSpan.FromMilliseconds(10));

			Assert.AreEqual(6, runs.Count);
			for (int i = 1; i < runs.Count; i++)
			{
				Assert.AreEqual(TimeSpan.FromSeconds(5), runs[i] - runs[i - 1]);
			}
		}

		[TestMethod]
		public void OneSecondTick_Every15Seconds()
		{
			var runs = SimulateTicks("*/15 * * * * *", TimeSpan.FromMinutes(1), TimeSpan.Zero);

			CollectionAssert.AreEqual(new[] { Start.AddSeconds(15), Start.AddSeconds(30), Start.AddSeconds(45), Start.AddSeconds(60) }, runs);
		}
	}
}
