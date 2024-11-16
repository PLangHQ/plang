using System.Text.Json;

namespace Plang.Channels.Serializers
{
	/// <summary>
	/// A JSON serializer that implements the <see cref="ISerializer"/> interface.
	/// </summary>
	public class JsonSerializer : ISerializer
	{
		private readonly JsonSerializerOptions _options;

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonSerializer"/> class.
		/// </summary>
		/// <param name="options">Optional JSON serialization options.</param>
		public JsonSerializer(JsonSerializerOptions options = null)
		{
			_options = options ?? new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			};
		}

		/// <summary>
		/// Gets the content type associated with the serializer.
		/// </summary>
		public string ContentType => "application/json";

		/// <summary>
		/// Serializes an object to a byte array asynchronously.
		/// </summary>
		public Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
		{
			byte[] data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj, _options);
			return Task.FromResult(data);
		}

		/// <summary>
		/// Deserializes a byte array to an object of type T asynchronously.
		/// </summary>
		public Task<T?> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
		{
			T? obj = System.Text.Json.JsonSerializer.Deserialize<T>(data, _options);
			return Task.FromResult(obj);
		}
	}
}
