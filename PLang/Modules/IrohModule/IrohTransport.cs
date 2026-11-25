using N0.IrohNet;
using System.Runtime.InteropServices;
using Endpoint = N0.IrohNet.Endpoint;
using System.Text;

namespace PLang.Modules.IrohModule
{
	/// <summary>
	/// Thin managed wrapper over the N0.IrohNet native calls (iroh-c-ffi). Every native call here is blocking, so the
	/// callers run them on their own threads.
	///
	/// Ownership, as iroh-c-ffi defines it: a function that takes a T** (a Rust <c>&amp;Box&lt;T&gt;</c>) borrows the box and
	/// only replaces the value inside it, so the address of a local copy of the pointer is fine. A function that takes
	/// a T* by value consumes and frees it: endpoint_close, connection_close, connection_closed, send_stream_finish
	/// and every *_free. Out boxes must come from the *_default() functions, a null one crashes the process.
	/// Each box is freed exactly once, and never while another thread is inside a call on it (see NativeHandle).
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
				if (result != KeyResult.KEY_RESULT_OK)
				{
					iroh.secret_key_free(sk);
					throw new IrohException("The stored iroh secret key is not valid: " + result);
				}
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

	/// <summary>The connection has ended (closed by the peer, the network or this side), so nothing more goes over it.</summary>
	public class IrohConnectionEndedException(string message) : IrohException(message);

	/// <summary>
	/// A native box that several threads call into. A call runs between TryEnter and Exit. Release hands the box to the
	/// native function that consumes it (close or free) exactly once: right away when no call is running, otherwise
	/// when the last running call exits. After Release, TryEnter fails, so no call starts on a freed box.
	/// </summary>
	internal sealed class NativeHandle(nint pointer)
	{
		private readonly object sync = new();
		private int users;
		private bool released;
		private Action<nint>? pendingRelease;

		public bool IsReleased
		{
			get { lock (sync) return released; }
		}

		public bool TryEnter(out nint value)
		{
			lock (sync)
			{
				if (released)
				{
					value = 0;
					return false;
				}
				users++;
				value = pointer;
				return true;
			}
		}

		public void Exit()
		{
			Action<nint> release;
			lock (sync)
			{
				users--;
				if (users > 0 || pendingRelease == null) return;
				release = pendingRelease;
				pendingRelease = null;
			}
			release(pointer);
		}

		public void Release(Action<nint> release)
		{
			lock (sync)
			{
				if (released) return;
				released = true;
				if (users > 0)
				{
					pendingRelease = release;
					return;
				}
			}
			release(pointer);
		}
	}

	public sealed unsafe class IrohEndpoint : IDisposable
	{
		public const int FirstAcceptRetryMilliseconds = 200;
		public const int MaxAcceptRetryMilliseconds = 1000;

		private readonly NativeHandle handle;
		private readonly byte[] alpn;

		public string Id { get; }

		public bool IsClosed => handle.IsReleased;

		private IrohEndpoint(nint endpoint, byte[] alpn, string id)
		{
			handle = new NativeHandle(endpoint);
			this.alpn = alpn;
			Id = id;
		}

		/// <summary>Binds the endpoint's UDP sockets on every address, on the given port (0: a free one).</summary>
		public static IrohEndpoint Bind(string secretKeyBase32, string alpnName, bool useRelay = true, int port = 0)
		{
			byte[] alpn = Encoding.ASCII.GetBytes(alpnName);
			string id = IrohKeys.IdFromSecret(secretKeyBase32);
			SecretKey* sk = IrohKeys.LoadSecret(secretKeyBase32);

			SocketAddrV4* v4 = iroh.socket_addr_v4_default();
			SocketAddrV6* v6 = iroh.socket_addr_v6_default();
			IntPtr any4 = Marshal.StringToCoTaskMemUTF8($"0.0.0.0:{port}");
			IntPtr any6 = Marshal.StringToCoTaskMemUTF8($"[::]:{port}");
			EndpointConfig config = iroh.endpoint_config_default();
			try
			{
				iroh.socket_addr_v4_from_string((byte*)any4, &v4);
				iroh.socket_addr_v6_from_string((byte*)any6, &v6);

				config.relay_mode = useRelay ? RelayMode.RELAY_MODE_DEFAULT : RelayMode.RELAY_MODE_DISABLED;
				config.discovery_cfg = useRelay ? DiscoveryConfig.DISCOVERY_CONFIG_D_N_S : DiscoveryConfig.DISCOVERY_CONFIG_NONE;
				fixed (byte* alpnPtr = alpn)
				{
					iroh.endpoint_config_add_alpn(&config, new slice_ref_uint8 { ptr = alpnPtr, len = (UIntPtr)alpn.Length });
				}
				iroh.endpoint_config_add_secret_key(&config, sk);

				// endpoint_bind consumes v4 and v6; the endpoint copies what it needs from the config.
				Endpoint* ep = iroh.endpoint_default();
				var result = iroh.endpoint_bind(&config, v4, v6, &ep);
				if (result != EndpointResult.ENDPOINT_RESULT_OK)
				{
					iroh.endpoint_free(ep);
					throw new IrohException("Could not bind the iroh endpoint: " + result);
				}

				return new IrohEndpoint((nint)ep, alpn, id);
			}
			finally
			{
				iroh.endpoint_config_free(config);
				Marshal.FreeCoTaskMem(any4);
				Marshal.FreeCoTaskMem(any6);
			}
		}

		public bool WaitUntilOnline(int timeoutMilliseconds)
		{
			nint pointer = Enter();
			try
			{
				Endpoint* ep = (Endpoint*)pointer;
				return iroh.endpoint_online(&ep, (ulong)timeoutMilliseconds) == EndpointResult.ENDPOINT_RESULT_OK;
			}
			finally { handle.Exit(); }
		}

		public string Ticket()
		{
			nint pointer = Enter();
			try
			{
				Endpoint* ep = (Endpoint*)pointer;
				EndpointAddr addr = iroh.endpoint_addr_default();
				try
				{
					var result = iroh.endpoint_addr(&ep, &addr);
					if (result != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not read the endpoint address: " + result);
					return IrohKeys.Take(iroh.endpoint_addr_as_str(&addr));
				}
				finally { iroh.endpoint_addr_free(addr); }
			}
			finally { handle.Exit(); }
		}

		/// <summary>
		/// Blocks until a peer connects. Null when the endpoint is closed. An IrohException when one incoming connection
		/// failed (handshake, wrong protocol); the endpoint keeps listening, so the caller may accept again.
		/// </summary>
		public IrohConnection? Accept()
		{
			if (!handle.TryEnter(out nint pointer)) return null;
			Connection* conn = iroh.connection_default();
			EndpointResult result;
			try
			{
				Endpoint* ep = (Endpoint*)pointer;
				fixed (byte* alpnPtr = alpn)
				{
					result = iroh.endpoint_accept(&ep, new slice_ref_uint8 { ptr = alpnPtr, len = (UIntPtr)alpn.Length }, &conn);
				}
			}
			finally { handle.Exit(); }

			if (result != EndpointResult.ENDPOINT_RESULT_OK)
			{
				iroh.connection_free(conn);
				if (handle.IsReleased) return null;
				throw new IrohException("An incoming iroh connection failed: " + result);
			}
			if (handle.IsReleased)
			{
				iroh.connection_close(conn);
				return null;
			}
			return Track(new IrohConnection((nint)conn));
		}

		/// <summary>
		/// Accepts peers until the endpoint is closed and hands every stream they open, with its connection, to onStream on
		/// the thread pool; onConnectionClosed gets each connection once it has closed. Blocks the calling thread. A failed
		/// accept is one peer whose handshake failed, the endpoint itself keeps working, so it is reported to onFailure and
		/// the next accept waits a little (200 ms doubling up to 1 s, back to none after a success). That bounds the work a
		/// flood of bad handshakes causes without holding up good peers long.
		/// </summary>
		public void Serve(Func<IrohStream, IrohConnection, Task> onStream, Action<Exception>? onFailure = null, Action<IrohConnection>? onConnectionClosed = null)
		{
			int retryMilliseconds = 0;
			while (true)
			{
				IrohConnection? connection;
				try
				{
					connection = Accept();
					retryMilliseconds = 0;
				}
				catch (IrohException ex)
				{
					onFailure?.Invoke(ex);
					retryMilliseconds = retryMilliseconds == 0 ? FirstAcceptRetryMilliseconds : Math.Min(retryMilliseconds * 2, MaxAcceptRetryMilliseconds);
					Thread.Sleep(retryMilliseconds);
					continue;
				}
				if (connection == null) return;

				var accepted = connection;
				try
				{
					Task.Factory.StartNew(() =>
					{
						accepted.ServeStreams(stream => onStream(stream, accepted), onFailure);
						try { onConnectionClosed?.Invoke(accepted); }
						catch (Exception ex) { onFailure?.Invoke(ex); }
					}, TaskCreationOptions.LongRunning);
				}
				catch (Exception ex)
				{
					accepted.Dispose();
					onFailure?.Invoke(ex);
				}
			}
		}

		/// <summary>Connect to a peer by endpoint id (found through address lookup) or by a full ticket.</summary>
		public IrohConnection Connect(string idOrTicket)
		{
			EndpointAddr addr = ParseAddress(idOrTicket);
			nint pointer;
			try { pointer = Enter(); }
			catch
			{
				iroh.endpoint_addr_free(addr);
				throw;
			}

			Connection* conn = iroh.connection_default();
			EndpointResult result;
			try
			{
				Endpoint* ep = (Endpoint*)pointer;
				fixed (byte* alpnPtr = alpn)
				{
					// endpoint_connect consumes addr.
					result = iroh.endpoint_connect(&ep, new slice_ref_uint8 { ptr = alpnPtr, len = (UIntPtr)alpn.Length }, addr, &conn);
				}
			}
			finally { handle.Exit(); }

			if (result != EndpointResult.ENDPOINT_RESULT_OK)
			{
				iroh.connection_free(conn);
				throw new IrohException($"Could not connect to {Short(idOrTicket)}: {result}");
			}
			return Track(new IrohConnection((nint)conn));
		}

		/// <summary>An endpoint id is 64 hex characters, so only a ticket starts with "endpoint".</summary>
		public static bool IsTicket(string idOrTicket) => idOrTicket.StartsWith("endpoint", StringComparison.OrdinalIgnoreCase);

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
					if (addrResult != AddrResult.ADDR_RESULT_OK)
					{
						iroh.endpoint_addr_free(parsed);
						throw new IrohException("The iroh ticket is not valid: " + addrResult);
					}
					return parsed;
				}

				PublicKey key = iroh.public_key_default();
				var keyResult = iroh.public_key_from_base32((byte*)text, &key);
				if (keyResult != KeyResult.KEY_RESULT_OK) throw new IrohException("The iroh endpoint id is not valid: " + keyResult);
				return iroh.endpoint_addr_new(key);
			}
			finally { Marshal.FreeCoTaskMem(text); }
		}

		private readonly System.Collections.Concurrent.ConcurrentDictionary<IrohConnection, byte> connections = new();

		/// <summary>Remembers a connection of this endpoint, so CloseConnections can close it. Disposed ones are dropped on the way.</summary>
		private IrohConnection Track(IrohConnection connection)
		{
			foreach (var disposed in connections.Keys.Where(p => p.IsDisposed).ToList()) connections.TryRemove(disposed, out _);
			connections[connection] = 0;
			return connection;
		}

		/// <summary>
		/// Closes every connection this endpoint accepted or made that is still open. Each one is freed once (NativeHandle),
		/// and one with a call running on another thread is freed when that call returns. The native API closes a
		/// connection without an application close code or reason. Returns how many were closed.
		/// </summary>
		public int CloseConnections()
		{
			int closed = 0;
			foreach (var connection in connections.Keys.ToList())
			{
				if (!connections.TryRemove(connection, out _) || connection.IsDisposed) continue;
				connection.Dispose();
				closed++;
			}
			return closed;
		}

		private static string Short(string value) => value.Length > 16 ? value[..16] + "..." : value;

		private nint Enter()
		{
			if (!handle.TryEnter(out nint pointer)) throw new IrohException("The iroh endpoint is closed");
			return pointer;
		}

		/// <summary>
		/// Closes the endpoint. A thread blocked in Accept holds it open until that accept returns (the next peer that
		/// connects), then the endpoint closes and Accept returns null.
		/// </summary>
		public void Dispose()
		{
			handle.Release(pointer => iroh.endpoint_close((Endpoint*)pointer));
		}
	}

	public sealed unsafe class IrohConnection : IDisposable
	{
		private const ulong ClosedPollMilliseconds = 1000;

		private readonly NativeHandle handle;

		internal IrohConnection(nint connection) { handle = new NativeHandle(connection); }

		public bool IsDisposed => handle.IsReleased;

		/// <summary>Names this connection inside the process.</summary>
		public string Id { get; } = Guid.NewGuid().ToString("N");

		private volatile bool closedByPeer;

		/// <summary>
		/// True when the peer or the network ended the connection, false while it is open or when this side closed it
		/// (disconnect, the end of the goal that held it).
		/// </summary>
		public bool ClosedByPeer => closedByPeer;

		/// <summary>Marks the connection as ended by the peer or the network, unless this side has closed it already.</summary>
		private void NoteEnded()
		{
			if (!handle.IsReleased) closedByPeer = true;
		}
		public const int ChunkBytes = 64 * 1024;

		/// <summary>
		/// One request/answer per stream. The request is a 4 byte big endian header length, the header, then
		/// the file bytes when there is a file. The peer may answer and stop reading (declined), so a failed
		/// write still tries to read the answer before the write error is reported.
		/// </summary>
		public byte[] Request(byte[] header, Stream? file, int chunkTimeoutMilliseconds, int answerTimeoutMilliseconds, int maxAnswerBytes)
		{
			using var stream = OpenStream();

			IrohException? writeError = null;
			try
			{
				byte[] length = [(byte)((header.Length >> 24) & 0xFF), (byte)((header.Length >> 16) & 0xFF), (byte)((header.Length >> 8) & 0xFF), (byte)(header.Length & 0xFF)];
				stream.Write(length, length.Length, chunkTimeoutMilliseconds);
				stream.Write(header, header.Length, chunkTimeoutMilliseconds);

				if (file != null)
				{
					byte[] buffer = new byte[ChunkBytes];
					int read;
					while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
					{
						stream.Write(buffer, read, chunkTimeoutMilliseconds);
					}
				}
			}
			catch (IrohException ex) { writeError = ex; }

			stream.Finish();

			try
			{
				return stream.ReadAll(answerTimeoutMilliseconds, maxAnswerBytes);
			}
			catch when (writeError != null)
			{
				throw writeError;
			}
		}

		private IrohStream OpenStream()
		{
			if (!handle.TryEnter(out nint pointer)) throw new IrohConnectionEndedException("The connection to the peer is closed");
			SendStream* send = iroh.send_stream_default();
			RecvStream* recv = iroh.recv_stream_default();
			EndpointResult open;
			try
			{
				Connection* conn = (Connection*)pointer;
				open = iroh.connection_open_bi(&conn, &send, &recv);
			}
			finally { handle.Exit(); }

			if (open != EndpointResult.ENDPOINT_RESULT_OK)
			{
				iroh.send_stream_free(send);
				iroh.recv_stream_free(recv);
				NoteEnded();
				throw new IrohConnectionEndedException("Could not open a stream to the peer: " + open);
			}
			return new IrohStream((nint)send, (nint)recv);
		}

		/// <summary>Blocks until the peer opens a stream. Null when the connection is closed.</summary>
		public IrohStream? AcceptStream()
		{
			if (!handle.TryEnter(out nint pointer)) return null;
			SendStream* send = iroh.send_stream_default();
			RecvStream* recv = iroh.recv_stream_default();
			EndpointResult result;
			try
			{
				Connection* conn = (Connection*)pointer;
				result = iroh.connection_accept_bi(&conn, &send, &recv);
			}
			finally { handle.Exit(); }

			if (result != EndpointResult.ENDPOINT_RESULT_OK)
			{
				iroh.send_stream_free(send);
				iroh.recv_stream_free(recv);
				NoteEnded();
				return null;
			}
			return new IrohStream((nint)send, (nint)recv);
		}

		/// <summary>
		/// Hands every stream the peer opens to onStream on the thread pool until the connection closes, then frees the
		/// connection. Each stream is disposed when its onStream ends. Blocks the calling thread.
		/// </summary>
		public void ServeStreams(Func<IrohStream, Task> onStream, Action<Exception>? onFailure = null)
		{
			try
			{
				while (true)
				{
					IrohStream? stream = AcceptStream();
					if (stream == null) return;

					var accepted = stream;
					_ = Task.Run(() => onStream(accepted)).ContinueWith(handled =>
					{
						if (handled.Exception != null) onFailure?.Invoke(handled.Exception.GetBaseException());
						accepted.Dispose();
					}, TaskScheduler.Default);
				}
			}
			finally { Dispose(); }
		}

		/// <summary>
		/// Blocks until the connection is closed by either side. Waits in short native calls, so a Dispose from another
		/// thread closes the connection within about a second.
		/// </summary>
		public void WaitUntilClosed()
		{
			while (handle.TryEnter(out nint pointer))
			{
				EndpointResult result;
				Vec_uint8 datagram = iroh.rust_buffer_alloc(0);
				try
				{
					Connection* conn = (Connection*)pointer;
					result = iroh.connection_read_datagram_timeout(&conn, &datagram, ClosedPollMilliseconds);
				}
				finally
				{
					iroh.rust_buffer_free(datagram);
					handle.Exit();
				}
				if (result != EndpointResult.ENDPOINT_RESULT_OK && result != EndpointResult.ENDPOINT_RESULT_TIMEOUT)
				{
					NoteEnded();
					return;
				}
			}
		}

		/// <summary>
		/// Closes the connection and frees it. A call running on another thread keeps it until that call returns.
		/// </summary>
		public void Dispose()
		{
			handle.Release(pointer => iroh.connection_close((Connection*)pointer));
		}
	}

	/// <summary>
	/// One bi directional stream. Used by one thread at a time; Dispose frees what is still held.
	/// </summary>
	public sealed unsafe class IrohStream : IDisposable
	{
		private nint send;
		private nint recv;

		internal IrohStream(nint send, nint recv) { this.send = send; this.recv = recv; }

		/// <summary>Reads the stream to its end, at most maxBytes. A timeout or a broken stream throws.</summary>
		public byte[] ReadAll(int timeoutMilliseconds, int maxBytes)
		{
			RecvStream* stream = Recv();
			Vec_uint8 buffer = iroh.rust_buffer_alloc(0);
			try
			{
				var result = iroh.recv_stream_read_to_end_timeout(&stream, &buffer, (UIntPtr)maxBytes, (ulong)timeoutMilliseconds);
				if (result == EndpointResult.ENDPOINT_RESULT_TIMEOUT) throw new IrohException("The peer did not answer in time");
				if (result != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not read from the peer: " + result);
				byte[] bytes = new byte[(int)buffer.len];
				Marshal.Copy((IntPtr)buffer.ptr, bytes, 0, bytes.Length);
				return bytes;
			}
			finally { iroh.rust_buffer_free(buffer); }
		}

		/// <summary>Reads up to count bytes. 0 means the peer finished the stream. A timeout or a broken stream throws.</summary>
		public int Read(byte[] buffer, int count, int timeoutMilliseconds)
		{
			RecvStream* stream = Recv();
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

		public void Write(byte[] bytes, int count, int timeoutMilliseconds)
		{
			SendStream* stream = Send();
			fixed (byte* ptr = bytes)
			{
				var written = iroh.send_stream_write_timeout(&stream, new slice_ref_uint8 { ptr = ptr, len = (UIntPtr)count }, (ulong)timeoutMilliseconds);
				if (written == EndpointResult.ENDPOINT_RESULT_TIMEOUT) throw new IrohException("The peer stopped taking data");
				if (written != EndpointResult.ENDPOINT_RESULT_OK) throw new IrohException("Could not send to the peer: " + written);
			}
		}

		/// <summary>Ends the sending side. send_stream_finish consumes the send stream, so it is no longer held.</summary>
		public void Finish()
		{
			nint stream = Interlocked.Exchange(ref send, 0);
			if (stream == 0) return;
			iroh.send_stream_finish((SendStream*)stream);
		}

		public void WriteAndFinish(byte[] bytes)
		{
			SendStream* stream = Send();
			fixed (byte* ptr = bytes)
			{
				iroh.send_stream_write(&stream, new slice_ref_uint8 { ptr = ptr, len = (UIntPtr)bytes.Length });
			}
			Finish();
		}

		private SendStream* Send()
		{
			nint stream = send;
			if (stream == 0) throw new IrohException("The stream is finished");
			return (SendStream*)stream;
		}

		private RecvStream* Recv()
		{
			nint stream = recv;
			if (stream == 0) throw new IrohException("The stream is closed");
			return (RecvStream*)stream;
		}

		public void Dispose()
		{
			nint sendStream = Interlocked.Exchange(ref send, 0);
			if (sendStream != 0) iroh.send_stream_free((SendStream*)sendStream);
			nint recvStream = Interlocked.Exchange(ref recv, 0);
			if (recvStream != 0) iroh.recv_stream_free((RecvStream*)recvStream);
		}
	}
}
