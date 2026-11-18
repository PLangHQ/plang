using Microsoft.VisualStudio.TestTools.UnitTesting;
using NCrontab;
using Newtonsoft.Json;
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

		[TestMethod]
		public void GetNextIntervalRun_StaysOnGrid_NoDrift()
		{
			var interval = TimeSpan.FromSeconds(45);
			// the tick that runs the job comes up to a second after the scheduled time; the next run keeps to the scheduled time
			Assert.AreEqual(Start.AddSeconds(45), GetNextIntervalRun(Start, interval, Start.AddMilliseconds(990)));
			Assert.AreEqual(Start.AddSeconds(90), GetNextIntervalRun(Start.AddSeconds(45), interval, Start.AddSeconds(45).AddMilliseconds(10)));
		}

		[TestMethod]
		public void GetNextIntervalRun_MissedPeriods_RunOnceThenNextInFuture()
		{
			var interval = TimeSpan.FromSeconds(45);
			// down for 5 minutes after the run due at Start: the next run is the first grid point after now, not one per missed period
			var now = Start.AddMinutes(5).AddSeconds(3);
			var next = GetNextIntervalRun(Start, interval, now);

			Assert.AreEqual(Start.AddSeconds(45 * 7), next);
			Assert.IsTrue(next > now && next - now <= interval);
			// exactly on a grid point: that point counts as passed
			Assert.AreEqual(Start.AddSeconds(135), GetNextIntervalRun(Start, interval, Start.AddSeconds(90)));
		}

		[TestMethod]
		public void GetNextRun_IntervalJob_FirstRunOneIntervalAfterNow()
		{
			var job = new CronJob("job.pr", null, "Job", null, 45);

			Assert.IsTrue(job.IsInterval);
			Assert.AreEqual(Start.AddSeconds(45), GetNextRun(job, Start));
			Assert.AreEqual(Start.AddSeconds(90), GetNextRun(job with { NextRun = Start.AddSeconds(45) }, Start.AddSeconds(45).AddMilliseconds(10)));
		}

		[TestMethod]
		public void GetNextRun_CronJob_Unchanged()
		{
			var job = new CronJob("job.pr", "* * * * *", "Job", Start);

			Assert.IsFalse(job.IsInterval);
			Assert.AreEqual(GetNextRun("* * * * *", Start.AddMilliseconds(10)), GetNextRun(job, Start.AddMilliseconds(10)));
			Assert.AreEqual(GetNextRun("0 8 * * *", Start), GetNextRun(job with { CronCommand = "0 8 * * *" }, Start));
		}

		// Runs the scheduler's decision on a 1 s tick for the given span, with the run time of the job.
		// Returns the start time of each run and the ticks at which a due occurrence was skipped because the job was still running.
		private static (List<DateTime> Runs, List<DateTime> Skips) SimulateJob(CronJob job, TimeSpan span, TimeSpan runTime, TimeSpan tickOffset)
		{
			var runs = new List<DateTime>();
			var skips = new List<DateTime>();
			var jobs = new List<CronJob> { job with { NextRun = GetNextRun(job, Start) } };
			DateTime? runningUntil = null;
			for (var now = Start + tickOffset; now <= Start + span; now = now.AddSeconds(1))
			{
				foreach (var due in PlanDueJobs(jobs, now, _ => runningUntil > now))
				{
					if (due.Skip) skips.Add(now);
					else
					{
						runs.Add(now);
						runningUntil = now + runTime;
					}
					jobs[jobs.IndexOf(due.Job)] = due.Job with { NextRun = due.NextRun };
				}
			}
			return (runs, skips);
		}

		[TestMethod]
		public void OneSecondTick_Every45SecondsInterval_Runs45SecondsApart()
		{
			var (runs, skips) = SimulateJob(new CronJob("job.pr", null, "Job", null, 45), TimeSpan.FromMinutes(5), TimeSpan.Zero, TimeSpan.FromMilliseconds(10));

			Assert.AreEqual(0, skips.Count);
			Assert.AreEqual(6, runs.Count);
			for (int i = 0; i < runs.Count; i++)
			{
				Assert.AreEqual(Start.AddSeconds(45 * (i + 1)).AddMilliseconds(10), runs[i]);
			}
		}

		[TestMethod]
		public void OneSecondTick_StillRunning_SkipsOccurrence()
		{
			// every 5 seconds, each run takes 10 seconds: a run starts, the next occurrence is skipped, the one after runs
			var (runs, skips) = SimulateJob(new CronJob("job.pr", null, "Job", null, 5), TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(10));

			var at = (int seconds) => Start.AddSeconds(seconds).AddMilliseconds(10);
			CollectionAssert.AreEqual(new[] { at(5), at(15), at(25) }, runs);
			CollectionAssert.AreEqual(new[] { at(10), at(20), at(30) }, skips);
		}

		[TestMethod]
		public void PlanDueJobs_SkipOnlyTheRunningJob()
		{
			var jobs = new List<CronJob>
			{
				new CronJob("slow.pr", null, "Slow", Start, 5),
				new CronJob("fast.pr", null, "Fast", Start, 3),
				new CronJob("cron.pr", "* * * * *", "Minute", Start),
			};

			var due = PlanDueJobs(jobs, Start, job => job.AbsolutePrFilePath == "slow.pr");

			CollectionAssert.AreEqual(new[] { true, false, false }, due.Select(p => p.Skip).ToArray());
			CollectionAssert.AreEqual(new[] { Start.AddSeconds(5), Start.AddSeconds(3), Start.AddMinutes(1) }, due.Select(p => p.NextRun).ToArray());
		}

		[TestMethod]
		public void StoredJob_WithoutInterval_LoadsAsCronJob()
		{
			// a job stored before jobs had an interval, with the field that is no longer part of the record
			var stored = "[{\"AbsolutePrFilePath\":\"c:\\\\file.pr\",\"CronCommand\":\"*/5 * * * *\",\"GoalName\":{\"Name\":\"Job\",\"Parameters\":{}},\"NextRun\":\"2026-11-18T10:05:00\",\"MaxExecutionTimeInMilliseconds\":30000,\"IsArchived\":false}]";

			var job = JsonConvert.DeserializeObject<List<CronJob>>(stored)!.Single();

			Assert.AreEqual("*/5 * * * *", job.CronCommand);
			Assert.IsNull(job.IntervalInSeconds);
			Assert.IsFalse(job.IsInterval);
			Assert.AreEqual(new DateTime(2026, 11, 18, 10, 5, 0), job.NextRun);
			Assert.AreEqual(new DateTime(2026, 11, 18, 10, 10, 0), GetNextRun(job, job.NextRun!.Value));
		}

		[TestMethod]
		public void StoredJob_Interval_RoundTrips()
		{
			var job = new CronJob("c:\\file.pr", null, "Job", Start, 45);

			var json = JsonConvert.SerializeObject(new List<CronJob> { job });
			var loaded = JsonConvert.DeserializeObject<List<CronJob>>(json)!.Single();

			Assert.IsFalse(json.Contains("IsInterval"));
			Assert.AreEqual(45, loaded.IntervalInSeconds);
			Assert.IsNull(loaded.CronCommand);
			Assert.IsTrue(loaded.IsInterval);
			Assert.AreEqual(Start, loaded.NextRun);
		}
	}
}
