using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using ConditionalBuilder = PLang.Modules.ConditionalModule.Builder;
using static PLang.Modules.BaseBuilder;

namespace PLangTests.Building
{
	[TestClass]
	public class MissingConditionTests
	{
		private static GenericFunction Function(string name, params (string Name, object? Value)[] parameters)
		{
			return new GenericFunction("", name, parameters.Select(p => new Parameter("System.Object", p.Name, p.Value)).ToList());
		}

		private static readonly (string, object?) Goal = ("goalToCallIfTrue", new { Name = "AskName" });

		[TestMethod]
		public void IfStepWithOnlyTheGoal_IsRejected()
		{
			var message = ConditionalBuilder.MissingCondition("if %name% is empty then call goal AskName", Function("IsEmpty", Goal));

			Assert.IsNotNull(message);
			StringAssert.Contains(message, "IsEmpty");
		}

		[TestMethod]
		public void IfStepWithAnEmptyOperand_IsRejected()
		{
			Assert.IsNotNull(ConditionalBuilder.MissingCondition("if %name% is empty then call goal AskName", Function("IsEmpty", ("item", null), Goal)));
		}

		[TestMethod]
		public void IfStepWithItsOperand_IsAccepted()
		{
			Assert.IsNull(ConditionalBuilder.MissingCondition("if %name% is empty then call goal AskName", Function("IsEmpty", ("item", "%name%"), Goal)));
		}

		[TestMethod]
		public void IfStepWithTheOperandAndAThrow_IsAccepted()
		{
			Assert.IsNull(ConditionalBuilder.MissingCondition("if %name% is empty then end goal", Function("IsEmpty", ("item", "%name%"), ("throwErrorOnTrue", new { Message = "End goal" }))));
		}

		[TestMethod]
		public void StepThatIsNotAnIf_IsLeftAlone()
		{
			Assert.IsNull(ConditionalBuilder.MissingCondition("set %x% = 1", Function("IsEmpty", Goal)));
			Assert.IsNull(ConditionalBuilder.MissingCondition("call goal X, if %x% is empty", Function("IsEmpty", Goal)));
		}

		[TestMethod]
		public void MethodThatDoesNotTakeGoals_IsLeftAlone()
		{
			Assert.IsNull(ConditionalBuilder.MissingCondition("if %x% > 2 then call A", Function("RunInlineCode", Goal)));
		}
	}
}
