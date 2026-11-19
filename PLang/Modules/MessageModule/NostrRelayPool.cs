using Microsoft.Extensions.Logging;
using NNostr.Client;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace PLang.Modules.MessageModule
{
	/// <summary>
	/// The relays the app talks to. One pool per process: every goal run (each with its own service container) sends and listens
	/// over the same websockets. A relay that cannot be reached does not hold back the others, and a relay that drops is
	/// reconnected and its subscriptions are sent again.
	/// </summary>
	public sealed class NostrRelayPool : IDisposable
	{
		private static readonly Lazy<NostrRelayPool> shared = new(() => new NostrRelayPool());
		public static NostrRelayPool Shared => shared.Value;

		private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan OkTimeout = TimeSpan.FromSeconds(10);
		private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(15);

		private readonly ConcurrentDictionary<string, NostrClient> relays = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, NostrSubscriptionFilter[]> subscriptions = new();
		private readonly ConcurrentDictionary<string, DateTime> lastFailure = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, CancellationTokenSource> connectionLifetimes = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, SemaphoreSlim> connecting = new(StringComparer.OrdinalIgnoreCase);
		private readonly CancellationTokenSource lifetime = new();
		private Task? reconnectLoop;

		public ILogger? Logger { get; set; }

		/// <summary>Every event of every subscription, from every relay. The same event can come from more than one relay.</summary>
		public event Action<string, NostrEvent>? EventReceived;

		private NostrRelayPool() { }

		public IReadOnlyCollection<string> Relays => relays.Keys.ToList();

		/// <summary>Makes the pool hold exactly these relays and connects the ones that are not open.</summary>
		public async Task SetRelays(IEnumerable<string> relayUrls)
		{
			var wanted = relayUrls.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			foreach (var url in relays.Keys.Where(k => !wanted.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
			{
				RemoveRelay(url);
			}
			foreach (var url in wanted)
			{
				relays.GetOrAdd(url, CreateClient);
			}
			await ConnectAll();
			StartReconnectLoop();
		}

		public void RemoveRelay(string relayUrl)
		{
			if (relays.TryRemove(Normalize(relayUrl), out var client))
			{
				client.Dispose();
			}
		}

		/// <summary>Sends the event to every open relay (and to the extra relays, e.g. the inbox relays of the receiver) and returns the relays that accepted it.</summary>
		public async Task<List<string>> Publish(NostrEvent nostrEvent, IEnumerable<string>? extraRelays = null)
		{
			await ConnectAll();

			var targets = relays.ToList();
			var temporary = new List<NostrClient>();
			foreach (var url in (extraRelays ?? []).Select(Normalize).Where(u => !relays.ContainsKey(u)).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				var client = CreateClient(url);
				temporary.Add(client);
				if (await TryConnect(url, client)) targets.Add(new(url, client));
			}

			try
			{
				var accepted = new ConcurrentBag<string>();
				await Task.WhenAll(targets.Where(t => t.Value.State == WebSocketState.Open).Select(async target =>
				{
					var ok = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
					void OnOk(object? sender, (string eventId, bool success, string message) e)
					{
						if (e.eventId != nostrEvent.Id) return;
						if (!e.success) Logger?.LogWarning($"Relay {target.Key} refused event {nostrEvent.Id}: {e.message}");
						ok.TrySetResult(e.success);
					}
					target.Value.OkReceived += OnOk;
					try
					{
						await target.Value.PublishEvent(nostrEvent);
						var done = await Task.WhenAny(ok.Task, Task.Delay(OkTimeout));
						if (done == ok.Task && ok.Task.Result) accepted.Add(target.Key);
					}
					catch (Exception ex)
					{
						Logger?.LogWarning($"Could not send to relay {target.Key}: {ex.Message}");
					}
					finally
					{
						target.Value.OkReceived -= OnOk;
					}
				}));
				return accepted.ToList();
			}
			finally
			{
				temporary.ForEach(c => c.Dispose());
			}
		}

		/// <summary>Opens a subscription on every relay, and again on a relay after it reconnects.</summary>
		public async Task Subscribe(string subscriptionId, NostrSubscriptionFilter[] filters)
		{
			subscriptions[subscriptionId] = filters;
			await ConnectAll();
			await Task.WhenAll(relays.Values.Where(c => c.State == WebSocketState.Open).Select(c => SafeSubscribe(c, subscriptionId, filters)));
			StartReconnectLoop();
		}

		/// <summary>Asks the relays once for the events that match the filters, and waits until each relay has answered or the time is up.</summary>
		public async Task<List<NostrEvent>> Fetch(NostrSubscriptionFilter[] filters, TimeSpan timeout)
		{
			await ConnectAll();
			var found = new ConcurrentDictionary<string, NostrEvent>();
			using var cts = new CancellationTokenSource(timeout);
			await Task.WhenAll(relays.Values.Where(c => c.State == WebSocketState.Open).Select(async client =>
			{
				try
				{
					foreach (var e in await client.FetchEvents(filters, cts.Token)) found.TryAdd(e.Id, e);
				}
				catch (Exception) { }
			}));
			return found.Values.ToList();
		}

		private NostrClient CreateClient(string url)
		{
			var client = new NostrClient(new Uri(url));
			client.EventsReceived += (sender, e) =>
			{
				foreach (var nostrEvent in e.events)
				{
					EventReceived?.Invoke(e.subscriptionId, nostrEvent);
				}
			};
			return client;
		}

		/// <summary>Connects the relays that are not open. A relay that failed lately is left to the reconnect loop (force), so a dead
		/// relay does not hold up every send.</summary>
		private async Task ConnectAll(bool force = false)
		{
			var toConnect = relays.Where(r => r.Value.State != WebSocketState.Open
				&& (force || !lastFailure.TryGetValue(r.Key, out var failedAt) || DateTime.UtcNow - failedAt > ReconnectInterval)).ToList();
			await Task.WhenAll(toConnect.Select(async r =>
			{
				// One connect per relay at a time; a relay that another caller is connecting is not waited for.
				var gate = connecting.GetOrAdd(r.Key, _ => new SemaphoreSlim(1, 1));
				if (!await gate.WaitAsync(0)) return;
				try
				{
					if (r.Value.State == WebSocketState.Open) return;
					if (!await TryConnect(r.Key, r.Value))
					{
						lastFailure[r.Key] = DateTime.UtcNow;
						return;
					}
					lastFailure.TryRemove(r.Key, out _);
					foreach (var subscription in subscriptions)
					{
						await SafeSubscribe(r.Value, subscription.Key, subscription.Value);
					}
				}
				finally
				{
					gate.Release();
				}
			}));
		}

		private async Task<bool> TryConnect(string url, NostrClient client)
		{
			// NostrClient opens the socket with its lifetime token, not the connection token, so a relay that does not answer would
			// hold the connect until the TCP timeout of the OS (over a minute). The connection gets its own lifetime token, cancelled
			// when the connect takes longer than ConnectTimeout, and otherwise kept for as long as the connection lives.
			if (connectionLifetimes.TryRemove(url, out var previous))
			{
				previous.Cancel();
				previous.Dispose();
			}
			var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
			try
			{
				var connect = client.ConnectAndWaitUntilConnected(connectionLifetime.Token, connectionLifetime.Token);
				if (await Task.WhenAny(connect, Task.Delay(ConnectTimeout)) != connect)
				{
					connectionLifetime.Cancel();
					_ = connect.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
					Logger?.LogDebug($"Relay {url} did not answer in {ConnectTimeout.TotalSeconds} seconds");
					connectionLifetime.Dispose();
					return false;
				}
				await connect;
				if (client.State != WebSocketState.Open)
				{
					connectionLifetime.Dispose();
					return false;
				}
				connectionLifetimes[url] = connectionLifetime;
				return true;
			}
			catch (Exception ex)
			{
				Logger?.LogDebug($"Could not connect to relay {url}: {ex.Message}");
				connectionLifetime.Dispose();
				return false;
			}
		}

		private async Task SafeSubscribe(NostrClient client, string subscriptionId, NostrSubscriptionFilter[] filters)
		{
			try
			{
				await client.CreateSubscription(subscriptionId, filters);
			}
			catch (Exception ex)
			{
				Logger?.LogWarning($"Could not subscribe on relay {client.Relay}: {ex.Message}");
			}
		}

		private void StartReconnectLoop()
		{
			if (reconnectLoop != null) return;
			reconnectLoop = Task.Run(async () =>
			{
				while (!lifetime.IsCancellationRequested)
				{
					try
					{
						await Task.Delay(ReconnectInterval, lifetime.Token);
						await ConnectAll(force: true);
					}
					catch (OperationCanceledException) { return; }
					catch (Exception ex)
					{
						Logger?.LogWarning($"Relay reconnect failed: {ex.Message}");
					}
				}
			});
		}

		public static string Normalize(string relayUrl)
		{
			relayUrl = relayUrl.Trim().TrimEnd('/');
			if (!relayUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) && !relayUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
			{
				relayUrl = "wss://" + relayUrl;
			}
			return relayUrl;
		}

		public void Dispose()
		{
			lifetime.Cancel();
			foreach (var client in relays.Values) client.Dispose();
			relays.Clear();
		}
	}
}
