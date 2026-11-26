using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using PLang.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class ExecutedStepTests
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static ExecutedStep CompleteWithLargeValue()
		{
			var step = new ExecutedStep(new GoalStep(), 0);
			step.Complete(new string('a', 100_000), null);
			return step;
		}

		[TestMethod]
		public void LargeReturnValue_IsNotKeptAlive()
		{
			var step = CompleteWithLargeValue();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			Assert.IsNull(step.ReturnValue);
		}

		[TestMethod]
		public void SmallReturnValue_IsKept()
		{
			var step = new ExecutedStep(new GoalStep(), 0);
			step.Complete(42L, null);
			Assert.AreEqual(42L, step.ReturnValue);

			var text = new ExecutedStep(new GoalStep(), 1);
			text.Complete("short", null);
			GC.Collect();
			Assert.AreEqual("short", text.ReturnValue);
		}

		[TestMethod]
		public void LargeReturnValue_IsReadWhileAlive()
		{
			var value = new string('b', 100_000);
			var step = new ExecutedStep(new GoalStep(), 0);
			step.Complete(value, null);
			GC.Collect();
			Assert.AreSame(value, step.ReturnValue);
			GC.KeepAlive(value);
		}
	}
}
