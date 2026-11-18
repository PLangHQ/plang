using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLangTests.Mocks;
using System.IO;

namespace PLang.Building.Parsers.Tests
{
	// The instruction cache is keyed by the step's .pr path. A deploy can write another build of a step to the same path,
	// e.g. "set debug mode = false" built first for one module and then for EnvironmentModule.RemoveDebugMode.
	[TestClass]
	public class InstructionCacheTests
	{
		private static string Pr(string module, string function) => $$"""
			{
			  "Text": "set debug mode = false",
			  "ModuleType": "{{module}}",
			  "GenericFunctionType": "PLang.Modules.BaseBuilder+GenericFunction",
			  "Function": { "Name": "{{function}}", "Parameters": [], "ReturnValues": null },
			  "Properties": {}
			}
			""";

		private static (PLangMockFileSystem FileSystem, PrParser Parser, string Path) Setup()
		{
			var fileSystem = new PLangMockFileSystem();
			var parser = new PrParser(fileSystem, Substitute.For<ILogger>());
			var path = Path.Join(fileSystem.BuildPath, "Start", "02. set_debug_mode.pr");
			return (fileSystem, parser, path);
		}

		private static GoalStep Step(string path, string module) => new GoalStep { Text = "set debug mode = false", ModuleType = module, AbsolutePrFilePath = path };

		[TestMethod]
		public void GoalListsAnotherModuleThanTheStepFile_StepRunsOnTheInstructionsModule()
		{
			var (fileSystem, parser, path) = Setup();
			fileSystem.AddFile(path, Pr("PLang.Modules.EnvironmentModule", "RemoveDebugMode"));

			// 00. Goal.pr lists the step as VariableModule, the step file is EnvironmentModule.RemoveDebugMode.
			var step = Step(path, "PLang.Modules.VariableModule");

			var instruction = parser.ParseInstructionFile(step);
			Assert.AreEqual("RemoveDebugMode", instruction!.Function.Name);
			Assert.AreEqual("PLang.Modules.EnvironmentModule", step.ModuleType);

			// The next read comes from the cache, now that the step and the instruction agree.
			Assert.AreSame(instruction, parser.ParseInstructionFile(step));
		}

		[TestMethod]
		public void CachedInstructionOfAnotherBuild_IsReadAgainFromDisk()
		{
			var (fileSystem, parser, path) = Setup();
			fileSystem.AddFile(path, Pr("PLang.Modules.VariableModule", "SetVariable"));
			var oldStep = Step(path, "PLang.Modules.VariableModule");
			Assert.AreEqual("SetVariable", parser.ParseInstructionFile(oldStep)!.Function.Name);

			// The deploy rewrites the file; the cache still holds the earlier build for this path.
			fileSystem.AddFile(path, Pr("PLang.Modules.EnvironmentModule", "RemoveDebugMode"));
			var newStep = Step(path, "PLang.Modules.EnvironmentModule");

			var instruction = parser.ParseInstructionFile(newStep);
			Assert.AreEqual("RemoveDebugMode", instruction!.Function.Name);
			Assert.AreEqual("PLang.Modules.EnvironmentModule", instruction.ModuleType);
		}

		[TestMethod]
		public void MatchingAndOlderInstructions_AreCached()
		{
			var (fileSystem, parser, path) = Setup();
			fileSystem.AddFile(path, Pr("PLang.Modules.EnvironmentModule", "RemoveDebugMode"));
			var step = Step(path, "PLang.Modules.EnvironmentModule");
			var first = parser.ParseInstructionFile(step);
			Assert.AreSame(first, parser.ParseInstructionFile(step));

			// An instruction written without a ModuleType cannot be checked and is used as it is.
			var olderPath = Path.Join(fileSystem.BuildPath, "Start", "03. older.pr");
			fileSystem.AddFile(olderPath, Pr("", "RemoveDebugMode"));
			Assert.IsNotNull(parser.ParseInstructionFile(Step(olderPath, "PLang.Modules.EnvironmentModule")));
		}
	}
}
