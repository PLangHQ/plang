using app.filesystem;
using app.filesystem.Default;
using System.IO.Abstractions;

namespace app.filesystem
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
		void ClearFileAccess();
		bool IsOsRooted(string path);
		bool IsPlangRooted(string? path);
		void SetFileAccess(List<FileAccessControl> fileAccesses);
		public string ValidatePath(string? path);
	}

	



}
