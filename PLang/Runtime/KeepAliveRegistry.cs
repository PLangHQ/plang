namespace PLang.Runtime
{
	/// <summary>
	/// One thing running in the app: a webserver, the scheduler, a "dont wait" goal, a file watcher, a rented engine, ...
	/// Kind is the reason printed in "Keeping app alive, reasons:", Name tells entries of the same kind apart
	/// (webserver name and port, goal name, engine id), Instance is the object that runs; for a goal running without
	/// its caller waiting ("dont wait", a scheduled run) it is a PlangTask, for a rented engine the IEngine.
	/// KeepsAppAlive: Engine.KeepAlive waits while an entry with it exists (listeners, "dont wait" and scheduled tasks).
	/// A rented engine does not: it serves a request or a task that something else keeps alive, and returns by itself.
	/// WaitOnShutdown: the entry is work in progress that a graceful shutdown should let finish (a rented engine),
	/// as opposed to a listener that only needs to stop accepting.
	/// </summary>
	public record KeepAliveEntry(string Kind, string Name, DateTime StartedUtc, object Instance, bool KeepsAppAlive = true, bool WaitOnShutdown = false);

	/// <summary>
	/// Everything that runs in the app. There is one per app, owned by the root engine (IEngine.KeepAliveRegistry
	/// walks ParentEngine to the top), so an entry added from a rented engine, e.g. a webserver started inside
	/// a "dont wait" goal, is seen by the root engine like any other. The app stays running while an entry
	/// with KeepsAppAlive exists.
	/// Thread safe: "dont wait" goals add themselves from their own threads, engines are rented and returned
	/// on request threads, while Engine.KeepAlive removes finished tasks once a second.
	/// </summary>
	public class KeepAliveRegistry
	{
		public const string DontWait = "WaitForExecution";
		public const string Schedule = "Schedule";
		/// <summary>An engine rented from the EnginePool, from rent until it is returned.</summary>
		public const string Engine = "Engine";

		private readonly object sync = new();
		private readonly List<KeepAliveEntry> entries = new();

		/// <summary>All entries, rented engines included.</summary>
		public int Count
		{
			get { lock (sync) return entries.Count; }
		}

		/// <summary>Entries that keep the app running.</summary>
		public int KeepsAppAliveCount
		{
			get { lock (sync) return entries.Count(p => p.KeepsAppAlive); }
		}

		public KeepAliveEntry Add(string kind, string name, object instance, bool keepsAppAlive = true, bool waitOnShutdown = false)
		{
			var entry = new KeepAliveEntry(kind, name, DateTime.UtcNow, instance, keepsAppAlive, waitOnShutdown);
			lock (sync) entries.Add(entry);
			return entry;
		}

		/// <summary>
		/// Registers an engine rented from the EnginePool. It does not keep the app alive; a graceful shutdown waits for it.
		/// </summary>
		public KeepAliveEntry AddEngine(IEngine engine)
		{
			return Add(Engine, engine.Id, engine, keepsAppAlive: false, waitOnShutdown: true);
		}

		/// <summary>
		/// Removes the entry of a returned engine. The engine goes back to the pool, so it is not disposed.
		/// Returns false when the engine has no entry (never rented, or already returned).
		/// </summary>
		public bool RemoveEngine(IEngine engine)
		{
			return Remove(engine, Engine, dispose: false);
		}

		/// <summary>
		/// Removes the entry of this kind holding this instance and, when dispose is true, disposes the instance
		/// when it is IDisposable. Returns false when no such entry exists; the instance is then left as it is.
		/// </summary>
		public bool Remove(object instance, string kind, bool dispose = true)
		{
			KeepAliveEntry? entry;
			lock (sync)
			{
				entry = entries.FirstOrDefault(p => p.Kind == kind && ReferenceEquals(p.Instance, instance));
				if (entry == null) return false;
				entries.Remove(entry);
			}
			if (dispose && instance is IDisposable disposable) disposable.Dispose();
			return true;
		}

		/// <summary>
		/// Removes every PlangTask entry ("dont wait", scheduled run) whose task has completed (ran to the end,
		/// faulted or was cancelled) and returns them, so the caller can report faulted ones.
		/// </summary>
		public List<KeepAliveEntry> RemoveCompletedTasks()
		{
			lock (sync)
			{
				var completed = entries.Where(p => p.Instance is PlangTask task && task.IsCompleted).ToList();
				foreach (var entry in completed)
				{
					entries.Remove(entry);
				}
				return completed;
			}
		}

		/// <summary>
		/// Removes every entry without disposing anything; Engine.KeepAlive then returns and the app shuts down.
		/// </summary>
		public void Clear()
		{
			lock (sync) entries.Clear();
		}

		/// <summary>
		/// A copy of the current entries in the order they were added. Safe to enumerate while entries are added or removed.
		/// </summary>
		public List<KeepAliveEntry> GetSnapshot()
		{
			lock (sync) return entries.ToList();
		}

		/// <summary>The entries that keep the app running, in the order they were added.</summary>
		public List<KeepAliveEntry> GetKeepAppAliveSnapshot()
		{
			lock (sync) return entries.Where(p => p.KeepsAppAlive).ToList();
		}

		/// <summary>The rented engines, in the order they were rented.</summary>
		public List<KeepAliveEntry> GetEngineSnapshot()
		{
			lock (sync) return entries.Where(p => p.Kind == Engine).ToList();
		}
	}
}
