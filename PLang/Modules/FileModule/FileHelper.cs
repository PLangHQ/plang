using System.Text;

namespace PLang.Modules.FileModule
{
	public class FileHelper
	{
		public static Encoding GetEncoding(string encoding)
		{
			switch (encoding)
			{
				case "utf-8":
				case "utf-16":
				case "utf-16BE":
				case "utf-32LE":
				case "us-ascii":
					return Encoding.GetEncoding(encoding);
			}

			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			if (int.TryParse(encoding, out int code))
			{
				return Encoding.GetEncoding(code);
			}
			return Encoding.GetEncoding(encoding);
		}
	}
}
