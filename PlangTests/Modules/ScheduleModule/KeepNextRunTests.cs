using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Models;
using static PLang.Modules.ScheduleModule.Program;

namespace PLangTests.Modules.ScheduleModule
{
	[TestClass]
	public class KeepNextRunTests
	{
		private static readonly DateTime Stored = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

		private static CronJob Interval(long seconds, string goal = "Job", DateTime? nextRun = null, string pr = "a.pr")
			=> new(pr, null, new GoalToCallInfo(goal), nextRun, seconds);

		private static CronJob Cron(string expression, string goal = "Job", DateTime? nextRun = null, string pr = "a.pr")
			=> new(pr, expression, new GoalToCallInfo(goal), nextRun);

		[TestMethod]
		public void KeepNextRun_SameIntervalAndGoal_KeepsTheStoredNextRun()
		{
			var result = KeepNextRun([Interval(3600, nextRun: Stored)], Interval(3600));

			Assert.AreEqual(Stored, result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_SameCronAndGoal_KeepsTheStoredNextRun()
		{
			var result = KeepNextRun([Cron("0 8 * * *", nextRun: Stored)], Cron("0 8 * * *"));

			Assert.AreEqual(Stored, result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_ChangedInterval_StartsOver()
		{
			var result = KeepNextRun([Interval(3600, nextRun: Stored)], Interval(1800));

			Assert.IsNull(result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_ChangedCronExpression_StartsOver()
		{
			var result = KeepNextRun([Cron("0 8 * * *", nextRun: Stored)], Cron("0 9 * * *"));

			Assert.IsNull(result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_ChangedGoal_StartsOver()
		{
			var result = KeepNextRun([Interval(3600, "Old", Stored)], Interval(3600, "New"));

			Assert.IsNull(result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_IntervalChangedToCron_StartsOver()
		{
			var result = KeepNextRun([Interval(3600, nextRun: Stored)], Cron("0 * * * *"));

			Assert.IsNull(result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_NextRunGivenByTheStep_Wins()
		{
			var given = Stored.AddHours(5);

			var result = KeepNextRun([Interval(3600, nextRun: Stored)], Interval(3600, nextRun: given));

			Assert.AreEqual(given, result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_OtherStep_IsNotTouched()
		{
			var result = KeepNextRun([Interval(3600, nextRun: Stored, pr: "other.pr")], Interval(3600, pr: "a.pr"));

			Assert.IsNull(result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_NothingStored_IsTheNewJob()
		{
			var result = KeepNextRun([], Interval(60));

			Assert.IsNull(result.NextRun);
		}

		[TestMethod]
		public void KeepNextRun_StoredJobThatPassedWhileDown_IsDueOnceAtStart()
		{
			// The app was down for three hours: the stored NextRun is in the past, so the job is due at the first tick and runs once,
			// and the periods that passed are dropped, not run one after another.
			var now = Stored.AddHours(3);
			var kept = KeepNextRun([Interval(3600, nextRun: Stored)], Interval(3600));

			var plan = PlanDueJobs([kept], now, _ => false);

			Assert.AreEqual(1, plan.Count);
			Assert.IsFalse(plan[0].Skip);
			Assert.AreEqual(Stored.AddHours(4), plan[0].NextRun);
		}
	}
}
