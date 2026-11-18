using Microsoft.VisualStudio.TestTools.UnitTesting;
using static PLang.Modules.ConditionalModule.ConditionEvaluator;

namespace PLangTests.Modules.ConditionalModule
{
	[TestClass]
	public class ConditionEvaluatorTests
	{
		private static Condition Simple(object? left, string op, object? right) =>
			new Condition { Kind = ConditionKind.Simple, LeftValue = left, Operator = op, RightValue = right };

		[DataTestMethod]
		[DataRow("==")]
		[DataRow("=")]
		[DataRow("===")]
		[DataRow("equals")]
		[DataRow("EQUALS")]
		public void EqualityOperators_MatchEqualValues(string op)
		{
			Assert.IsTrue(ConditionEngine.Evaluate(Simple(0L, op, 0L)));
			Assert.IsFalse(ConditionEngine.Evaluate(Simple(1L, op, 0L)));
			Assert.IsTrue(ConditionEngine.Evaluate(Simple("abc", op, "abc")));
		}

		[DataTestMethod]
		[DataRow("!=")]
		[DataRow("<>")]
		[DataRow("!==")]
		public void NotEqualOperators_MatchDifferentValues(string op)
		{
			Assert.IsTrue(ConditionEngine.Evaluate(Simple(1L, op, 0L)));
			Assert.IsFalse(ConditionEngine.Evaluate(Simple(0L, op, 0L)));
		}

		[TestMethod]
		public void Compound_WithSingleEqualsOperator_Evaluates()
		{
			// `if %bookRowCount% is 0 and %hasTarget% is 1`
			Condition Build(object rowCount, object hasTarget) => new Condition
			{
				Kind = ConditionKind.Compound,
				Logic = "AND",
				Conditions = new[]
				{
					Simple(rowCount, "=", 0L),
					Simple(hasTarget, "=", 1L),
				}
			};

			Assert.IsTrue(ConditionEngine.Evaluate(Build(0L, 1L)));
			Assert.IsFalse(ConditionEngine.Evaluate(Build(2L, 1L)));
			Assert.IsFalse(ConditionEngine.Evaluate(Build(0L, 0L)));
		}

		[TestMethod]
		public void IsNot_InvertsSingleEqualsOperator()
		{
			var c = Simple(0L, "=", 0L) with { IsNot = true };
			Assert.IsFalse(ConditionEngine.Evaluate(c));
		}

		[TestMethod]
		public void ExistingOperators_KeepTheirMeaning()
		{
			Assert.IsTrue(ConditionEngine.Evaluate(Simple(5L, ">", 3L)));
			Assert.IsTrue(ConditionEngine.Evaluate(Simple(3L, "<=", 3L)));
			Assert.IsTrue(ConditionEngine.Evaluate(Simple("hello", "contains", "ell")));
			Assert.IsTrue(ConditionEngine.Evaluate(Simple("", "isEmpty", null)));
		}

		[TestMethod]
		public void UnknownOperator_StillThrows()
		{
			Assert.ThrowsException<System.NotSupportedException>(() => ConditionEngine.Evaluate(Simple(1L, "~", 1L)));
		}
	}
}
