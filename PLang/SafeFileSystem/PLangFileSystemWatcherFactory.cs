using PLang.Interfaces;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.SafeFileSystem
{
	[Serializable]
	internal class PLangFileSystemWatcherFactory : FileSystemWatcherFactory
	{
		IPLangFileSystem fileSystem;
		///
		public PLangFileSystemWatcherFactory(IPLangFileSystem fileSystem) : base(fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		/// <inheritdoc />
		public IFileSystem FileSystem { get { return fileSystem; } }

		/// <inheritdoc />
		[Obsolete("Use `IFileSystemWatcherFactory.New()` instead")]
		public IFileSystemWatcher CreateNew()
			=> New();

		/// <inheritdoc />
		[Obsolete("Use `IFileSystemWatcherFactory.New(string)` instead")]
		public IFileSystemWatcher CreateNew(string path)
			=> New(path);

		/// <inheritdoc />
		[Obsolete("Use `IFileSystemWatcherFactory.New(string, string)` instead")]
		public IFileSystemWatcher CreateNew(string path, string filter)
			=> New(path, filter);

		/// <inheritdoc />
		public IFileSystemWatcher New()
			=> new FileSystemWatcherWrapper(FileSystem);

		/// <inheritdoc />
		public IFileSystemWatcher New(string path)
		{
			path = fileSystem.ValidatePath(path);
			return new FileSystemWatcherWrapper(FileSystem, path);
		}

		/// <inheritdoc />
		public IFileSystemWatcher New(string path, string filter)
		{
			path = fileSystem.ValidatePath(path);
			return new FileSystemWatcherWrapper(FileSystem, path, filter);
		}

	}


}
