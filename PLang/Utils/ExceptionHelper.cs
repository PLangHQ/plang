using System.Diagnostics.CodeAnalysis;
using static PLang.Utils.VariableHelper;

namespace PLang.Utils
{
	public class ExceptionHelper
	{

		public static Exception GetLowestException(Exception exception)
		{
			var ex = exception.InnerException;
			var lowestException = exception;
			while (ex != null)
			{
				ex = ex.InnerException;
				if (ex != null) lowestException = ex;
			}

			return lowestException;
		}

		[DoesNotReturn]
		public static void NotImplemented(string? message = null)
		{
			if (string.IsNullOrEmpty(message))
			{
				message = $"This has not been implement.";
			}

			if (!message.Contains("https://github.com/PLangHQ"))
			{
				message += " Come and help us out, you can implement it if you know C#. Check out https://github.com/PLangHQ/plang/blob/main/Documentation/PLangDevelopment.md";
			}

			throw new NotImplementedException(message);
		}
	}
}
