using PLang.Exceptions.AskUser;

namespace PLang.Exceptions
{
	public class FileAccessException : Exception
	{
		
		public FileAccessException(string appName, string path, string message) : base(message)
		{
			AppName = appName;
			Path = path;
		}

		public string AppName { get; }
		public string Path { get; }


	}

	
}
