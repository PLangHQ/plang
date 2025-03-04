using PLang.Errors;
using PLang.SafeFileSystem;
using System.IO.Abstractions;

namespace PLang.Interfaces
{
	public interface IPLangFileSystem : IFileSystem
	{
		public string RootDirectory { get; }
		public bool IsRootApp { get; }
		public string RelativeAppPath { get; }
		public string SharedPath { get; }
		public string GoalsPath { get; }
		public string BuildPath { get; }
		public string DbPath { get; }
		string OsDirectory { get; }
		string Id { get; init; }

		void AddFileAccess(FileAccessControl fileAccess);
		bool IsPathRooted(string? path);
		void SetFileAccess(List<FileAccessControl> fileAccesses);
		public string ValidatePath(string? path);
	}

	



}
