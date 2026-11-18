namespace PLang.Runtime
{
	/// <summary>
	/// One thing that keeps the app running: a webserver, the scheduler, a "dont wait" goal, a file watcher, ...
	/// Kind is the reason printed in "Keeping app alive, reasons:", Name tells entries of the same kind apart
	/// (webserver name and port, goal name), Instance is the object kept alive; for a goal running without
	/// its caller waiting ("dont wait", a scheduled run) it is a PlangTask.
	/// </summary>
	public record KeepAliveEntry(string Kind, string Name, DateTime StartedUtc, object Instance);

	/// <summary>
	/// The app stays running while this registry holds an entry. There is one per app, owned by the root engine
	/// (IEngine.KeepAliveRegistry walks ParentEngine to the top), so an entry added from a rented engine, e.g.
	/// a webserver started inside a "dont wait" goal, is waited on by the root engine like any other.
	/// Thread safe: "dont wait" goals add themselves from their own threads while Engine.KeepAlive removes
	/// finished ones once a second.
	/// </summary>
	public class KeepAliveRegistry
	{
		public const string DontWait = "WaitForExecution";
		public const string Schedule = "Schedule";

		private readonly object sync = new();
		private readonly List<KeepAliveEntry> entries = new();

		public int Count
		{
			get { lock (sync) return entries.Count; }
		}

		public KeepAliveEntry Add(string kind, string name, object instance)
		{
			var entry = new KeepAliveEntry(kind, name, DateTime.UtcNow, instance);
			lock (sync) entries.Add(entry);
			return entry;
		}

		/// <summary>
		/// Removes the entry of this kind holding this instance and disposes the instance when it is IDisposable.
		/// Returns false when no such entry exists; the instance is then left as it is.
		/// </summary>
		public bool Remove(object instance, string kind)
		{
			KeepAliveEntry? entry;
			lock (sync)
			{
				entry = entries.FirstOrDefault(p => p.Kind == kind && ReferenceEquals(p.Instance, instance));
				if (entry == null) return false;
				entries.Remove(entry);
			}
			if (instance is IDisposable disposable) disposable.Dispose();
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
	}
}
