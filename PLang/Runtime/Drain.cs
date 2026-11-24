using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PLang.Utils;
using System.Diagnostics;
using WebserverProperties = PLang.Modules.WebserverModule.Program.WebserverProperties;

namespace PLang.Runtime
{
	/// <summary>
	/// What `drain app` returns. Written as JSON the names are snake case.
	/// StopAcceptingMs, WaitMs and GoingAwayMs are the time of phase 1, 2 and 3, TotalMs the whole drain.
	/// EnginesWaitedFor is how many rented engines were running at some point during the wait (the engine of the step that
	/// drains is not counted), TimedOut is true when the timeout came before everything had finished.
	/// LiveConnectionsClosed are the long lived HTTP responses that were ended, IrohClosed the iroh endpoints and connections
	/// that were closed, WebserversStopped the webservers that no longer accept connections. Exited: the registry was cleared, so the app ends.
	/// StillRunning: what was still running when the timeout came (cut when the process ends); empty when nothing timed out.
	/// </summary>
	[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
	public record DrainResult(long StopAcceptingMs, long WaitMs, long GoingAwayMs, long TotalMs, int EnginesWaitedFor, bool TimedOut,
		int LiveConnectionsClosed, int IrohClosed, int WebserversStopped, bool Exited, List<DrainStillRunning>? StillRunning = null);

	/// <summary>A run that had not finished when the drain timed out: its kind (Engine, Schedule, ...), name and start.</summary>
	[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
	public record DrainStillRunning(string Kind, string Name, DateTime StartedUtc);

	/// <summary>
	/// A graceful drain of the running app, so another process (started with --standby, listening on the same port with `reuse port`)
	/// can take over without dropped requests.
	/// 1. Stop accepting: for one second every HTTP/1.x answer carries Connection: close, so busy keep-alive clients move to the new
	///    process; then every "Webserver" entry of the registry gets IHost.StopAsync (Kestrel stops accepting and lets requests in flight
	///    finish until the timeout), and the scheduler starts no new runs.
	/// 2. Wait: until no entry with WaitOnShutdown (a rented engine serving a request, a "dont wait" or scheduled run) is left, or the timeout.
	/// 3. Going away: what is still open is closed so the client connects again: long lived HTTP responses (Engine.LiveConnections),
	///    the iroh endpoints and their connections. Then the webserver hosts are awaited, they end at the timeout at the latest.
	/// 4. With exitWhenDone the registry is cleared, so Engine.KeepAlive returns and the process ends with exit code 0.
	/// Server side WebSockets are not tracked anywhere in the runtime, so none are closed.
	/// </summary>
	public class AppDrainer
	{
		public const int DefaultTimeoutSeconds = 30;
		public const string TimeoutSetting = "DrainTimeoutSeconds";
		public const int PollMilliseconds = 100;
		private const int IrohCloseWaitMilliseconds = 5000;
		private const int LiveConnectionCloseWaitMilliseconds = 1000;
		private const int HostStopGraceMilliseconds = 2000;
		private const int CalledFromRequestGraceMilliseconds = 300;
		// How long the webservers keep answering, each answer with Connection: close, before they stop. A client busy on a
		// keep-alive connection gets its close with an answer and opens its next connection to the new process; without
		// it, Kestrel closes the connection while the client's next request is already on its way, and that request is lost.
		private const int ConnectionCloseWindowMilliseconds = 1000;
		private const string IrohListenKindPrefix = "iroh-listen-";

		private readonly KeepAliveRegistry registry;
		private readonly IEngine? callingEngine;
		private readonly Func<int> closeIroh;
		private int started;

		/// <summary>
		/// callingEngine is the engine that runs the `drain app` step; it is rented, so it is in the registry, and is not waited for.
		/// closeIroh closes the iroh endpoints and connections and returns how many; the default closes those of the iroh module.
		/// </summary>
		public AppDrainer(KeepAliveRegistry registry, IEngine? callingEngine = null, Func<int>? closeIroh = null)
		{
			this.registry = registry;
			this.callingEngine = callingEngine;
			this.closeIroh = closeIroh ?? CloseIrohModule;
		}

		public bool IsDraining => Volatile.Read(ref started) == 1;

		private static int CloseIrohModule()
		{
			var (endpoints, connections) = Modules.IrohModule.Program.CloseAll();
			return endpoints + connections;
		}

		/// <summary>
		/// Runs the drain. Returns null when this drainer has already started one (a second call does not start a second drain).
		/// </summary>
		public async Task<DrainResult?> Drain(TimeSpan timeout, bool exitWhenDone = true)
		{
			if (Interlocked.Exchange(ref started, 1) == 1) return null;

			var total = Stopwatch.StartNew();
			var phase = Stopwatch.StartNew();
			RuntimeMode.SetDraining(true);
			using var deadline = new CancellationTokenSource(timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout);
			bool timedOut = false;

			// Phase 1: stop accepting
			RuntimeMode.SetSchedulerEnabled(false);
			var webservers = registry.GetSnapshot()
				.Where(p => p.Kind == Modules.WebserverModule.Program.WebserverKeepAliveKind && p.Instance is WebserverProperties)
				.Select(p => (Entry: p, Properties: (WebserverProperties)p.Instance))
				.ToList();
			if (webservers.Count > 0)
			{
				try { await Task.Delay(ConnectionCloseWindowMilliseconds, deadline.Token); }
				catch (OperationCanceledException) { }
			}
			// When the drain is a step of a request, Kestrel must not abort connections at the timeout: that would also cut the
			// connection the drain answer goes out on. Requests still running then end with the process instead.
			bool calledFromRequest = callingEngine?.RunningContext?.HttpContext != null;
			var stopToken = calledFromRequest ? CancellationToken.None : deadline.Token;
			var stops = webservers.Select(p => StopHost(p.Properties, stopToken)).ToList();
			await Task.Yield();
			long stopAcceptingMs = phase.ElapsedMilliseconds;

			// Phase 2: wait for the work in progress
			phase.Restart();
			var waitedFor = new HashSet<KeepAliveEntry>(ReferenceEqualityComparer.Instance);
			var stillRunning = new List<DrainStillRunning>();
			while (true)
			{
				var pending = registry.GetSnapshot().Where(IsWaitedFor).ToList();
				foreach (var entry in pending) waitedFor.Add(entry);
				if (pending.Count == 0) break;

				if (deadline.IsCancellationRequested)
				{
					timedOut = true;
					stillRunning.AddRange(pending.Select(p => new DrainStillRunning(p.Kind, (p.Instance as IEngine)?.Name ?? p.Name, p.StartedUtc)));
					break;
				}
				await Task.Delay(PollMilliseconds);
			}
			long waitMs = phase.ElapsedMilliseconds;

			// Phase 3: going away
			phase.Restart();
			int liveClosed = await CloseLiveConnections(webservers.Select(p => p.Properties));
			int irohClosed = await CloseIroh();

			var stopsDone = Task.WhenAll(stops);

			// When the drain is a step of a request of a host that is being drained, that host cannot finish its stop before this request ends,
			// so waiting for it would hold the answer until the timeout, and disposing the host would cut the answer. The listening sockets
			// are closed already; the host finishes and goes away when the request is done.
			var hostWait = calledFromRequest ? TimeSpan.FromMilliseconds(CalledFromRequestGraceMilliseconds) : timeout + TimeSpan.FromMilliseconds(HostStopGraceMilliseconds);

			// Kestrel returns at the token at the latest, the grace only keeps a host that does not honour the token from holding the drain
			var finished = await Task.WhenAny(stopsDone, Task.Delay(hostWait));
			int webserversStopped = 0;
			if (finished == stopsDone)
			{
				foreach (var (stopped, stopTimedOut) in stopsDone.Result)
				{
					if (stopped) webserversStopped++;
					if (stopTimedOut) timedOut = true;
				}
			}
			else if (calledFromRequest)
			{
				// the sockets are closed, the hosts finish with the request
				webserversStopped = webservers.Count;
			}
			else
			{
				timedOut = true;
			}

			foreach (var (entry, properties) in webservers)
			{
				if (!calledFromRequest) { try { properties.Listener?.Dispose(); } catch { } }
				Modules.WebserverModule.Program.ForgetListener(properties);
				registry.Remove(properties, entry.Kind, dispose: false);
			}
			foreach (var entry in registry.GetSnapshot().Where(p => p.Kind.StartsWith(IrohListenKindPrefix)))
			{
				registry.Remove(entry.Instance, entry.Kind, dispose: false);
			}
			long goingAwayMs = phase.ElapsedMilliseconds;

			// Phase 4: let the app end
			if (exitWhenDone) registry.Clear();

			return new DrainResult(stopAcceptingMs, waitMs, goingAwayMs, total.ElapsedMilliseconds, waitedFor.Count, timedOut,
				liveClosed, irohClosed, webserversStopped, exitWhenDone, stillRunning);
		}

		private bool IsWaitedFor(KeepAliveEntry entry)
		{
			return entry.WaitOnShutdown && !ReferenceEquals(entry.Instance, callingEngine);
		}

		/// <summary>
		/// Stops the host: Kestrel closes its listening sockets at once and then waits for the requests in flight until the token is cancelled.
		/// TimedOut is true when the token was cancelled before the host was done.
		/// </summary>
		private static async Task<(bool Stopped, bool TimedOut)> StopHost(WebserverProperties webserver, CancellationToken token)
		{
			if (webserver.Listener == null) return (false, false);
			try
			{
				await webserver.Listener.StopAsync(token);
				return (true, token.IsCancellationRequested);
			}
			catch (OperationCanceledException)
			{
				return (true, true);
			}
			catch (Exception)
			{
				return (false, false);
			}
		}

		/// <summary>
		/// Ends the long lived HTTP responses (poll, server sent events) of the webserver engines. The response is completed, so the client
		/// sees the end of the stream and connects again; one that does not complete in a second is aborted.
		/// </summary>
		private static async Task<int> CloseLiveConnections(IEnumerable<WebserverProperties> webservers)
		{
			int closed = 0;
			foreach (var engine in webservers.Select(p => p.Engine).Where(p => p?.LiveConnections != null).Distinct())
			{
				foreach (var key in engine.LiveConnections.Keys.ToList())
				{
					if (!engine.LiveConnections.TryRemove(key, out var live)) continue;
					try
					{
						var complete = live.Response.CompleteAsync();
						if (await Task.WhenAny(complete, Task.Delay(LiveConnectionCloseWaitMilliseconds)) != complete) live.Response.HttpContext.Abort();
					}
					catch
					{
						try { live.Response.HttpContext.Abort(); } catch { }
					}
					closed++;
				}
			}
			return closed;
		}

		/// <summary>
		/// Closing an iroh endpoint waits for its connections, so it runs on its own thread and the drain does not wait longer than a few seconds for it.
		/// </summary>
		private async Task<int> CloseIroh()
		{
			var task = Task.Run(closeIroh);
			try
			{
				if (await Task.WhenAny(task, Task.Delay(IrohCloseWaitMilliseconds)) == task) return task.Result;
			}
			catch
			{
			}
			return 0;
		}
	}
}
