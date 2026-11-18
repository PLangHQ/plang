using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Errors;
using System.Reflection;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class KeepAliveRegistryTests
	{
		private sealed class DisposableThing : IDisposable
		{
			public int DisposeCount;
			public void Dispose() => DisposeCount++;
		}

		// A rented engine gets its parent from EnginePool.CreateEngine through SetParentEngine, which also
		// needs a file system; the parent link is all the registry looks at.
		private static Engine CreateEngine(IEngine? parent = null)
		{
			var engine = new Engine();
			if (parent != null)
			{
				typeof(Engine).GetField("_parentEngine", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(engine, parent);
			}
			return engine;
		}

		[TestMethod]
		public void Add_Remove_Snapshot()
		{
			var registry = new KeepAliveRegistry();
			var webserver = new object();
			var scheduler = new object();

			var before = DateTime.UtcNow;
			registry.Add("Webserver", "default *:8080", webserver);
			registry.Add("Scheduler", "Scheduler", scheduler);

			var snapshot = registry.GetSnapshot();
			Assert.AreEqual(2, registry.Count);
			Assert.AreEqual(2, snapshot.Count);
			Assert.AreEqual("Webserver", snapshot[0].Kind);
			Assert.AreEqual("default *:8080", snapshot[0].Name);
			Assert.AreSame(webserver, snapshot[0].Instance);
			Assert.IsTrue(snapshot[0].StartedUtc >= before && snapshot[0].StartedUtc <= DateTime.UtcNow);
			Assert.AreEqual(DateTimeKind.Utc, snapshot[0].StartedUtc.Kind);
			Assert.AreEqual("Scheduler", snapshot[1].Kind);

			Assert.IsFalse(registry.Remove(webserver, "Scheduler"), "kind must match");
			Assert.IsTrue(registry.Remove(webserver, "Webserver"));
			Assert.AreEqual(1, registry.Count);
			Assert.AreEqual(2, snapshot.Count, "a snapshot is a copy");
			Assert.AreSame(scheduler, registry.GetSnapshot().Single().Instance);

			registry.Clear();
			Assert.AreEqual(0, registry.Count);
		}

		[TestMethod]
		public void Remove_DisposesInstance()
		{
			var registry = new KeepAliveRegistry();
			var instance = new DisposableThing();
			registry.Add("iroh-listen-a", "iroh-listen-a", instance);

			Assert.IsTrue(registry.Remove(instance, "iroh-listen-a"));
			Assert.AreEqual(1, instance.DisposeCount);
			Assert.AreEqual(0, registry.Count);

			Assert.IsFalse(registry.Remove(instance, "iroh-listen-a"));
			Assert.AreEqual(1, instance.DisposeCount, "an instance that is not registered is not disposed");
		}

		[TestMethod]
		public void Clear_DoesNotDispose()
		{
			var registry = new KeepAliveRegistry();
			var instance = new DisposableThing();
			registry.Add("Webserver", "default *:8080", instance);

			registry.Clear();

			Assert.AreEqual(0, instance.DisposeCount);
		}

		[TestMethod]
		public void RemoveCompletedTasks_RemovesOnlyCompletedDontWaitTasks()
		{
			var registry = new KeepAliveRegistry();
			var running = new TaskCompletionSource<(object? Variables, IError? Error)>();
			var done = Task.FromResult<(object? Variables, IError? Error)>((null, null));
			var faulted = Task.FromException<(object? Variables, IError? Error)>(new InvalidOperationException("boom"));
			var otherKindTask = Task.CompletedTask;

			registry.Add(KeepAliveRegistry.DontWait, "Running", running.Task);
			registry.Add(KeepAliveRegistry.DontWait, "Done", done);
			registry.Add(KeepAliveRegistry.DontWait, "Faulted", faulted);
			registry.Add("Webserver", "default *:8080", otherKindTask);

			var removed = registry.RemoveCompletedTasks();

			CollectionAssert.AreEquivalent(new[] { "Done", "Faulted" }, removed.Select(p => p.Name).ToList());
			CollectionAssert.AreEqual(new[] { "Running", "default *:8080" }, registry.GetSnapshot().Select(p => p.Name).ToList());

			running.SetResult((null, null));
			Assert.AreEqual("Running", registry.RemoveCompletedTasks().Single().Name);
			Assert.AreEqual(1, registry.Count);
		}

		[TestMethod]
		public void KeepAliveRegistry_FromRentedEngine_IsTheRootEnginesRegistry()
		{
			var root = CreateEngine();
			var rented = CreateEngine(root);
			var rentedFromRented = CreateEngine(rented);

			Assert.AreSame(root.KeepAliveRegistry, rented.KeepAliveRegistry);
			Assert.AreSame(root.KeepAliveRegistry, rentedFromRented.KeepAliveRegistry);

			var task = new TaskCompletionSource<(object? Variables, IError? Error)>().Task;
			rentedFromRented.KeepAliveRegistry.Add(KeepAliveRegistry.DontWait, "Loop", task);

			var entry = root.KeepAliveRegistry.GetSnapshot().Single();
			Assert.AreEqual("Loop", entry.Name);
			Assert.AreSame(task, entry.Instance);
		}

		[TestMethod]
		public void KeepAliveRegistry_ThroughSubstitutedRoot_UsesRootInstance()
		{
			var rootRegistry = new KeepAliveRegistry();
			var root = Substitute.For<IEngine>();
			root.ParentEngine.Returns((IEngine?)null);
			root.KeepAliveRegistry.Returns(rootRegistry);

			var rented = CreateEngine(root);

			Assert.AreSame(rootRegistry, rented.KeepAliveRegistry);
		}
	}
}
