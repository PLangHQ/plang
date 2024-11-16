namespace Plang.Channels
{
	/// <summary>
	/// Defines a serializer interface for serializing and deserializing objects.
	/// </summary>
	public interface ISerializer
	{
		/// <summary>
		/// Gets the content type associated with the serializer (e.g., "application/json").
		/// </summary>
		string ContentType { get; }

		/// <summary>
		/// Serializes an object to a byte array asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of the object to serialize.</typeparam>
		/// <param name="obj">The object to serialize.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous serialization operation.</returns>
		Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default);

		/// <summary>
		/// Deserializes a byte array to an object of type T asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of the object to deserialize to.</typeparam>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task that represents the asynchronous deserialization operation.</returns>
		Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default);
	}
}
