using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PLang.Modules.WebserverModule;

/// <summary>
/// Binds the listening socket of a Kestrel host with SO_REUSEPORT, so a second process (or host) can listen on the
/// same port at the same time and the kernel spreads the new connections between them. Used by `start webserver ... reuse port`.
/// </summary>
public static class ReusePortSocket
{
	// Linux: SOL_SOCKET = 1, SO_REUSEPORT = 15
	private const int SolSocket = 1;
	private const int SoReusePort = 15;

	public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

	private const string MigrateRequestsSetting = "/proc/sys/net/ipv4/tcp_migrate_req";

	/// <summary>
	/// Each listener of a port shared with SO_REUSEPORT has its own accept queue. When the old process closes its listener
	/// (`drain app` during a promote), the connections still in its queue are reset, unless the kernel moves them to
	/// another listener of the port, which it does when net.ipv4.tcp_migrate_req is 1 (Linux 5.14+).
	/// Returns a warning when the setting is 0 or the kernel does not have it, else null.
	/// </summary>
	public static string? QueuedConnectionsLostOnCloseWarning()
	{
		if (!IsSupported) return null;

		string? value = null;
		try { value = File.ReadAllText(MigrateRequestsSetting).Trim(); }
		catch (Exception) { }
		if (value == "1") return null;

		var state = value == null ? "is not available in this kernel (needs Linux 5.14+)" : $"is {value}";
		return $"reuse port: net.ipv4.tcp_migrate_req {state}, so connections waiting in this process's accept queue are " +
			"dropped when it closes its listener (`drain app` during a promote). Set it to 1 to hand them to the new process: " +
			"`echo 'net.ipv4.tcp_migrate_req = 1' | sudo tee /etc/sysctl.d/60-plang-reuseport.conf && sudo sysctl --system`. " +
			"See tcp_migrate_req in https://docs.kernel.org/networking/ip-sysctl.html";
	}

	/// <summary>Makes the host bind its sockets with SO_REUSEPORT. Does nothing outside Linux.</summary>
	public static void Enable(IServiceCollection services)
	{
		if (!IsSupported) return;
		services.Configure<SocketTransportOptions>(o => o.CreateBoundListenSocket = CreateBoundListenSocket);
	}

	public static Socket CreateBoundListenSocket(EndPoint endpoint)
	{
		if (endpoint is not IPEndPoint ipEndPoint)
		{
			// Unix sockets and the like have no port to share
			return SocketTransportOptions.CreateDefaultBoundListenSocket(endpoint);
		}

		var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		try
		{
			socket.SetRawSocketOption(SolSocket, SoReusePort, BitConverter.GetBytes(1));

			// Same as the default of Kestrel: listening on IPv6Any also takes IPv4
			if (ipEndPoint.Address.Equals(IPAddress.IPv6Any))
			{
				socket.DualMode = true;
			}
			socket.Bind(ipEndPoint);
			return socket;
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}
}
