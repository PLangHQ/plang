using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plang.Channels.Serializers
{
 /// <summary>
    /// A text serializer that implements the <see cref="ISerializer"/> interface
    /// for plain text serialization.
    /// </summary>
    public class TextSerializer : ISerializer
    {
        private readonly Encoding _encoding;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextSerializer"/> class with optional settings.
        /// </summary>
        /// <param name="encoding">The text encoding to use for serialization.</param>
        public TextSerializer(Encoding? encoding = null)
        {
            _encoding = encoding ?? Encoding.UTF8;
        }

		/// <summary>
		/// Gets the content type associated with the serializer.
		/// </summary>
		public virtual string ContentType => "text/plain";

		/// <summary>
		/// Serializes an object to a byte array asynchronously by converting it to a string.
		/// </summary>
		public Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
        {
            string text = obj?.ToString() ?? string.Empty;
            byte[] data = _encoding.GetBytes(text);
            return Task.FromResult(data);
        }

        /// <summary>
        /// Deserializes a byte array to an object of type <typeparamref name="T"/> asynchronously
        /// by converting it from a string.
        /// </summary>
        public Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
        {
            string text = _encoding.GetString(data);
            object result;

            if (typeof(T) == typeof(string))
            {
                result = text;
            }
            else
            {
                try
                {
                    result = Convert.ChangeType(text, typeof(T));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to deserialize text to type {typeof(T).Name}.", ex);
                }
            }

            return Task.FromResult((T)result);
        }
    }
}
