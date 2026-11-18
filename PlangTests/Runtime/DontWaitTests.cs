using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils;
using PLang.Building.Parsers;
using PLang.SafeFileSystem;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class DontWaitTests
	{
		[TestMethod]
		public void KeepAlive_TaskFromRentedEngine_IsRegisteredOnRootEngine()
		{
			var rootContext = new PLangAppContext();
			var rentedContext = new PLangAppContext();

			var root = Substitute.For<IEngine>();
			root.ParentEngine.Returns((IEngine?)null);
			root.GetAppContext().Returns(rootContext);

			var rented = Substitute.For<IEngine>();
			rented.ParentEngine.Returns(root);
			rented.GetAppContext().Returns(rentedContext);

			var tcs = new TaskCompletionSource<(object? Variables, IError? Error)>();
			PseudoRuntime.KeepAlive(rented, tcs.Task);

			var alives = rootContext.GetOrDefault<List<Alive>>("KeepAlive");
			Assert.IsNotNull(alives);
			var waitForExecution = alives.Single(p => p.Key == "WaitForExecution");
			Assert.AreEqual(1, waitForExecution.Instances.Count);
			Assert.AreSame(tcs.Task, ((PseudoRuntime.EngineWait)waitForExecution.Instances[0]).task);

			Assert.IsNull(rentedContext.GetOrDefault<List<Alive>>("KeepAlive"));
		}

		[TestMethod]
		public void CopyVariables_SystemVariablesStayDynamic_UserVariablesAreCopied()
		{
			var fileSystem = Substitute.For<IPLangFileSystem>();
			var logger = Substitute.For<ILogger>();
			var settings = Substitute.For<ISettings>();
			var engine = Substitute.For<IEngine>();
			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var variableHelper = new VariableHelper(settings, logger);

			var runtime = new PseudoRuntime(fileSystem, new PrParser(fileSystem, logger), logger);
			var source = new MemoryStack(runtime, engine, settings, variableHelper, contextAccessor);
			var target = new MemoryStack(runtime, engine, settings, variableHelper, contextAccessor);
			source.Put("name", "Jim");

			runtime.CopyVariables(source, target);

			var targetVariables = target.GetMemoryStack().ToList();
			foreach (var systemVariable in new[] { "Now", "NowUtc", ReservedKeywords.GUID })
			{
				var ov = targetVariables.Single(p => p.Name == systemVariable);
				Assert.IsInstanceOfType(ov, typeof(DynamicObjectValue), systemVariable);
			}
			Assert.AreEqual("Jim", target.Get("name"));
		}
	}
}
