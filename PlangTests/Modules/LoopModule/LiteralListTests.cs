using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.LoopModule;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;

namespace PLangTests.Modules.LoopModule
{
	[TestClass]
	public class LiteralListTests
	{
		[TestMethod]
		public void TryParse_JsonStrings()
		{
			Assert.IsTrue(LiteralList.TryParse("[\"cpu\",\"memory\",\"io\"]", out var items));
			CollectionAssert.AreEqual(new object[] { "cpu", "memory", "io" }, items);
		}

		[TestMethod]
		public void TryParse_JsonNumbers()
		{
			Assert.IsTrue(LiteralList.TryParse(" [1,2,3] ", out var items));
			CollectionAssert.AreEqual(new object[] { 1L, 2L, 3L }, items);
		}

		[TestMethod]
		public void TryParse_IndentedJsonArray()
		{
			// a json array value in the .pr file reaches the string parameter as indented json
			var value = new JArray("cpu", "memory", "io").ToString();
			Assert.IsTrue(LiteralList.TryParse(value, out var items));
			CollectionAssert.AreEqual(new object[] { "cpu", "memory", "io" }, items);
		}

		[TestMethod]
		public void TryParse_UnquotedItems()
		{
			Assert.IsTrue(LiteralList.TryParse("[cpu, 'memory', 2, %io%]", out var items));
			CollectionAssert.AreEqual(new object[] { "cpu", "memory", 2L, "%io%" }, items);
		}

		[TestMethod]
		public void TryParse_Empty()
		{
			Assert.IsTrue(LiteralList.TryParse("[]", out var items));
			Assert.AreEqual(0, items.Count);
		}

		[DataTestMethod]
		[DataRow("%list%")]
		[DataRow("list")]
		[DataRow("%obj.items%")]
		[DataRow("%list[0]%")]
		[DataRow("")]
		[DataRow(null)]
		public void TryParse_VariableIsNotLiteral(string? value)
		{
			Assert.IsFalse(LiteralList.TryParse(value, out _));
		}

		private (Program program, IPseudoRuntime pseudoRuntime, MemoryStack memoryStack, List<object?> calledWith) CreateProgram()
		{
			var fileSystem = Substitute.For<IPLangFileSystem>();
			var logger = Substitute.For<ILogger>();
			var settings = Substitute.For<ISettings>();
			var engine = Substitute.For<IEngine>();
			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var variableHelper = new VariableHelper(settings, logger);
			var runtime = new PseudoRuntime(fileSystem, new PrParser(fileSystem, logger), logger);
			var memoryStack = new MemoryStack(runtime, engine, settings, variableHelper, contextAccessor);
			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			contextAccessor.Current.Returns(context);

			var container = Substitute.For<IServiceContainer>();
			container.GetInstance(typeof(ILogger)).Returns(logger);

			var calledWith = new List<object?>();
			var pseudoRuntime = Substitute.For<IPseudoRuntime>();
			pseudoRuntime.RunGoal(Arg.Any<IEngine>(), Arg.Any<IPLangContextAccessor>(), Arg.Any<string>(), Arg.Any<GoalToCallInfo>(), Arg.Any<Goal?>(),
				Arg.Any<bool>(), Arg.Any<long>(), Arg.Any<uint>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<RuntimeEvent?>())
				.Returns(callInfo =>
				{
					var goalToCall = callInfo.ArgAt<GoalToCallInfo>(3);
					calledWith.Add(goalToCall.Parameters["name"]);
					return Task.FromResult<(IEngine, object?, IError?)>((engine, null, null));
				});

			var goal = new Goal { GoalName = "Start", RelativeAppStartupFolderPath = "/" };
			context.CallStack.EnterGoal(goal);

			var program = new Program(logger, pseudoRuntime, engine);
			program.Init(container, goal, null!, null!, contextAccessor);
			return (program, pseudoRuntime, memoryStack, calledWith);
		}

		[TestMethod]
		public async Task RunLoop_LiteralStringList_ItemNameFromParameter()
		{
			var (program, _, _, calledWith) = CreateProgram();
			var goalToCall = new GoalToCallInfo("Show", new Dictionary<string, object?> { { "item", "%name%" } });

			var error = await program.RunLoop("[\"cpu\",\"memory\",\"io\"]", goalToCall);

			Assert.IsNull(error, error?.ToString());
			CollectionAssert.AreEqual(new object[] { "cpu", "memory", "io" }, calledWith);
		}

		[TestMethod]
		public async Task RunLoop_LiteralNumberList()
		{
			var (program, _, _, calledWith) = CreateProgram();
			var goalToCall = new GoalToCallInfo("Show", new Dictionary<string, object?> { { "item", "%name%" } });

			var error = await program.RunLoop("[1,2,3]", goalToCall);

			Assert.IsNull(error, error?.ToString());
			CollectionAssert.AreEqual(new object[] { 1L, 2L, 3L }, calledWith);
		}

		[TestMethod]
		public async Task RunLoop_LiteralListWithVariableItems()
		{
			var (program, _, memoryStack, calledWith) = CreateProgram();
			memoryStack.Put("first", "cpu");
			memoryStack.Put("second", "io");
			var goalToCall = new GoalToCallInfo("Show", new Dictionary<string, object?> { { "item", "%name%" } });

			var error = await program.RunLoop("[%first%, %second%]", goalToCall);

			Assert.IsNull(error, error?.ToString());
			CollectionAssert.AreEqual(new object[] { "cpu", "io" }, calledWith);
		}

		[TestMethod]
		public async Task RunLoop_VariableList_Unchanged()
		{
			var (program, _, memoryStack, calledWith) = CreateProgram();
			memoryStack.Put("parts", new List<string> { "cpu", "memory", "io" });
			var goalToCall = new GoalToCallInfo("Show", new Dictionary<string, object?> { { "item", "%name%" } });

			var error = await program.RunLoop("%parts%", goalToCall);

			Assert.IsNull(error, error?.ToString());
			CollectionAssert.AreEqual(new object[] { "cpu", "memory", "io" }, calledWith);
		}
	}
}
