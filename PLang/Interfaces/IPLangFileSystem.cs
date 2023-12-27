using Microsoft.Extensions.Logging;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using System.IO.Abstractions;

namespace PLang.Interfaces
{
	public interface IPLangFileSystem : IFileSystem
	{
		public string RootDirectory { get; }
		public bool IsRootApp { get; }
		public string RelativeAppPath { get; }

		void SetFileAccess(List<FileAccessControl> fileAccesses);
		public string? ValidatePath(string? path);
	}

	



}
