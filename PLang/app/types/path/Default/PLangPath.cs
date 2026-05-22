using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;

namespace app.types.path.Default
{
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
	}
}
