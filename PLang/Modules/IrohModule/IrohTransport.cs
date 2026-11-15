using N0.IrohNet;
using System.Runtime.InteropServices;
using Endpoint = N0.IrohNet.Endpoint;
using System.Text;

namespace PLang.Modules.IrohModule
{
	/// <summary>
	/// Thin managed wrapper over the N0.IrohNet native calls. Every native call here is blocking, so the
	/// callers run them on their own threads. Out pointers must be initialised with the *_default()
	/// functions first, the native side drops the old value when it fills them (null crashes the process).
	/// </summary>
	public static unsafe class IrohKeys
	{
		public static (string Secret, string Id) Generate()
		{
			SecretKey* sk = iroh.secret_key_generate();
			try { return (Take(iroh.secret_key_as_base32(sk)), IdOf(sk)); }
			finally { iroh.secret_key_free(sk); }
		}

		public static string IdFromSecret(string secretBase32)
		{
			SecretKey* sk = LoadSecret(secretBase32);
			try { return IdOf(sk); }
			finally { iroh.secret_key_free(sk); }
		}

		internal static SecretKey* LoadSecret(string secretBase32)
		{
			SecretKey* sk = iroh.secret_key_default();
			IntPtr text = Marshal.StringToCoTaskMemUTF8(secretBase32);
			try
			{
				var result = iroh.secret_key_from_base32((byte*)text, &sk);
				if (result != KeyResult.KEY_RESULT_OK) throw new IrohException("The stored iroh secret key is not valid: " + result);
				return sk;
			}
			finally { Marshal.FreeCoTaskMem(text); }
		}

		private static string IdOf(SecretKey* sk)
		{
			PublicKey pub = iroh.secret_key_public(sk);
			return Take(iroh.public_key_as_base32(&pub));
		}

		internal static string Take(byte* native)
		{
			string value = Marshal.PtrToStringUTF8((IntPtr)native) ?? "";
			iroh.rust_free_string(native);
			return value;
		}
	}

	public class IrohException(string message) : Exception(message);

	public sealed unsafe class IrohEndpoint : IDisposable
	{
		private readonly nint endpoint;
		private readonly byte[] alpn;
		private bool disposed;

		public string Id { get; }

		private IrohEndpoint(nint endpoint, byte[] alpn, string id)
		{
			this.endpoint = endpoint;
			this.alpn = alpn;
			Id = id;
		}

		public static IrohEndpoint Bind(string secretKeyBase32, string alpnName, bool useRelay = true)
		{
			byte[] alpn = Encoding.ASCII.GetBytes(alpnName);
			SecretKey* sk = IrohKeys.LoadSecret(secretKeyBase32);
			string id = IrohKeys.IdFromSecret(secretKeyBase32);

			SocketAddrV4* v4 = iroh.socket_addr_v4_default();
			SocketAddrV6* v6 = iroh.socket_addr_v6_default();
			IntPtr any4 = Marshal.StringToCoTaskMemUTF8("0.0.0.0:0");
			IntPtr any6 = Marshal.StringToCoTaskMemUTF8("[::]:0");
			try
			{
				iroh.socket_addr_v4_from_string((byte*)any4, &v4);
				iroh.socket_addr_v6_from_string((byte*)any6, &v6);

				EndpointConfig config = iroh.endpoint_config_default();
				config.relay_mode = useRelay ? RelayMode.RELAY_MODE_DEFAULT : RelayMode.RELAY_MODE_DISABLED;
				config.discovery_cfg = useRelay ? DiscoveryConfig.DISCOVERY_CONFIG_D_N_S : DiscoveryConfig.DISCOVERY_CONFIG_NONE;
				fixed (byte* alpnPtr = alpn)
				{
					iroh.endpoint_config_add_alpn(&config, new slice_ref_uint8 { ptr = alpnPtr, len = (UIntPtr)alpn.Length });
				}
				iroh.endpoint_config_add_secret_key(&config, sk);

				Endpoint* ep = iroh.endpoint_default();
				var result = iroh.endpoint_bind(&config, v4, v6, &ep);
				if (result != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not bind the iroh endpoint: " + result);

				return new IrohEndpoint((nint)ep, alpn, id);
			}
			finally
			{
				Marshal.FreeCoTaskMem(any4);
				Marshal.FreeCoTaskMem(any6);
			}
		}

		public bool WaitUntilOnline(int timeoutMilliseconds)
		{
			Endpoint* ep = (Endpoint*)endpoint;
			return iroh.endpoint_online(&ep, (ulong)timeoutMilliseconds) == EndpointResult.ENDPOINT_RESULT_OK;
		}

		public string Ticket()
		{
			Endpoint* ep = (Endpoint*)endpoint;
			EndpointAddr addr = iroh.endpoint_addr_default();
			var result = iroh.endpoint_addr(&ep, &addr);
			if (result != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not read the endpoint address: " + result);
			return IrohKeys.Take(iroh.endpoint_addr_as_str(&addr));
		}

		/// <summary>Blocks until a peer connects. Returns null when the endpoint is closed.</summary>
		public IrohConnection? Accept()
		{
			if (disposed) return null;
			Endpoint* ep = (Endpoint*)endpoint;
			Connection* conn = iroh.connection_default();
			EndpointResult result;
			fixed (byte* alpnPtr = alpn)
			{
				result = iroh.endpoint_accept(&ep, new slice_ref_uint8 { ptr = alpnPtr, len = (UIntPtr)alpn.Length }, &conn);
			}
			if (result != EndpointResult.ENDPOINT_RESULT_OK) return null;
			return new IrohConnection((nint)conn);
		}

		/// <summary>Connect to a peer by endpoint id (found through address lookup) or by a full ticket.</summary>
		public IrohConnection Connect(string idOrTicket)
		{
			Endpoint* ep = (Endpoint*)endpoint;
			EndpointAddr addr = ParseAddress(idOrTicket);
			Connection* conn = iroh.connection_default();
			EndpointResult result;
			fixed (byte* alpnPtr = alpn)
			{
				result = iroh.endpoint_connect(&ep, new slice_ref_uint8 { ptr = alpnPtr, len = (UIntPtr)alpn.Length }, addr, &conn);
			}
			if (result != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException($"Could not connect to {Short(idOrTicket)}: {result}");
			return new IrohConnection((nint)conn);
		}

		public static bool IsTicket(string idOrTicket) => idOrTicket.StartsWith("endpoint", StringComparison.OrdinalIgnoreCase) && idOrTicket.Length > 80;

		private static EndpointAddr ParseAddress(string idOrTicket)
		{
			idOrTicket = idOrTicket.Trim().Trim('﻿').Trim();
			IntPtr text = Marshal.StringToCoTaskMemUTF8(idOrTicket);
			try
			{
				if (IsTicket(idOrTicket))
				{
					EndpointAddr parsed = iroh.endpoint_addr_default();
					var addrResult = iroh.endpoint_addr_from_string((byte*)text, &parsed);
					if (addrResult != AddrResult.ADDR_RESULT_OK) throw new IrohException("The iroh ticket is not valid: " + addrResult);
					return parsed;
				}

				PublicKey key = iroh.public_key_default();
				var keyResult = iroh.public_key_from_base32((byte*)text, &key);
				if (keyResult != KeyResult.KEY_RESULT_OK) throw new IrohException("The iroh endpoint id is not valid: " + keyResult);
				return iroh.endpoint_addr_new(key);
			}
			finally { Marshal.FreeCoTaskMem(text); }
		}

		private static string Short(string value) => value.Length > 16 ? value[..16] + "..." : value;

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			iroh.endpoint_close((Endpoint*)endpoint);
		}
	}

	public sealed unsafe class IrohConnection : IDisposable
	{
		private readonly nint connection;
		private bool disposed;

		internal IrohConnection(nint connection) { this.connection = connection; }

		public bool IsDisposed => disposed;

		public const int ChunkBytes = 64 * 1024;

		/// <summary>
		/// One request/answer per stream. The request is a 4 byte big endian header length, the header, then
		/// the file bytes when there is a file. The peer may answer and stop reading (declined), so a failed
		/// write still tries to read the answer before the write error is reported.
		/// </summary>
		public byte[] Request(byte[] header, Stream? file, int chunkTimeoutMilliseconds, int answerTimeoutMilliseconds, int maxAnswerBytes)
		{
			Connection* conn = (Connection*)connection;
			SendStream* send = iroh.send_stream_default();
			RecvStream* recv = iroh.recv_stream_default();
			var open = iroh.connection_open_bi(&conn, &send, &recv);
			if (open != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not open a stream to the peer: " + open);

			IrohException? writeError = null;
			try
			{
				byte[] length = [(byte)((header.Length >> 24) & 0xFF), (byte)((header.Length >> 16) & 0xFF), (byte)((header.Length >> 8) & 0xFF), (byte)(header.Length & 0xFF)];
				Write(send, length, length.Length, chunkTimeoutMilliseconds);
				Write(send, header, header.Length, chunkTimeoutMilliseconds);

				if (file != null)
				{
					byte[] buffer = new byte[ChunkBytes];
					int read;
					while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
					{
						Write(send, buffer, read, chunkTimeoutMilliseconds);
					}
				}
			}
			catch (IrohException ex) { writeError = ex; }

			iroh.send_stream_finish(send);

			try
			{
				return ReadAll(recv, answerTimeoutMilliseconds, maxAnswerBytes);
			}
			catch when (writeError != null)
			{
				throw writeError;
			}
		}

		private static void Write(SendStream* send, byte[] bytes, int count, int timeoutMilliseconds)
		{
			fixed (byte* ptr = bytes)
			{
				var written = iroh.send_stream_write_timeout(&send, new slice_ref_uint8 { ptr = ptr, len = (UIntPtr)count }, (ulong)timeoutMilliseconds);
				if (written == EndpointResult.ENDPOINT_RESULT_TIMEOUT) throw new IrohException("The peer stopped taking data");
				if (written != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not send to the peer: " + written);
			}
		}

		/// <summary>Blocks until the peer opens a stream. Null when the connection is closed.</summary>
		public IrohStream? AcceptStream()
		{
			Connection* conn = (Connection*)connection;
			SendStream* send = iroh.send_stream_default();
			RecvStream* recv = iroh.recv_stream_default();
			var result = iroh.connection_accept_bi(&conn, &send, &recv);
			if (result != EndpointResult.ENDPOINT_RESULT_OK) return null;
			return new IrohStream((nint)send, (nint)recv);
		}

		/// <summary>Blocks until the connection is closed by either side.</summary>
		public void WaitUntilClosed()
		{
			iroh.connection_closed((Connection*)connection);
		}

		internal static byte[] ReadAll(RecvStream* recv, int timeoutMilliseconds, int maxBytes)
		{
			Vec_uint8 buffer = iroh.rust_buffer_alloc(0);
			var result = iroh.recv_stream_read_to_end_timeout(&recv, &buffer, (UIntPtr)maxBytes, (ulong)timeoutMilliseconds);
			if (result == EndpointResult.ENDPOINT_RESULT_TIMEOUT) throw new IrohException("The peer did not answer in time");
			if (result != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not read from the peer: " + result);
			byte[] bytes = new byte[(int)buffer.len];
			Marshal.Copy((IntPtr)buffer.ptr, bytes, 0, bytes.Length);
			iroh.rust_buffer_free(buffer);
			return bytes;
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			iroh.connection_close((Connection*)connection);
		}
	}

	public sealed unsafe class IrohStream
	{
		private readonly nint send;
		private readonly nint recv;

		internal IrohStream(nint send, nint recv) { this.send = send; this.recv = recv; }

		public byte[] ReadAll(int timeoutMilliseconds, int maxBytes) => IrohConnection.ReadAll((RecvStream*)recv, timeoutMilliseconds, maxBytes);

		/// <summary>Reads up to count bytes. 0 means the peer finished the stream. A timeout or a broken stream throws.</summary>
		public int Read(byte[] buffer, int count, int timeoutMilliseconds)
		{
			RecvStream* stream = (RecvStream*)recv;
			fixed (byte* ptr = buffer)
			{
				long read = iroh.recv_stream_read_timeout(&stream, new slice_mut_uint8 { ptr = ptr, len = (UIntPtr)count }, (ulong)timeoutMilliseconds);
				if (read < 0) throw new IrohException("The peer stopped sending");
				return (int)read;
			}
		}

		/// <summary>Fills count bytes. False when the stream ended before the first byte, throws when it ends in the middle.</summary>
		public bool ReadExactly(byte[] buffer, int count, int timeoutMilliseconds)
		{
			int total = 0;
			byte[] part = new byte[count];
			while (total < count)
			{
				int read = Read(part, count - total, timeoutMilliseconds);
				if (read == 0)
				{
					if (total == 0) return false;
					throw new IrohException("The peer ended the stream in the middle of a message");
				}
				Array.Copy(part, 0, buffer, total, read);
				total += read;
			}
			return true;
		}

		public void WriteAndFinish(byte[] bytes)
		{
			SendStream* stream = (SendStream*)send;
			fixed (byte* ptr = bytes)
			{
				iroh.send_stream_write(&stream, new slice_ref_uint8 { ptr = ptr, len = (UIntPtr)bytes.Length });
			}
			iroh.send_stream_finish(stream);
		}
	}
}
