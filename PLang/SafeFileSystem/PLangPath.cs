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
	}

		public sealed class PLangPath : PathWrapper, IPath
	{
		private PLangFileSystem fileSystem;
		public PLangPath(PLangFileSystem fileSystem) : base(fileSystem) {
			this.fileSystem = fileSystem;
		}
		public override bool Exists([NotNullWhen(true)] string? path)
		{
			path = fileSystem.ValidatePath(path);
			return base.Exists(path);
		}
	}
}
