namespace PLang.Utils
{
	public class ExceptionHelper
	{

		public static Exception GetLowestException(Exception exception)
		{
			var ex = exception.InnerException;
			var lowestException = exception;
			while (ex != null) { 
				ex = ex.InnerException;
				if (ex != null) lowestException = ex;
			}

			return lowestException;
		}
	}
}
