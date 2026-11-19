using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;

namespace PLang.SafeFileSystem
{
	public sealed class Path : PathWrapper, IPath
	{
		private PLangFileSystem fileSystem;
		public Path(PLangFileSystem fileSystem) : base(fileSystem)
		{
			this.fileSystem = fileSystem;
		}
		public override bool Exists([NotNullWhen(true)] string? path)
		{
			path = fileSystem.ValidatePath(path);
			return base.Exists(path);
		}

		public override string? GetDirectoryName(string? path) => PathOs.KeepDriveRoot(path, base.GetDirectoryName(path));
	}

	/// <summary>
	/// A path outside the app is written //folder/file on Linux, and a single / is the app's root.
	/// Path.GetDirectoryName turns //folder/file into /folder, which would be taken for a folder in the app.
	/// </summary>
	internal static class PathOs
	{
		public static string? KeepDriveRoot(string? path, string? directory)
		{
			if (directory == null || path == null || OperatingSystem.IsWindows()) return directory;
			if (path.StartsWith("//") && !directory.StartsWith("//") && directory.StartsWith("/")) return "/" + directory;
			return directory;
		}
	}

	public sealed class PLangPath : PathWrapper, IPath
	{
		private PLangFileSystem fileSystem;
		public PLangPath(PLangFileSystem fileSystem) : base(fileSystem)
		{
			this.fileSystem = fileSystem;
		}
		public override bool Exists([NotNullWhen(true)] string? path)
		{
			path = fileSystem.ValidatePath(path);
			return base.Exists(path);
		}

		public override string? GetDirectoryName(string? path) => PathOs.KeepDriveRoot(path, base.GetDirectoryName(path));
	}
}
