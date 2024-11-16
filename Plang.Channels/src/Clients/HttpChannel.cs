using System.Net;
using System.Text;
using System.Text.Json;

namespace Plang.Channels.Clients
{
	/// <summary>
	/// A communication channel that interacts over HTTP using an <see cref="HttpListenerContext"/>.
	/// </summary>
	/// <summary>
	/// A communication channel that interacts over HTTP using an <see cref="HttpListenerContext"/>.
	/// </summary>
	public class HttpChannel : IChannel
	{
		private readonly HttpListenerContext _context;
		private readonly Stream _inputStream;
		private readonly Stream _outputStream;
		private readonly Encoding _encoding;
		private bool _disposed = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpChannel"/> class.
		/// </summary>
		/// <param name="context">The HTTP listener context.</param>
		/// <param name="encoding">The text encoding to use for HTTP communication.</param>
		public HttpChannel(HttpListenerContext context, Encoding encoding = null)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_inputStream = context.Request.InputStream;
			_outputStream = context.Response.OutputStream;
			_encoding = encoding ?? Encoding.UTF8;

			// Enable chunked transfer encoding
			_context.Response.SendChunked = true;
		}

		public bool CanRead => _inputStream.CanRead;
		public bool CanWrite => _outputStream.CanWrite;

		/// <summary>
		/// Writes data asynchronously to the HTTP response and flushes it.
		/// </summary>
		public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));

			await _outputStream.WriteAsync(data, 0, data.Length, cancellationToken);
			await _outputStream.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Writes data asynchronously to the HTTP response without flushing.
		/// </summary>
		public async Task WriteToBufferAsync(byte[] data, CancellationToken cancellationToken = default)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));

			await _outputStream.WriteAsync(data, 0, data.Length, cancellationToken);
			// Do not flush here
		}

		private async Task FlushBufferAsync(CancellationToken cancellationToken = default)
		{
			await _outputStream.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Reads data asynchronously from the HTTP request.
		/// </summary>
		public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
		{
			using var memoryStream = new MemoryStream();
			await _inputStream.CopyToAsync(memoryStream, cancellationToken);
			return memoryStream.ToArray();
		}

		/// <summary>
		/// Asks a question to the user via HTTP and awaits an asynchronous response.
		/// </summary>
		public Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
		{
			// Implementation as per previous discussions
			// Since HTTP cannot wait for a response, perhaps send the question and return null
			return Task.FromResult<string>(null);
		}

		/// <summary>
		/// Sends a notification message to the user via HTTP.
		/// </summary>
		public async Task NotifyAsync(string message, CancellationToken cancellationToken = default)
		{
			var notification = new
			{
				Type = "notification",
				Content = message
			};

			string json = JsonSerializer.Serialize(notification);
			byte[] data = _encoding.GetBytes(json);

			await WriteAsync(data, cancellationToken); // This will flush
		}

		/// <summary>
		/// Writes data asynchronously to the HTTP response from a stream and flushes it.
		/// </summary>
		public async Task WriteAsync(Stream dataStream, CancellationToken cancellationToken = default)
		{
			if (dataStream == null) throw new ArgumentNullException(nameof(dataStream));

			await dataStream.CopyToAsync(_outputStream, 81920, cancellationToken);
			await _outputStream.FlushAsync(cancellationToken);
		}

		/// <summary>
		/// Reads data asynchronously from the HTTP request as a stream.
		/// </summary>
		public Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken = default)
		{
			// Return the input stream for reading
			return Task.FromResult(_inputStream);
		}

		/// <summary>
		/// Releases all resources used by the <see cref="HttpChannel"/>.
		/// </summary>
		public void Dispose()
		{
			if (!_disposed)
			{
				// Flush any remaining data in buffer
				FlushBufferAsync().GetAwaiter().GetResult();

				_inputStream?.Dispose();
				_outputStream?.Dispose();
				_context.Response.Close();
				_disposed = true;
			}
		}
	}
}
