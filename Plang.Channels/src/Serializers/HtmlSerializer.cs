using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plang.Channels.Serializers
{
	/// <summary>
	/// An HTML serializer that inherits from <see cref="TextSerializer"/> but uses "text/html" as the content type.
	/// </summary>
	public class HtmlSerializer : TextSerializer
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HtmlSerializer"/> class with optional settings.
		/// </summary>
		/// <param name="encoding">The text encoding to use for serialization.</param>
		public HtmlSerializer(Encoding encoding = null)
			: base(encoding)
		{
		}

		/// <summary>
		/// Gets the content type associated with the serializer.
		/// </summary>
		public override string ContentType => "text/html";
	}
}
