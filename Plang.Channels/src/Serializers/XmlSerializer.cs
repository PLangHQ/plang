using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plang.Channels.Serializers
{
	/// <summary>
	/// An XML serializer that implements the <see cref="ISerializer"/> interface
	/// using System.Xml.Serialization.
	/// </summary>
	public class XmlSerializer : ISerializer
	{
		private readonly Encoding _encoding;

		/// <summary>
		/// Initializes a new instance of the <see cref="XmlSerializer"/> class with optional settings.
		/// </summary>
		/// <param name="encoding">The text encoding to use for serialization.</param>
		public XmlSerializer(Encoding encoding = null)
		{
			_encoding = encoding ?? Encoding.UTF8;
		}

		/// <summary>
		/// Gets the content type associated with the serializer.
		/// </summary>
		public string ContentType => "application/xml";

		/// <summary>
		/// Serializes an object of type <typeparamref name="T"/> to a byte array asynchronously.
		/// </summary>
		public Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
		{
			if (obj == null) throw new ArgumentNullException(nameof(obj));

			var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(T));

			using var memoryStream = new MemoryStream();
			using var streamWriter = new StreamWriter(memoryStream, _encoding);

			xmlSerializer.Serialize(streamWriter, obj);

			byte[] data = memoryStream.ToArray();
			return Task.FromResult(data);
		}

		/// <summary>
		/// Deserializes a byte array to an object of type <typeparamref name="T"/> asynchronously.
		/// </summary>
		public Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
		{
			if (data == null) throw new ArgumentNullException(nameof(data));

			var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(T));

			using var memoryStream = new MemoryStream(data);
			using var streamReader = new StreamReader(memoryStream, _encoding);

			T obj = (T)xmlSerializer.Deserialize(streamReader);
			return Task.FromResult(obj);
		}
	}
}
