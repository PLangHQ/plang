using System.Text;

namespace PLang.Services.Transformers
{


	public class HtmlTransformer : TextTransformer
	{

		public HtmlTransformer(Encoding encoding) : base(encoding) { }
		public override string ContentType { get { return "text/html"; } }
	}
}
