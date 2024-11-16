namespace Plang.Channels
{
	/// <summary>
	/// Represents a client that communicates through a specified communication channel,
	/// using a specified serializer for object serialization and deserialization.
	/// </summary>
	public class ChannelClient : IDisposable
	{
		private readonly IChannel _channel;
		private readonly ISerializer _serializer;

		/// <summary>
		/// Initializes a new instance of the <see cref="ChannelClient"/> class.
		/// </summary>
		/// <param name="channel">The communication channel to use.</param>
		/// <param name="serializer">The serializer to use for object serialization.</param>
		public ChannelClient(IChannel channel, ISerializer serializer)
		{
			_channel = channel ?? throw new ArgumentNullException(nameof(channel));
			_serializer = serializer; // Serializer can be null if not needed
		}

		public bool CanRead => _channel.CanRead;
		public bool CanWrite => _channel.CanWrite;

		/// <summary>
		/// Writes an object asynchronously to the communication channel and flushes it.
		/// </summary>
		public async Task WriteAsync(object obj, CancellationToken cancellationToken = default)
		{
			if (obj == null) throw new ArgumentNullException(nameof(obj));
			if (_serializer == null) throw new InvalidOperationException("Serializer is not set.");

			byte[] data = await _serializer.SerializeAsync(obj, cancellationToken);
			await _channel.WriteAsync(data, cancellationToken);
		}

		/// <summary>
		/// Writes an object asynchronously to the communication channel without flushing.
		/// </summary>
		public async Task WriteToBufferAsync(object obj, CancellationToken cancellationToken = default)
		{
			if (obj == null) throw new ArgumentNullException(nameof(obj));
			if (_serializer == null) throw new InvalidOperationException("Serializer is not set.");

			byte[] data = await _serializer.SerializeAsync(obj, cancellationToken);
			await _channel.WriteToBufferAsync(data, cancellationToken);
		}

		/// <summary>
		/// Asks a question to the user through the communication channel and awaits an answer.
		/// </summary>
		public Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
		{
			return _channel.AskAsync(question, cancellationToken);
		}

		/// <summary>
		/// Sends a notification message to the user through the communication channel.
		/// </summary>
		public Task NotifyAsync(string message, CancellationToken cancellationToken = default)
		{
			return _channel.NotifyAsync(message, cancellationToken);
		}

		/// <summary>
		/// Releases all resources used by the <see cref="ChannelClient"/>.
		/// </summary>
		public void Dispose()
		{
			_channel.Dispose();
		}
	}
}
