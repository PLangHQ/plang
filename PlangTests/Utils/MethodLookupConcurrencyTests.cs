using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Services.SettingsService;
using PLang.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils.Tests
{
	// Steps of many requests resolve their module type and method at the same time, while a hot reload hands out new
	// instruction and function objects and new engines construct a TypeHelper. An existing method must be found every time.
	[TestClass]
	public class MethodLookupConcurrencyTests
	{
		private static readonly (string Module, string Method)[] Calls =
		[
			("PLang.Modules.EnvironmentModule", "RemoveDebugMode"),
			("PLang.Modules.EnvironmentModule", "SetDebugMode"),
			("PLang.Modules.EnvironmentModule", "IsInDebugMode"),
			("PLang.Modules.EnvironmentModule", "GetMachineName"),
			("PLang.Modules.ThrowErrorModule", "EndApp"),
			("PLang.Modules.ThrowErrorModule", "EndGoalExecution"),
			("PLang.Modules.EnvironmentModule", "EndApp"),
		];

		private static MethodHelper NewMethodHelper()
		{
			return new MethodHelper(Substitute.For<ITypeHelper>(), Substitute.For<ILogger>(), Substitute.For<IPLangContextAccessor>(), Substitute.For<ISettings>());
		}

		private static ITypeHelper NewTypeHelper()
		{
			var fileSystem = Substitute.For<IPLangFileSystem>();
			fileSystem.GoalsPath.Returns("/app");
			return new TypeHelper(fileSystem, new DependancyHelper(fileSystem, Substitute.For<ILogger>()));
		}

		private static (IGenericFunction Function, GoalStep Step) NewStep(string module, string method)
		{
			var step = new GoalStep { Text = "step " + method, ModuleType = module };
			var function = new GenericFunction("", method, new List<Parameter>());
			var instruction = new Instruction { ModuleType = module, Function = function, Step = step };
			step.Instruction = instruction;
			function.Instruction = instruction;
			return (function, step);
		}

		[TestMethod]
		public async Task GetMethod_ManyThreads_SharedAndReloadedFunctions_AlwaysFound()
		{
			var typeHelper = NewTypeHelper();
			var methodHelpers = Enumerable.Range(0, 4).Select(_ => NewMethodHelper()).ToArray();

			// The step objects of the loaded goals, shared by every request until a reload replaces them.
			var shared = new (IGenericFunction Function, GoalStep Step)[Calls.Length];
			for (int i = 0; i < Calls.Length; i++) shared[i] = NewStep(Calls[i].Module, Calls[i].Method);

			var failures = new ConcurrentQueue<string>();
			int resolved = 0;
			using var stop = new CancellationTokenSource();

			// Hot reload: new goal, instruction and function objects every few milliseconds; new engines build a TypeHelper.
			var reloader = Task.Run(async () =>
			{
				while (!stop.IsCancellationRequested)
				{
					for (int i = 0; i < Calls.Length; i++) shared[i] = NewStep(Calls[i].Module, Calls[i].Method);
					NewTypeHelper();
					await Task.Delay(1);
				}
			});

			var workers = Enumerable.Range(0, Environment.ProcessorCount * 4).Select(w => Task.Run(async () =>
			{
				var random = new Random(w);
				for (int n = 0; n < 3000; n++)
				{
					int i = random.Next(Calls.Length);
					var (function, step) = random.Next(4) == 0 ? NewStep(Calls[i].Module, Calls[i].Method) : shared[i];
					try
					{
						var type = typeHelper.GetRuntimeType(step.ModuleType);
						if (type == null)
						{
							failures.Enqueue($"GetRuntimeType({step.ModuleType}) returned null");
							continue;
						}
						var instance = RuntimeHelpers.GetUninitializedObject(type);
						var method = await methodHelpers[random.Next(methodHelpers.Length)].GetMethod(instance, function);
						if (method == null || method.Name != function.Name || !method.DeclaringType!.IsAssignableFrom(type))
						{
							failures.Enqueue($"{type.FullName}.{function.Name} resolved to {method?.DeclaringType?.FullName}.{method?.Name}");
							continue;
						}
						Interlocked.Increment(ref resolved);
					}
					catch (Exception ex)
					{
						failures.Enqueue($"{step.ModuleType}.{function.Name}: {ex.GetType().Name}: {ex.Message}");
					}
				}
			})).ToArray();

			await Task.WhenAll(workers);
			stop.Cancel();
			await reloader;

			Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
			Assert.AreEqual(workers.Length * 3000, resolved);
		}

		[TestMethod]
		public async Task GetMethod_SameFunctionObject_OnTwoModuleTypes_ResolvesForEachType()
		{
			// A function object remembers the method it was resolved to; the module type it is run on decides which method that is.
			var methodHelper = NewMethodHelper();
			var (function, _) = NewStep("PLang.Modules.EnvironmentModule", "EndApp");

			var environment = RuntimeHelpers.GetUninitializedObject(typeof(PLang.Modules.EnvironmentModule.Program));
			var throwError = RuntimeHelpers.GetUninitializedObject(typeof(PLang.Modules.ThrowErrorModule.Program));

			for (int n = 0; n < 100; n++)
			{
				var a = await methodHelper.GetMethod(environment, function);
				var b = await methodHelper.GetMethod(throwError, function);
				Assert.AreEqual(typeof(PLang.Modules.EnvironmentModule.Program), a!.DeclaringType);
				Assert.AreEqual(typeof(PLang.Modules.ThrowErrorModule.Program), b!.DeclaringType);
			}
		}

		[TestMethod]
		public async Task GetMethod_StepRunOnAModuleWithoutTheMethod_GivesTheProdMessage()
		{
			// The message seen on prod ("Method RemoveDebugMode could not be found that matches with your statement. " with
			// nothing after it) is what a lookup gives when the module the step runs on has no method of that name. When the
			// module has the name but the parameters do not match, the parameter errors are appended to the message.
			var methodHelper = NewMethodHelper();
			var (function, _) = NewStep("PLang.Modules.EnvironmentModule", "RemoveDebugMode");
			var throwError = RuntimeHelpers.GetUninitializedObject(typeof(PLang.Modules.ThrowErrorModule.Program));

			var ex = await Assert.ThrowsExceptionAsync<PLang.Exceptions.MethodNotFoundException>(() => methodHelper.GetMethod(throwError, function));
			Assert.AreEqual("Method RemoveDebugMode could not be found that matches with your statement. ", ex.Message);
		}
	}
}
