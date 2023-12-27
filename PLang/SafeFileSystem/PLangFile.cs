using Microsoft.Win32.SafeHandles;
using PLang.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text;

namespace PLang.SafeFileSystem
{

	public sealed class PLangFile : FileWrapper, IFile
	{
		private string rootPath;
		private readonly IPLangFileSystem fileSystem;

		public IFileSystem FileSystem => fileSystem;

		public PLangFile(IPLangFileSystem fileSystem) : base(fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		public override void AppendAllLines(string path, IEnumerable<string> contents)
		{
			path = fileSystem.ValidatePath(path);
			base.AppendAllLines(path, contents);
		}

		public override void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			base.AppendAllLines(path, contents, encoding);
		}

		public override Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.AppendAllLinesAsync(path, contents, cancellationToken);
		}

		public override Task AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.AppendAllLinesAsync(path, contents, encoding, cancellationToken);
		}

		public override void AppendAllText(string path, string? contents)
		{
			path = fileSystem.ValidatePath(path);
			base.AppendAllText(path, contents);
		}

		public override void AppendAllText(string path, string? contents, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			base.AppendAllText(path, contents, encoding);
		}

		public override Task AppendAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.AppendAllTextAsync(path, contents, cancellationToken);
		}

		public override Task AppendAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.AppendAllTextAsync(path, contents, encoding, cancellationToken);
		}

		public override StreamWriter AppendText(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.AppendText(path);
		}

		public override void Copy(string sourceFileName, string destFileName)
		{
			sourceFileName = fileSystem.ValidatePath(sourceFileName);
			destFileName = fileSystem.ValidatePath(destFileName);
			base.Copy(sourceFileName, destFileName);
		}

		public override void Copy(string sourceFileName, string destFileName, bool overwrite)
		{
			sourceFileName = fileSystem.ValidatePath(sourceFileName);
			destFileName = fileSystem.ValidatePath(destFileName);
			base.Copy(sourceFileName, destFileName, overwrite);
		}

		public override FileSystemStream Create(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.Create(path);
		}

		public override FileSystemStream Create(string path, int bufferSize)
		{
			path = fileSystem.ValidatePath(path);
			return base.Create(path, bufferSize);
		}

		public override FileSystemStream Create(string path, int bufferSize, FileOptions options)
		{
			path = fileSystem.ValidatePath(path);
			return base.Create(path, bufferSize, options);
		}

		public override IFileSystemInfo CreateSymbolicLink(string path, string pathToTarget)
		{
			path = fileSystem.ValidatePath(path);
			pathToTarget = fileSystem.ValidatePath(pathToTarget);
			return base.CreateSymbolicLink(path, pathToTarget);
		}

		public override StreamWriter CreateText(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.CreateText(path);
		}

		public override void Decrypt(string path)
		{
			path = fileSystem.ValidatePath(path);
			base.Decrypt(path);
		}

		public override void Delete(string path)
		{
			path = fileSystem.ValidatePath(path);
			base.Delete(path);
		}

		public override void Encrypt(string path)
		{
			path = fileSystem.ValidatePath(path);
			base.Encrypt(path);
		}

		public override bool Exists([NotNullWhen(true)] string? path)
		{
			path = fileSystem.ValidatePath(path);
			return base.Exists(path);
		}

		public override FileAttributes GetAttributes(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetAttributes(path);
		}

		public override FileAttributes GetAttributes(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override DateTime GetCreationTime(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetCreationTime(path);
		}

		public override DateTime GetCreationTime(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override DateTime GetCreationTimeUtc(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetCreationTimeUtc(path);
		}

		public override DateTime GetCreationTimeUtc(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override DateTime GetLastAccessTime(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetLastAccessTime(path);
		}

		public override DateTime GetLastAccessTime(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override DateTime GetLastAccessTimeUtc(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetLastAccessTimeUtc(path);
		}

		public override DateTime GetLastAccessTimeUtc(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override DateTime GetLastWriteTime(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetLastWriteTime(path);
		}

		public override DateTime GetLastWriteTime(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override DateTime GetLastWriteTimeUtc(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetLastWriteTimeUtc(path);
		}

		public override DateTime GetLastWriteTimeUtc(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override UnixFileMode GetUnixFileMode(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.GetUnixFileMode(path);
		}

		public override UnixFileMode GetUnixFileMode(SafeFileHandle fileHandle)
		{
			throw new NotImplementedException();
		}

		public override void Move(string sourceFileName, string destFileName)
		{
			sourceFileName = fileSystem.ValidatePath(sourceFileName);
			destFileName = fileSystem.ValidatePath(destFileName);
			base.Move(sourceFileName, destFileName);
		}

		public override void Move(string sourceFileName, string destFileName, bool overwrite)
		{
			sourceFileName = fileSystem.ValidatePath(sourceFileName);
			destFileName = fileSystem.ValidatePath(destFileName);
			base.Move(sourceFileName, destFileName, overwrite);
		}

		public override FileSystemStream Open(string path, FileMode mode)
		{
			path = fileSystem.ValidatePath(path);
			return base.Open(path, mode);
		}

		public override FileSystemStream Open(string path, FileMode mode, FileAccess access)
		{
			path = fileSystem.ValidatePath(path);
			return base.Open(path, mode, access);
		}

		public override FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share)
		{
			path = fileSystem.ValidatePath(path);
			return base.Open(path, mode, access, share);
		}

		public override FileSystemStream Open(string path, FileStreamOptions options)
		{
			path = fileSystem.ValidatePath(path);
			return base.Open(path, options);
		}

		public override FileSystemStream OpenRead(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.OpenRead(path);
		}

		public override StreamReader OpenText(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.OpenText(path);
		}

		public override FileSystemStream OpenWrite(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.OpenWrite(path);
		}

		public override byte[] ReadAllBytes(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllBytes(path);
		}

		public override Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllBytesAsync(path, cancellationToken);
		}

		public override string[] ReadAllLines(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllLines(path);
		}

		public override string[] ReadAllLines(string path, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllLines(path, encoding);
		}

		public override Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllLinesAsync(path, cancellationToken);
		}

		public override Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllLinesAsync(path, encoding, cancellationToken);
		}

		public override string ReadAllText(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllText(path);
		}

		public override string ReadAllText(string path, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllText(path, encoding);
		}

		public override Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllTextAsync(path, cancellationToken);
		}

		public override Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadAllTextAsync(path, encoding, cancellationToken);
		}

		public override IEnumerable<string> ReadLines(string path)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadLines(path);
		}

		public override IEnumerable<string> ReadLines(string path, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadLines(path, encoding);
		}

		public override IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadLinesAsync(path, cancellationToken);
		}

		public override IAsyncEnumerable<string> ReadLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.ReadLinesAsync(path, encoding, cancellationToken);
		}

		public override void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName)
		{
			sourceFileName = fileSystem.ValidatePath(sourceFileName);
			destinationFileName = fileSystem.ValidatePath(destinationFileName);
			destinationBackupFileName = fileSystem.ValidatePath(destinationBackupFileName);
			base.Replace(sourceFileName, destinationFileName, destinationBackupFileName);
		}

		public override void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName, bool ignoreMetadataErrors)
		{
			sourceFileName = fileSystem.ValidatePath(sourceFileName);
			destinationFileName = fileSystem.ValidatePath(destinationFileName);
			destinationBackupFileName = fileSystem.ValidatePath(destinationBackupFileName);
			base.Replace(sourceFileName, destinationFileName, destinationBackupFileName, ignoreMetadataErrors);
		}

		public override IFileSystemInfo? ResolveLinkTarget(string linkPath, bool returnFinalTarget)
		{
			linkPath = fileSystem.ValidatePath(linkPath);
			return base.ResolveLinkTarget(linkPath, returnFinalTarget);
		}

		public override void SetAttributes(string path, FileAttributes fileAttributes)
		{
			path = fileSystem.ValidatePath(path);
			base.SetAttributes(path, fileAttributes);
		}

		public override void SetAttributes(SafeFileHandle fileHandle, FileAttributes fileAttributes)
		{
			throw new NotImplementedException();
		}

		public override void SetCreationTime(string path, DateTime creationTime)
		{
			path = fileSystem.ValidatePath(path);
			base.SetCreationTime(path, creationTime);
		}

		public override void SetCreationTime(SafeFileHandle fileHandle, DateTime creationTime)
		{
			throw new NotImplementedException();
		}

		public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
		{
			path = fileSystem.ValidatePath(path);
			base.SetCreationTimeUtc(path, creationTimeUtc);
		}

		public override void SetCreationTimeUtc(SafeFileHandle fileHandle, DateTime creationTimeUtc)
		{
			throw new NotImplementedException();
		}

		public override void SetLastAccessTime(string path, DateTime lastAccessTime)
		{
			path = fileSystem.ValidatePath(path);
			base.SetLastAccessTime(path, lastAccessTime);
		}

		public override void SetLastAccessTime(SafeFileHandle fileHandle, DateTime lastAccessTime)
		{
			throw new NotImplementedException();
		}

		public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
		{
			path = fileSystem.ValidatePath(path);
			base.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
		}

		public override void SetLastAccessTimeUtc(SafeFileHandle fileHandle, DateTime lastAccessTimeUtc)
		{
			throw new NotImplementedException();
		}

		public override void SetLastWriteTime(string path, DateTime lastWriteTime)
		{
			path = fileSystem.ValidatePath(path);
			base.SetLastWriteTime(path, lastWriteTime);
		}

		public override void SetLastWriteTime(SafeFileHandle fileHandle, DateTime lastWriteTime)
		{
			throw new NotImplementedException();
		}

		public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
		{
			path = fileSystem.ValidatePath(path);
			base.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
		}

		public override void SetLastWriteTimeUtc(SafeFileHandle fileHandle, DateTime lastWriteTimeUtc)
		{
			throw new NotImplementedException();
		}

		public override void SetUnixFileMode(string path, UnixFileMode mode)
		{
			path = fileSystem.ValidatePath(path);
			base.SetUnixFileMode(path, mode);
		}

		public override void SetUnixFileMode(SafeFileHandle fileHandle, UnixFileMode mode)
		{
			throw new NotImplementedException();
		}

		public override void WriteAllBytes(string path, byte[] bytes)
		{
			path = fileSystem.ValidatePath(path);
			base.WriteAllBytes(path, bytes);
		}

		public override Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.WriteAllBytesAsync(path, bytes, cancellationToken);
		}

		public override void WriteAllLines(string path, string[] contents)
		{

			path = fileSystem.ValidatePath(path);
			base.WriteAllLines(path, contents);
		}

		public override void WriteAllLines(string path, IEnumerable<string> contents)
		{
			path = fileSystem.ValidatePath(path);
			base.WriteAllLines(path, contents);
		}

		public override void WriteAllLines(string path, string[] contents, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			base.WriteAllLines(path, contents, encoding);
		}

		public override void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			base.WriteAllLines(path, contents, encoding);
		}

		public override Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.WriteAllLinesAsync(path, contents, cancellationToken);
		}

		public override Task WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.WriteAllLinesAsync(path, contents, encoding, cancellationToken);
		}

		public override void WriteAllText(string path, string? contents)
		{
			path = fileSystem.ValidatePath(path);
			base.WriteAllText(path, contents);
		}

		public override void WriteAllText(string path, string? contents, Encoding encoding)
		{
			path = fileSystem.ValidatePath(path);
			base.WriteAllText(path, contents, encoding);
		}

		public override Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.WriteAllTextAsync(path, contents, cancellationToken);
		}

		public override Task WriteAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default)
		{
			path = fileSystem.ValidatePath(path);
			return base.WriteAllTextAsync(path, contents, encoding, cancellationToken);
		}
	}
}
