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
		string SystemDirectory { get; }
		string OsDirectory { get; }
		string Id { get; init; }

		void AddFileAccess(FileAccessControl fileAccess);
		void ClearFileAccess();
		/// <summary>The grants that last as long as this file system's process (ProcessId == Id), e.g. from `give access to`.</summary>
		List<FileAccessControl> GetProcessFileAccess();
		bool IsOsRooted(string path);
		bool IsPlangRooted(string? path);
		void SetFileAccess(List<FileAccessControl> fileAccesses);
		public string ValidatePath(string? path);
	}

	



}
