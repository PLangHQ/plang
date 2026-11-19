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
