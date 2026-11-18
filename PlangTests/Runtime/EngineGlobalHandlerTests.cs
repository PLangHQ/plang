using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class EngineGlobalHandlerTests
	{
		// AppDomain.UnhandledException is stored in a static event field of AppContext (or AppDomain on older runtimes).
		private static int UnhandledExceptionHandlerCount()
		{
			foreach (var type in new[] { typeof(AppContext), typeof(AppDomain) })
			{
				var field = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
					.FirstOrDefault(f => f.FieldType == typeof(UnhandledExceptionEventHandler));
				if (field != null)
				{
					var handler = field.GetValue(null) as Delegate;
					return handler?.GetInvocationList().Length ?? 0;
				}
			}
			Assert.Inconclusive("Could not find the UnhandledException event field on this runtime");
			return -1;
		}

		[TestMethod]
		public void CreatingAndDisposingEngines_DoesNotAddUnhandledExceptionHandlers()
		{
			// The first engine in the process may add the one process wide handler.
			new Engine().Dispose();
			int before = UnhandledExceptionHandlerCount();
			Assert.IsTrue(before >= 1, "unhandled exceptions are still written out");

			for (int i = 0; i < 50; i++)
			{
				var engine = new Engine();
				engine.Dispose();
			}

			Assert.AreEqual(before, UnhandledExceptionHandlerCount());
		}
	}
}
