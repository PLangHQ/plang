using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building;
using PLang.Building.Model;
using ConditionalBuilder = PLang.Modules.ConditionalModule.Builder;
using PLangTests.Mocks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Tests
{
	[TestClass]
	public class StepPrFilesTests
	{
		private static string Pr(string text, string module, string function) => $$"""
			{
			  "Text": "{{text}}",
			  "ModuleType": "{{module}}",
			  "GenericFunctionType": "PLang.Modules.BaseBuilder+GenericFunction",
			  "Function": { "Name": "{{function}}", "Parameters": [], "ReturnValues": null },
			  "Properties": {}
			}
			""";

		private static (PLangMockFileSystem FileSystem, Goal Goal) Setup()
		{
			var fileSystem = new PLangMockFileSystem();
			var goal = new Goal
			{
				GoalName = "Start",
				AbsolutePrFolderPath = Path.Join(fileSystem.BuildPath, "Start"),
				RelativePrFolderPath = Path.Join(".build", "Start"),
				GoalSteps = new List<GoalStep>()
			};
			return (fileSystem, goal);
		}

		// A step as GoalParser hands it over: text of this build, file name and module of the previous one.
		private static GoalStep Step(Goal goal, string text, string prFileName, string module)
		{
			var step = new GoalStep
			{
				Text = text,
				PrFileName = prFileName,
				AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, prFileName),
				ModuleType = module,
				Goal = goal,
				Index = goal.GoalSteps.Count
			};
			goal.GoalSteps.Add(step);
			return step;
		}

		[TestMethod]
		public void LineAddedAbove_UnchangedStepsKeepTheirBuildUnderTheirNewNumber()
		{
			var (fs, goal) = Setup();
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "01. set_debug_mode_false.pr"), Pr("set debug mode = false", "PLang.Modules.EnvironmentModule", "RemoveDebugMode"));
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "02. if_codecount.pr"), Pr("if %codeCount% >= 2 then call goal A, else call goal B", "PLang.Modules.ConditionalModule", "SimpleCondition"));

			Step(goal, "log 'start'", null!, null!);
			var debug = Step(goal, "set debug mode = false", "01. set_debug_mode_false.pr", "PLang.Modules.EnvironmentModule");
			var condition = Step(goal, "if %codeCount% >= 2 then call goal A, else call goal B", "02. if_codecount.pr", "PLang.Modules.ConditionalModule");

			var moved = StepPrFiles.MoveRenumberedSteps(goal, fs);

			CollectionAssert.AreEqual(new List<int> { 1, 2 }, moved);
			Assert.AreEqual("02. set_debug_mode_false.pr", debug.PrFileName);
			Assert.AreEqual("03. if_codecount.pr", condition.PrFileName);
			Assert.AreEqual(Path.Join(goal.AbsolutePrFolderPath, "03. if_codecount.pr"), condition.AbsolutePrFilePath);
			Assert.AreEqual(Path.Join(goal.RelativePrFolderPath, "03. if_codecount.pr"), condition.RelativePrPath);

			var (instruction, _) = InstructionCreator.Create(debug.AbsolutePrFilePath, fs);
			Assert.AreEqual("RemoveDebugMode", instruction!.Function.Name);
			(instruction, _) = InstructionCreator.Create(condition.AbsolutePrFilePath, fs);
			Assert.AreEqual("SimpleCondition", instruction!.Function.Name);
		}

		[TestMethod]
		public void TwoStepsMovingOntoEachOthersNames_EachKeepsItsOwnBuild()
		{
			var (fs, goal) = Setup();
			// Both steps were built under the same name, one number apart; the line removed above moves both down.
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "02. call_goal_showerror.pr"), Pr("call goal ShowError message=%a%", "PLang.Modules.CallGoalModule", "RunGoal"));
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "03. call_goal_showerror.pr"), Pr("call goal ShowError message=%b%", "PLang.Modules.CallGoalModule", "RunGoalB"));

			var first = Step(goal, "call goal ShowError message=%a%", "02. call_goal_showerror.pr", "PLang.Modules.CallGoalModule");
			var second = Step(goal, "call goal ShowError message=%b%", "03. call_goal_showerror.pr", "PLang.Modules.CallGoalModule");

			StepPrFiles.MoveRenumberedSteps(goal, fs);

			Assert.AreEqual("01. call_goal_showerror.pr", first.PrFileName);
			Assert.AreEqual("02. call_goal_showerror.pr", second.PrFileName);
			Assert.AreEqual("RunGoal", InstructionCreator.Create(first.AbsolutePrFilePath, fs).Instruction!.Function.Name);
			Assert.AreEqual("RunGoalB", InstructionCreator.Create(second.AbsolutePrFilePath, fs).Instruction!.Function.Name);
		}

		[TestMethod]
		public void StepInPlace_OrFileForOtherText_IsNotMoved()
		{
			var (fs, goal) = Setup();
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "01. a.pr"), Pr("a", "PLang.Modules.OutputModule", "Write"));
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "01. b.pr"), Pr("old text", "PLang.Modules.OutputModule", "Write"));

			var inPlace = Step(goal, "a", "01. a.pr", "PLang.Modules.OutputModule");
			var otherText = Step(goal, "b", "01. b.pr", "PLang.Modules.OutputModule");
			var missingFile = Step(goal, "c", "01. c.pr", "PLang.Modules.OutputModule");

			var moved = StepPrFiles.MoveRenumberedSteps(goal, fs);

			Assert.AreEqual(0, moved.Count);
			Assert.AreEqual("01. a.pr", inPlace.PrFileName);
			Assert.AreEqual("01. b.pr", otherText.PrFileName);
			Assert.AreEqual("01. c.pr", missingFile.PrFileName);
		}

		[TestMethod]
		public void MovedStepListedForAnotherModule_TakesTheModuleOfItsPrFile()
		{
			var (fs, goal) = Setup();
			fs.AddFile(Path.Join(goal.AbsolutePrFolderPath, "01. set_debug_mode_false.pr"), Pr("set debug mode = false", "PLang.Modules.EnvironmentModule", "RemoveDebugMode"));
			Step(goal, "log 'start'", null!, null!);
			var debug = Step(goal, "set debug mode = false", "01. set_debug_mode_false.pr", "PLang.Modules.VariableModule");

			StepPrFiles.MoveRenumberedSteps(goal, fs);

			Assert.AreEqual("PLang.Modules.EnvironmentModule", debug.ModuleType);
		}

		[TestMethod]
		public void AlignModule_InstructionWinsOnlyForTheSameStepText()
		{
			var step = new GoalStep { Text = "set debug mode = false", ModuleType = "PLang.Modules.VariableModule" };
			var instruction = new Instruction { Text = "set debug mode = false", ModuleType = "PLang.Modules.EnvironmentModule" };

			Assert.IsTrue(StepPrFiles.AlignModuleWithInstruction(step, instruction));
			Assert.AreEqual("PLang.Modules.EnvironmentModule", step.ModuleType);
			Assert.IsFalse(StepPrFiles.AlignModuleWithInstruction(step, instruction));

			var other = new GoalStep { Text = "set debug mode = true", ModuleType = "PLang.Modules.VariableModule" };
			Assert.IsFalse(StepPrFiles.AlignModuleWithInstruction(other, instruction));
			Assert.AreEqual("PLang.Modules.VariableModule", other.ModuleType);

			// An instruction written without a ModuleType says nothing about the module.
			Assert.IsFalse(StepPrFiles.AlignModuleWithInstruction(other, new Instruction { Text = other.Text, ModuleType = null! }));
		}

		[TestMethod]
		public void BatchedFunction_IsOnlyHandedToAStepBuiltWithItsModule()
		{
			var answers = new GoalDeciderAnswers();
			var step = new GoalStep { Text = "set debug mode = false", Index = 3, ModuleType = "PLang.Modules.EnvironmentModule" };
			answers.SetFunction(step, "PLang.Modules.EnvironmentModule", new GenericFunction("", "RemoveDebugMode"), new PLang.Models.LlmRequest("t", new List<PLang.Models.LlmMessage>()));

			Assert.AreEqual("RemoveDebugMode", answers.Function(step)!.Value.Function.Name);

			step.ModuleType = "PLang.Modules.VariableModule";
			Assert.IsNull(answers.Function(step));
		}
	}

	[TestClass]
	public class ConditionalGoalToCallTests
	{
		private static GenericFunction Function(string name, object? ifTrue = null, object? ifFalse = null)
		{
			var parameters = new List<Parameter> { new("PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition", "condition", new { }) };
			if (ifTrue != null) parameters.Add(new("PLang.Models.GoalToCallInfo", "goalToCallIfTrue", ifTrue));
			if (ifFalse != null) parameters.Add(new("PLang.Models.GoalToCallInfo", "goalToCallIfFalse", ifFalse));
			return new GenericFunction("", name, parameters);
		}

		private const string OneLineIf = "if %codeCount% >= 2 then call goal HandleOverLimit, else call goal IssueCodeNow";

		[TestMethod]
		public void OneLineIfWithTwoCalls_NeedsBothGoals()
		{
			Assert.IsNotNull(ConditionalBuilder.MissingGoalToCall(OneLineIf, Function("SimpleCondition")));
			Assert.IsNotNull(ConditionalBuilder.MissingGoalToCall(OneLineIf, Function("SimpleCondition", ifTrue: new { Name = "HandleOverLimit" })));
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall(OneLineIf, Function("SimpleCondition", new { Name = "HandleOverLimit" }, new { Name = "IssueCodeNow" })));
		}

		[TestMethod]
		public void IfWithOneCall_NeedsAGoal()
		{
			Assert.IsNotNull(ConditionalBuilder.MissingGoalToCall("if %x% is empty then call Create", Function("IsEmpty")));
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %x% is empty then call Create", Function("IsEmpty", new { Name = "Create" })));
			// `if %x% is not empty then call A` may be built as IsEmpty with the goal on the false side.
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %x% is not empty then call A", Function("IsEmpty", ifFalse: new { Name = "A" })));
		}

		[TestMethod]
		public void GoalToCall_MustBeAGoalTheStepNames()
		{
			const string blockIf = "if %!Event.SourceGoal.IsSystem% is false and %!callstack.Frames[idx].DurationMs% > 300 then";
			Assert.IsNotNull(ConditionalBuilder.UnnamedGoalToCall(blockIf, Function("CompoundCondition", new { Name = "then" })));
			Assert.IsNotNull(ConditionalBuilder.UnnamedGoalToCall("if %x% > 2 then", Function("SimpleCondition", new { Name = "Process" })));
			Assert.IsNull(ConditionalBuilder.UnnamedGoalToCall(blockIf, Function("CompoundCondition")));
			Assert.IsNull(ConditionalBuilder.UnnamedGoalToCall(OneLineIf, Function("SimpleCondition", new { Name = "HandleOverLimit" }, new { Name = "IssueCodeNow" })));
			Assert.IsNull(ConditionalBuilder.UnnamedGoalToCall("if %x% then call goal /api/Process", Function("IsTrue", new { Name = "/api/Process" })));
		}

		private static GenericFunction WithCondition(string json) => new("", "SimpleCondition",
			new List<Parameter> { new("PLang.Modules.ConditionalModule.ConditionEvaluator+SimpleCondition", "condition", Newtonsoft.Json.Linq.JObject.Parse(json)) });

		[TestMethod]
		public void IsNot_OnlyWhenTheStepSaysNot()
		{
			var doubleNegation = WithCondition("""{"Kind":"Compound","Logic":"AND","Conditions":[{"Kind":"Simple","LeftValue":"%a%","Operator":"==","RightValue":false,"IsNot":true},{"Kind":"Simple","LeftValue":"%b%","Operator":">","RightValue":300,"IsNot":false}]}""");
			Assert.IsNotNull(ConditionalBuilder.StrayNegation("if %a% is false and %b% > 300 then", doubleNegation));
			Assert.IsNull(ConditionalBuilder.StrayNegation("if %a% is not false and %b% > 300 then", doubleNegation));
			var plain = WithCondition("""{"Kind":"Simple","LeftValue":"%a%","Operator":"==","RightValue":false,"IsNot":false}""");
			Assert.IsNull(ConditionalBuilder.StrayNegation("if %a% is false then", plain));
		}

		[TestMethod]
		public void NoCallInTheCondition_NeedsNoGoal()
		{
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %x% > 2 then", Function("SimpleCondition")));
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %text% contains \"call me\" then", Function("ContainsString")));
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %x% > 2 then, on error call HandleError", Function("SimpleCondition")));
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %x% > 2 then call A", Function("RunInlineCode")));
			Assert.IsNull(ConditionalBuilder.MissingGoalToCall("if %x% > 2 then call A", Function("SimpleCondition", Newtonsoft.Json.Linq.JObject.Parse("{\"Name\":\"A\"}"))));
			Assert.IsNotNull(ConditionalBuilder.MissingGoalToCall("if %x% > 2 then call A", Function("SimpleCondition", Newtonsoft.Json.Linq.JValue.CreateNull())));
		}
	}
}
