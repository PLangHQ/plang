using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plang.Channels.Clients
{
	/// <summary>
	/// A communication channel that interacts with the console for input and output operations.
	/// </summary>
	public class ConsoleChannel : IChannel
	{
		private readonly Encoding _encoding;

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleChannel"/> class.
		/// </summary>
		/// <param name="encoding">The text encoding to use for console input and output.</param>
		public ConsoleChannel(Encoding? encoding = null)
		{
			_encoding = encoding ?? Console.OutputEncoding;
		}

		public bool CanRead => true;
		public bool CanWrite => true;

		/// <summary>
		/// Writes data asynchronously to the console and flushes it.
		/// </summary>
		public Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
		{
			string text = _encoding.GetString(data);
			Console.WriteLine(text);
			return Task.CompletedTask; // Console writes are immediate
		}

		/// <summary>
		/// Writes data asynchronously to an internal buffer without flushing.
		/// </summary>
		public Task WriteToBufferAsync(byte[] data, CancellationToken cancellationToken = default)
		{
			string text = _encoding.GetString(data);
			Console.Write(text);
			return Task.CompletedTask;
		}



		/// <summary>
		/// Reads data asynchronously from the console.
		/// </summary>
		public Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
		{
			string? line = Console.ReadLine();
			byte[] data = _encoding.GetBytes(line + Environment.NewLine);
			return Task.FromResult(data);
		}

		/// <summary>
		/// Asks a question to the user via the console and awaits an answer.
		/// </summary>
		public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
		{
			Console.WriteLine(question);
			return await Task.Run(() => Console.ReadLine(), cancellationToken);
		}

		/// <summary>
		/// Sends a notification message to the user via the console.
		/// </summary>
		public Task NotifyAsync(string message, CancellationToken cancellationToken = default)
		{
			Console.WriteLine($"Notification: {message}");
			return Task.CompletedTask;
		}

		/// <summary>
		/// Writes data asynchronously to the console from a stream and flushes it.
		/// </summary>
		public async Task WriteAsync(Stream dataStream, CancellationToken cancellationToken = default)
		{
			using var reader = new StreamReader(dataStream, _encoding);
			string line;
			while ((line = await reader.ReadLineAsync()) != null)
			{
				Console.WriteLine(line);
				cancellationToken.ThrowIfCancellationRequested();
			}
		}

		/// <summary>
		/// Reads data asynchronously from the console as a stream.
		/// </summary>
		public Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException("Chunked read is not supported for ConsoleCommunicationChannel.");
		}

		/// <summary>
		/// Releases all resources used by the <see cref="ConsoleChannel"/>.
		/// </summary>
		public void Dispose()
		{
		}
	}
}
