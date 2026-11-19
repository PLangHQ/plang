using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WebserverProperties = PLang.Modules.WebserverModule.Program.WebserverProperties;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class AppDrainerTests
	{
		[TestCleanup]
		public void Cleanup()
		{
			RuntimeMode.SetDraining(false);
			RuntimeMode.SetSchedulerEnabled(true);
		}

		private static int FreePort()
		{
			using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
			return ((IPEndPoint)probe.LocalEndPoint!).Port;
		}

		private static AppDrainer NewDrainer(KeepAliveRegistry registry, IEngine? calling = null) =>
			new AppDrainer(registry, calling, closeIroh: () => 0);

		[TestMethod]
		public async Task NoEntries_ReturnsAtOnce()
		{
			var registry = new KeepAliveRegistry();
			var watch = Stopwatch.StartNew();

			var result = await NewDrainer(registry).Drain(TimeSpan.FromSeconds(10), exitWhenDone: false);

			Assert.IsNotNull(result);
			Assert.IsTrue(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds} ms");
			Assert.IsFalse(result.TimedOut);
			Assert.AreEqual(0, result.EnginesWaitedFor);
			Assert.AreEqual(0, result.WebserversStopped);
			Assert.IsFalse(result.Exited);
			Assert.IsTrue(RuntimeMode.Draining);
			Assert.IsFalse(RuntimeMode.SchedulerEnabled);
		}

		[TestMethod]
		public async Task WaitOnShutdownEntry_FinishingAfter200Ms_IsWaitedFor()
		{
			var registry = new KeepAliveRegistry();
			var engine = Substitute.For<IEngine>();
			engine.Id.Returns("e1");
			registry.AddEngine(engine);
			_ = Task.Run(async () =>
			{
				await Task.Delay(200);
				registry.RemoveEngine(engine);
			});

			var watch = Stopwatch.StartNew();
			var result = await NewDrainer(registry).Drain(TimeSpan.FromSeconds(10), exitWhenDone: false);

			Assert.IsNotNull(result);
			Assert.IsFalse(result.TimedOut);
			Assert.AreEqual(1, result.EnginesWaitedFor);
			Assert.IsTrue(watch.ElapsedMilliseconds >= 190, $"took {watch.ElapsedMilliseconds} ms");
			Assert.IsTrue(watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
			Assert.IsTrue(result.WaitMs >= 190);
		}

		[TestMethod]
		public async Task EntryThatNeverFinishes_TimesOut()
		{
			var registry = new KeepAliveRegistry();
			var engine = Substitute.For<IEngine>();
			engine.Id.Returns("e1");
			registry.AddEngine(engine);

			var watch = Stopwatch.StartNew();
			var result = await NewDrainer(registry).Drain(TimeSpan.FromMilliseconds(400), exitWhenDone: false);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.TimedOut);
			Assert.AreEqual(1, result.EnginesWaitedFor);
			Assert.IsTrue(watch.ElapsedMilliseconds >= 390 && watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
		}

		[TestMethod]
		public async Task CallingEngine_IsNotWaitedFor()
		{
			var registry = new KeepAliveRegistry();
			var engine = Substitute.For<IEngine>();
			engine.Id.Returns("caller");
			registry.AddEngine(engine);

			var result = await NewDrainer(registry, engine).Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);

			Assert.IsNotNull(result);
			Assert.IsFalse(result.TimedOut);
			Assert.AreEqual(0, result.EnginesWaitedFor);
		}

		[TestMethod]
		public async Task ExitWhenDone_ClearsRegistry()
		{
			var registry = new KeepAliveRegistry();
			registry.Add("Scheduler", "Scheduler", new object());

			var result = await NewDrainer(registry).Drain(TimeSpan.FromSeconds(5), exitWhenDone: true);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Exited);
			Assert.AreEqual(0, registry.Count);
		}

		[TestMethod]
		public async Task WithoutExit_KeepsOtherEntries()
		{
			var registry = new KeepAliveRegistry();
			registry.Add("Scheduler", "Scheduler", new object());

			await NewDrainer(registry).Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);

			Assert.AreEqual(1, registry.Count);
		}

		[TestMethod]
		public async Task SecondCall_DoesNotStartSecondDrain()
		{
			var registry = new KeepAliveRegistry();
			var drainer = NewDrainer(registry);

			var first = await drainer.Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);
			var second = await drainer.Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);

			Assert.IsNotNull(first);
			Assert.IsNull(second);
			Assert.IsTrue(drainer.IsDraining);
		}

		[TestMethod]
		public async Task IrohClose_IsCountedAndRegistryEntriesRemoved()
		{
			var registry = new KeepAliveRegistry();
			var endpoint = new object();
			registry.Add("iroh-listen-default", "iroh-listen-default", endpoint);
			var drainer = new AppDrainer(registry, null, closeIroh: () => 3);

			var result = await drainer.Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);

			Assert.IsNotNull(result);
			Assert.AreEqual(3, result.IrohClosed);
			Assert.AreEqual(0, registry.Count);
		}

		[TestMethod]
		public async Task LiveConnections_AreClosed()
		{
			var registry = new KeepAliveRegistry();
			var live = new ConcurrentDictionary<string, Engine.LiveConnection>();
			var engine = Substitute.For<IEngine>();
			engine.LiveConnections.Returns(live);
			var httpContext = new DefaultHttpContext();
			live["a"] = new Engine.LiveConnection(httpContext.Response, true);
			var props = new WebserverProperties { Engine = engine };
			registry.Add("Webserver", "default", props);

			var result = await NewDrainer(registry).Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);

			Assert.IsNotNull(result);
			Assert.AreEqual(1, result.LiveConnectionsClosed);
			Assert.AreEqual(0, live.Count);
		}

		private static async Task<(IHost Host, WebserverProperties Props, int Port)> StartHost(TimeSpan handlerDelay, KeepAliveRegistry registry)
		{
			int port = FreePort();
			var host = Host.CreateDefaultBuilder()
				.ConfigureLogging(l => l.ClearProviders())
				.ConfigureWebHostDefaults(web =>
				{
					web.UseKestrel(k => k.Listen(IPAddress.Loopback, port));
					web.Configure(app => app.Run(async ctx =>
					{
						await Task.Delay(handlerDelay, ctx.RequestAborted);
						await ctx.Response.WriteAsync("done");
					}));
				})
				.Build();
			await host.StartAsync();
			var props = new WebserverProperties(Port: port) { Listener = host };
			registry.Add("Webserver", "default " + port, props);
			return (host, props, port);
		}

		[TestMethod]
		public async Task RealHost_RequestInFlight_CompletesAndNewConnectionIsRefused()
		{
			var registry = new KeepAliveRegistry();
			var (host, _, port) = await StartHost(TimeSpan.FromMilliseconds(300), registry);

			using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
			var request = client.GetAsync($"http://127.0.0.1:{port}/");
			await Task.Delay(100);

			var result = await NewDrainer(registry).Drain(TimeSpan.FromSeconds(5), exitWhenDone: false);
			var response = await request;

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual("done", await response.Content.ReadAsStringAsync());
			Assert.IsNotNull(result);
			Assert.IsFalse(result.TimedOut);
			Assert.AreEqual(1, result.WebserversStopped);

			using var second = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.Zero }) { Timeout = TimeSpan.FromSeconds(5) };
			await Assert.ThrowsExceptionAsync<HttpRequestException>(() => second.GetAsync($"http://127.0.0.1:{port}/"));
			Assert.AreEqual(0, registry.Count);
			host.Dispose();
		}

		[TestMethod]
		public async Task RealHost_HandlerSlowerThanTimeout_EndsAtTimeoutAndReportsTimedOut()
		{
			var registry = new KeepAliveRegistry();
			var (host, _, port) = await StartHost(TimeSpan.FromSeconds(5), registry);

			using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
			var request = client.GetAsync($"http://127.0.0.1:{port}/");
			await Task.Delay(100);

			var watch = Stopwatch.StartNew();
			var result = await NewDrainer(registry).Drain(TimeSpan.FromMilliseconds(100), exitWhenDone: false);
			watch.Stop();

			Assert.IsNotNull(result);
			Assert.IsTrue(result.TimedOut);
			Assert.IsTrue(watch.ElapsedMilliseconds < 2000, $"took {watch.ElapsedMilliseconds} ms");
			try { await request; } catch (HttpRequestException) { }
			host.Dispose();
		}
	}
}
