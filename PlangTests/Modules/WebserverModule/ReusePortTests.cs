using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebserverModule;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PLangTests.Modules.WebserverModule;

[TestClass]
public class ReusePortTests
{
	private static int FreePort()
	{
		using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
		return ((IPEndPoint)probe.LocalEndPoint!).Port;
	}

	private static async Task<IHost> StartHost(int port, string name, bool reusePort)
	{
		var host = Host.CreateDefaultBuilder()
			.ConfigureLogging(l => l.ClearProviders())
			.ConfigureWebHostDefaults(web =>
			{
				web.UseKestrel(k => k.Listen(IPAddress.Loopback, port));
				web.ConfigureServices(s =>
				{
					if (reusePort) ReusePortSocket.Enable(s);
				});
				web.Configure(app => app.Run(ctx => ctx.Response.WriteAsync(name)));
			})
			.Build();
		await host.StartAsync();
		return host;
	}

	[TestMethod]
	public async Task TwoHostsReusePort_BothStart_PortAnswers()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Assert.Inconclusive("SO_REUSEPORT is Linux only");

		int port = FreePort();
		using var hostA = await StartHost(port, "A", reusePort: true);
		using var hostB = await StartHost(port, "B", reusePort: true);

		var answers = new Dictionary<string, int>();
		for (int i = 0; i < 40; i++)
		{
			// A new connection for every request, the kernel picks the listener per connection
			using var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.Zero };
			using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
			var body = await client.GetStringAsync($"http://127.0.0.1:{port}/");
			answers[body] = answers.GetValueOrDefault(body) + 1;
		}

		Assert.AreEqual(40, answers.Values.Sum());
		// The kernel normally spreads the connections between the two sockets; only the answers are asserted hard
		Console.WriteLine($"Answers: {string.Join(", ", answers.Select(p => $"{p.Key}={p.Value}"))}");

		await hostA.StopAsync();
		await hostB.StopAsync();
	}

	[TestMethod]
	public async Task SecondHostWithoutReusePort_FailsWithAddressInUse()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Assert.Inconclusive("Linux only");

		int port = FreePort();
		using var hostA = await StartHost(port, "A", reusePort: false);

		await Assert.ThrowsExceptionAsync<IOException>(async () =>
		{
			using var hostB = await StartHost(port, "B", reusePort: false);
		});

		await hostA.StopAsync();
	}
}
