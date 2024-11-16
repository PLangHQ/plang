namespace Plang.Channels
{
	/// <summary>
	/// Defines a unified interface for communication channels that support reading, writing,
	/// asking questions, and sending notifications.
	/// </summary>
	public interface IChannel : IDisposable
	{
		/// <summary>
		/// Gets a value indicating whether the channel supports reading.
		/// </summary>
		bool CanRead { get; }

		/// <summary>
		/// Gets a value indicating whether the channel supports writing.
		/// </summary>
		bool CanWrite { get; }

		/// <summary>
		/// Writes data asynchronously to the channel.
		/// </summary>
		/// <param name="data">The byte array containing data to write.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		Task WriteAsync(byte[] data, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes data asynchronously to the channel from a stream and immediately flushes it.
		/// </summary>
		/// <param name="dataStream">The stream containing data to write.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		Task WriteAsync(Stream dataStream, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes data asynchronously to the channel without flushing.
		/// </summary>
		/// <param name="data">The byte array containing data to write.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		Task WriteToBufferAsync(byte[] data, CancellationToken cancellationToken = default);


		/// <summary>
		/// Reads data asynchronously from the channel.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous read operation. The value of the TResult parameter contains the read bytes.</returns>
		Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads data asynchronously from the channel as a stream.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task representing the asynchronous read operation. The value of the TResult parameter contains the stream.</returns>
		Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Asks a question to the user and returns the answer.
		/// </summary>
		/// <param name="question">The question to ask.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous ask operation. The value of the TResult parameter contains the user's answer.</returns>
		Task<string> AskAsync(string question, CancellationToken cancellationToken = default);

		/// <summary>
		/// Sends a notification message to the user.
		/// </summary>
		/// <param name="message">The notification message.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		Task NotifyAsync(string message, CancellationToken cancellationToken = default);
	}

}
