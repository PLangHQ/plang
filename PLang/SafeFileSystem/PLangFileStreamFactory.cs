using Microsoft.Win32.SafeHandles;
using PLang.Interfaces;
using System.IO.Abstractions;


namespace PLang.SafeFileSystem
{
	[Serializable]
	public sealed class PLangFileStreamFactory : IFileStreamFactory
	{
		IPLangFileSystem fileSystem;
		public PLangFileStreamFactory(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		/// <inheritdoc />
		public IFileSystem FileSystem { get { return fileSystem; } }

		[Obsolete("Use `IFileStreamFactory.New(string, FileMode)` instead.")]
		public Stream Create(string path, FileMode mode)
		{
			path = fileSystem.ValidatePath(path);
			return New(path, mode);
		}
		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(string, FileMode, FileAccess)` instead.")]
		public Stream Create(string path, FileMode mode, FileAccess access)
		{
			path = fileSystem.ValidatePath(path);
			return New(path, mode, access);
		}
		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(string, FileMode, FileAccess, FileShare)` instead.")]
		public Stream Create(string path, FileMode mode, FileAccess access, FileShare share)
		{
			path = fileSystem.ValidatePath(path);
			return New(path, mode, access, share);
		}
		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(string, FileMode, FileAccess, FileShare, int)` instead.")]
		public Stream Create(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
		{
			path = fileSystem.ValidatePath(path);
			return New(path, mode, access, share, bufferSize);
		}
		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(string, FileMode, FileAccess, FileShare, int, FileOptions)` instead.")]
		public Stream Create(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options)
		{
			path = fileSystem.ValidatePath(path);
			return New(path, mode, access, share, bufferSize, options);
		}
		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(string, FileMode, FileAccess, FileShare, int, bool)` instead.")]
		public Stream Create(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync)
		{
			path = fileSystem.ValidatePath(path);
			return New(path, mode, access, share, bufferSize, useAsync);
		}
		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(SafeFileHandle, FileAccess)` instead.")]
		public Stream Create(SafeFileHandle handle, FileAccess access)
			=> New(handle, access);

		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(SafeFileHandle, FileAccess, int)` instead.")]
		public Stream Create(SafeFileHandle handle, FileAccess access, int bufferSize)
			=> New(handle, access, bufferSize);

		/// <inheritdoc />
		[Obsolete("Use `IFileStreamFactory.New(SafeFileHandle, FileAccess, int, bool)` instead.")]
		public Stream Create(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync)
			=> New(handle, access, bufferSize, isAsync);

		[Obsolete("This method has been deprecated. Please use new Create(SafeFileHandle handle, FileAccess access) instead. http://go.microsoft.com/fwlink/?linkid=14202")]
		public Stream Create(IntPtr handle, FileAccess access)
			=> new FileStream(handle, access);

		[Obsolete("This method has been deprecated. Please use new Create(SafeFileHandle handle, FileAccess access) instead, and optionally make a new SafeFileHandle with ownsHandle=false if needed. http://go.microsoft.com/fwlink/?linkid=14202")]
		public Stream Create(IntPtr handle, FileAccess access, bool ownsHandle)
			=> new FileStream(handle, access, ownsHandle);

		[Obsolete("This method has been deprecated. Please use new Create(SafeFileHandle handle, FileAccess access, int bufferSize) instead, and optionally make a new SafeFileHandle with ownsHandle=false if needed. http://go.microsoft.com/fwlink/?linkid=14202")]
		public Stream Create(IntPtr handle, FileAccess access, bool ownsHandle, int bufferSize)
			=> new FileStream(handle, access, ownsHandle, bufferSize);

		[Obsolete("This method has been deprecated. Please use new Create(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) instead, and optionally make a new SafeFileHandle with ownsHandle=false if needed. http://go.microsoft.com/fwlink/?linkid=14202")]
		public Stream Create(IntPtr handle, FileAccess access, bool ownsHandle, int bufferSize, bool isAsync)
			=> new FileStream(handle, access, ownsHandle, bufferSize, isAsync);

		/// <inheritdoc />
		public FileSystemStream New(SafeFileHandle handle, FileAccess access)
			=> new PLangFileStreamWrapper(new FileStream(handle, access));

		/// <inheritdoc />
		public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize)
			=> new PLangFileStreamWrapper(new FileStream(handle, access, bufferSize));

		/// <inheritdoc />
		public FileSystemStream New(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync)
			=> new PLangFileStreamWrapper(new FileStream(handle, access, bufferSize, isAsync));


		/// <inheritdoc />
		public FileSystemStream New(string path, FileMode mode)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, mode));
		}

		/// <inheritdoc />
		public FileSystemStream New(string path, FileMode mode, FileAccess access)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, mode, access));
		}
		/// <inheritdoc />
		public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, mode, access, share));
		}
		/// <inheritdoc />
		public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, mode, access, share, bufferSize));
		}
		/// <inheritdoc />
		public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, mode, access, share, bufferSize, useAsync));
		}
		/// <inheritdoc />
		public FileSystemStream New(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize,
			FileOptions options)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, mode, access, share, bufferSize, options));
		}

		public FileSystemStream New(string path, FileStreamOptions options)
		{
			path = fileSystem.ValidatePath(path);
			return new PLangFileStreamWrapper(new FileStream(path, options));
		}

#if FEATURE_FILESTREAM_OPTIONS
        /// <inheritdoc />
        public FileSystemStream New(string path, FileStreamOptions options)
            => new FileStreamWrapper(new FileStream(path, options));
#endif

		/// <inheritdoc />
		public FileSystemStream Wrap(FileStream fileStream)
			=> new PLangFileStreamWrapper(fileStream);
	}


}
