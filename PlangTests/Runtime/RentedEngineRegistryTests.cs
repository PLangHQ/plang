using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Utils;
using System.Reflection;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class RentedEngineRegistryTests
	{
		[TestMethod]
		public void Flags_EngineDoesNotKeepAppAlive_ListenerDoes()
		{
			var registry = new KeepAliveRegistry();
			var webserver = new object();
			var engine = Substitute.For<IEngine>();
			engine.Id.Returns("engine-1");

			var listener = registry.Add("Webserver", "default *:8080", webserver);
			var rented = registry.AddEngine(engine);

			Assert.IsTrue(listener.KeepsAppAlive);
			Assert.IsFalse(listener.WaitOnShutdown);
			Assert.IsFalse(rented.KeepsAppAlive);
			Assert.IsTrue(rented.WaitOnShutdown);
			Assert.AreEqual(KeepAliveRegistry.Engine, rented.Kind);
			Assert.AreEqual("engine-1", rented.Name);
			Assert.AreSame(engine, rented.Instance);

			Assert.AreEqual(2, registry.Count);
			Assert.AreEqual(1, registry.KeepsAppAliveCount);
			Assert.AreSame(webserver, registry.GetKeepAppAliveSnapshot().Single().Instance);
			Assert.AreSame(engine, registry.GetEngineSnapshot().Single().Instance);

			Assert.IsTrue(registry.RemoveEngine(engine));
			Assert.IsFalse(registry.RemoveEngine(engine), "already removed");
			engine.DidNotReceive().Dispose();
			Assert.AreEqual(1, registry.Count);
		}

		[TestMethod]
		public async Task KeepAlive_ReturnsAtOnce_WhenOnlyRentedEnginesAreRegistered()
		{
			var root = new Engine();
			var rented = Substitute.For<IEngine>();
			rented.Id.Returns("rented");
			root.KeepAliveRegistry.AddEngine(rented);

			var keepAlive = (Task)typeof(Engine).GetMethod("KeepAlive", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(root, null)!;
			var finished = await Task.WhenAny(keepAlive, Task.Delay(3000));

			Assert.AreSame(keepAlive, finished, "a console app with only a rented engine must not be kept alive");
			await keepAlive;
		}

		private (IEngine root, EnginePool pool, IServiceContainer container) CreatePool()
		{
			var logger = Substitute.For<ILogger>();
			var settings = Substitute.For<ISettings>();
			var fileSystem = Substitute.For<IPLangFileSystem>();
			// Created before the Returns calls: a substitute called inside Returns(...) confuses NSubstitute's last call.
			var memoryStackAccessor = Substitute.For<IMemoryStackAccessor>();
			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var pseudoRuntime = new PseudoRuntime(fileSystem, new PrParser(fileSystem, logger), logger);
			var variableHelper = new VariableHelper(settings, logger);
			var container = Substitute.For<IServiceContainer>();
			container.GetInstance(typeof(IMemoryStackAccessor)).Returns(memoryStackAccessor);
			container.GetInstance(typeof(IPLangContextAccessor)).Returns(contextAccessor);
			container.GetInstance(typeof(IPseudoRuntime)).Returns(pseudoRuntime);
			container.GetInstance(typeof(ISettings)).Returns(settings);
			container.GetInstance(typeof(VariableHelper)).Returns(variableHelper);

			var root = Substitute.For<IEngine>();
			root.ParentEngine.Returns((IEngine?)null);
			root.KeepAliveRegistry.Returns(new KeepAliveRegistry());
			root.Container.Returns(container);
			var pool = new EnginePool(root);
			root.EnginePool.Returns(pool);
			return (root, pool, container);
		}

		private static IEngine PooledEngine(EnginePool pool, string id)
		{
			var engine = Substitute.For<IEngine>();
			engine.Id.Returns(id);
			engine.IsInPool = true;
			pool.Pool.Push(engine);
			pool.engineIds.TryAdd(id, 1);
			return engine;
		}

		[TestMethod]
		public async Task RentAndReturn_RegisterAndRemoveTheEngine()
		{
			var (root, pool, _) = CreatePool();
			var pooled = PooledEngine(pool, "pooled");
			var step = new GoalStep { Text = "call goal Loop, dont wait", LineNumber = 4, RelativeGoalPath = "/Start.goal" };

			var engine = await pool.RentAsync(step);

			Assert.AreSame(pooled, engine);
			Assert.AreSame(step, engine.RentedFrom);
			var entry = root.KeepAliveRegistry.GetEngineSnapshot().Single();
			Assert.AreSame(engine, entry.Instance);
			Assert.AreEqual(0, root.KeepAliveRegistry.KeepsAppAliveCount);

			pool.Return(engine);

			Assert.AreEqual(0, root.KeepAliveRegistry.Count, "a returned engine leaves no entry behind");
			engine.Received(1).Reset(true);
			Assert.AreEqual(1, pool.Free);

			// Returning twice (already in the pool) is harmless.
			pool.Return(engine);
			Assert.AreEqual(0, root.KeepAliveRegistry.Count);
			Assert.AreEqual(1, pool.Free);
		}

		[TestMethod]
		public async Task Rent_WhenSettingUpTheEngineFails_LeavesNoEntryAndKeepsTheEngine()
		{
			var (root, pool, container) = CreatePool();
			PooledEngine(pool, "pooled");
			container.GetInstance(typeof(IMemoryStackAccessor)).Throws(new InvalidOperationException("boom"));

			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => pool.RentAsync(new GoalStep()));

			Assert.AreEqual(0, root.KeepAliveRegistry.Count);
			Assert.AreEqual(1, pool.Free, "the engine went back to the pool");
		}

		[TestMethod]
		public async Task Return_WhenResetThrows_StillRemovesTheEntry()
		{
			var (root, pool, _) = CreatePool();
			PooledEngine(pool, "pooled");
			var engine = await pool.RentAsync(new GoalStep());
			engine.When(e => e.Reset(Arg.Any<bool>())).Do(_ => throw new Exception("reset failed"));

			Assert.ThrowsException<Exception>(() => pool.Return(engine));

			Assert.AreEqual(0, root.KeepAliveRegistry.Count);
		}

		[TestMethod]
		public async Task ConcurrentRentAndReturn_NoEngineIsLostOrDuplicated()
		{
			const int poolSize = 8;
			const int workers = 4;
			const int iterations = 5000;

			var (root, pool, _) = CreatePool();
			var engines = new List<IEngine>();
			for (int i = 0; i < poolSize; i++) engines.Add(PooledEngine(pool, "pooled" + i));
			// The engines were pushed by hand; count them as created so Total, Free and Rented can be checked.
			typeof(EnginePool).GetField("createdCount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pool, poolSize);

			// Fewer workers than engines: the pool never runs empty unless an engine is lost, and then
			// RentAsync tries to create one, which these substitutes cannot, so a lost engine fails the test.
			var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
			{
				for (int i = 0; i < iterations; i++)
				{
					var engine = await pool.RentAsync(new GoalStep());
					pool.Return(engine);
				}
			})).ToArray();
			await Task.WhenAll(tasks);

			Assert.AreEqual(poolSize, pool.Total);
			Assert.AreEqual(poolSize, pool.Free, "every engine is back in the pool");
			Assert.AreEqual(0, pool.Rented);
			Assert.AreEqual(poolSize, pool.Pool.Distinct().Count(), "no engine is in the pool twice");
			CollectionAssert.AreEquivalent(engines.Select(p => p.Id).ToList(), pool.engineIds.Keys.ToList());
			Assert.AreEqual(0, root.KeepAliveRegistry.GetEngineSnapshot().Count, "no rented engine entry is left");
		}

		[TestMethod]
		public async Task OverlappingRents_EachEngineHasOneEntryUntilReturned()
		{
			var (root, pool, _) = CreatePool();
			for (int i = 0; i < 5; i++) PooledEngine(pool, "pooled" + i);

			for (int round = 0; round < 20; round++)
			{
				var rented = new List<IEngine>();
				for (int i = 0; i < 5; i++) rented.Add(await pool.RentAsync(new GoalStep()));
				Assert.AreEqual(5, root.KeepAliveRegistry.GetEngineSnapshot().Select(p => p.Instance).Distinct().Count());

				// Returned in another order than rented.
				foreach (var engine in rented.OrderBy(p => p.Id.GetHashCode() ^ round))
				{
					pool.Return(engine);
				}
				Assert.AreEqual(0, root.KeepAliveRegistry.Count);
			}
		}
	}
}
