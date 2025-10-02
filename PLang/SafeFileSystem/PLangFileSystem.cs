using Microsoft.CodeAnalysis.CSharp.Syntax;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Websocket.Client.Logging;

namespace PLang.SafeFileSystem
{

	public record FileAccessControl(string appName, string path, DateTime? expires = null, string? ProcessId = null);


	//[Serializable]
	public sealed class PLangFileSystem : FileSystem, IPLangFileSystem
	{
		public string Id { get; init; }

		private string rootPath;
		List<FileAccessControl> fileAccesses = new();

		public bool IsRootApp { get; private set; }
		public string RelativeAppPath { get; set; }
		public string SystemDirectory
		{
			get
			{
				return Path.Join(AppContext.BaseDirectory, "system");
			}
		}
		public string OsDirectory
		{
			get
			{
				return Path.Join(AppContext.BaseDirectory, "os");
			}
		}
		public string RootDirectory
		{
			get
			{
				return rootPath;
			}
			set { rootPath = value; }
		}

		public string SharedPath { get; private set; }
		public string GoalsPath { get; private set; }
		public string BuildPath { get; private set; }
		public string DbPath { get; private set; }

		// This SafeFileSystem namespace would need some good testing
		// for now it is simply proof of concept about access control
		// SetFileAccess is a security hole
		//
		// Very basic access control, could add Access type(read, write, del), status such as blocked
		// appName is weak validation, need to find new way

		/// <inheritdoc />
		public PLangFileSystem(string appStartupPath, string relativeAppPath)
		{
			this.rootPath = appStartupPath.AdjustPathToOs();
			this.RelativeAppPath = relativeAppPath.AdjustPathToOs();

			this.Id = Guid.NewGuid().ToString();

			

			DriveInfo = new PLangDriveInfoFactory(this);
			DirectoryInfo = new PLangDirectoryInfoFactory(this);
			FileInfo = new PLangFileInfoFactory(this);
			Path = new PLangPath(this);
			File = new PLangFile(this);
			Directory = new PLangDirectoryWrapper(this);
			FileStream = new PLangFileStreamFactory(this);
			FileSystemWatcher = new PLangFileSystemWatcherFactory(this);


			this.fileAccesses = new List<FileAccessControl>();
			fileAccesses.Add(new SafeFileSystem.FileAccessControl(appStartupPath, SystemDirectory, ProcessId: Id));

			this.IsRootApp = (relativeAppPath == Path.DirectorySeparatorChar.ToString());
			if (AppContext.GetData("sharedPath") != null)
			{
				this.SharedPath = AppContext.GetData("sharedPath")!.ToString()!;
			}
			else if (Environment.GetEnvironmentVariable("sharedpath") != null)
			{
				this.SharedPath = Environment.GetEnvironmentVariable("sharedpath")!;
			}
			else
			{
				this.SharedPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plang");
				if (string.IsNullOrEmpty(SharedPath) || SharedPath == "plang")
				{
					SharedPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "plang");
					if (string.IsNullOrEmpty(SharedPath) || SharedPath == "plang")
					{
						SharedPath = AppDomain.CurrentDomain.BaseDirectory;
					}
				}
			}
			this.GoalsPath = this.RootDirectory;
			this.BuildPath = Path.Join(this.GoalsPath, ".build");
			this.DbPath = Path.Join(this.GoalsPath, ".db");
			
		}

		// TODO: This is a security issue, here anybody can set what ever file access.
		// There is issue with stack overflow if ISettings is injected
		// so some other solution needs to be found.
		//
		// In the mean time, big security hole available for anybody to exploit
		//
		// the file access control should be store in the apps database and not in system.sql
		// the access should be signed by the root private key, with the uri, jwt key with expiration
		// it is stored in system.sql. Now everytime he wants to access uri he can prove he has permission

		public void SetFileAccess(List<FileAccessControl> fileAccesses)
		{
			var engineFileAccess = this.fileAccesses.FirstOrDefault(p => p.ProcessId is not null);

			this.fileAccesses = fileAccesses;
			if (engineFileAccess != null)
			{
				this.fileAccesses.Add(engineFileAccess);
			}
		}

		public void AddFileAccess(FileAccessControl fileAccess)
		{
			this.fileAccesses.Add(fileAccess);
		}

		public void ClearFileAccess()
		{
			this.fileAccesses.RemoveAll(p => p.path != this.SystemDirectory);
		}

		public bool IsPlangRooted(string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new Exception("path cannot be empty");
			}

			if (path.AdjustPathToOs().StartsWith(Path.DirectorySeparatorChar)) return true;
			return false;
		}

		public bool IsOsRooted(string path)
		{

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (Regex.IsMatch(path, "^[A-Z]{1}:", RegexOptions.IgnoreCase))
				{
					return true;
				}
			}
			else if (path.StartsWith("//"))
			{
				return true;
			}
			return false;
		}

		public string ValidatePath(string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new Exception("path cannot be empty");
			}

			if (fileAccesses == null)
			{
				throw new Exception("File access has not been initated. Call IPLangFileSystem.Init");
			}
			RootDirectory = RootDirectory.TrimEnd(Path.DirectorySeparatorChar);
			
			if (IsPlangRooted(path))
			{
				if (!path.StartsWith(RootDirectory))
				{
					path = Path.GetFullPath(Path.Join(RootDirectory, path));
				}
			}
			else if (IsOsRooted(path))
			{
				if (path.StartsWith("//"))
				{
					path = path.Substring(0, 1);
				}
			}
			else
			{
				path = Path.GetFullPath(Path.Join(RootDirectory, path));
				
			}

		
			if (!path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
			{
				var appName = RootDirectory ?? Path.DirectorySeparatorChar.ToString();

				if (fileAccesses.Count > 0)
				{
					var hasAccess = fileAccesses.FirstOrDefault(p => p.appName.ToLower() == appName.ToLower() && path.ToLower().StartsWith(p.path.ToLower())
						&& (p.expires > DateTime.UtcNow || p.ProcessId == Id));
					if (hasAccess != null) return path;
				}

				throw new FileAccessException(appName, path, $@"{appName} 

	is trying to access 

{path}

Do you accept that?

You can answer
- yes/y
- no/n
- always/a

or in more natural language, e.g. 
- yes for 30 days 
- yes forever

Your answer:
");
			}


			if (!IsPlangRooted(path) && !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
			{
				var osPath = Path.Join(AppContext.BaseDirectory, "os", path);
				if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
				{
					path = osPath;
				}
			}
			
			return path;
		}



		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IDirectory Directory { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IFile File { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IFileInfoFactory FileInfo { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IFileStreamFactory FileStream { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IPath Path { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IDirectoryInfoFactory DirectoryInfo { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IDriveInfoFactory DriveInfo { get; }
		[Newtonsoft.Json.JsonIgnore]
		[System.Text.Json.Serialization.JsonIgnore]
		/// <inheritdoc />
		public override IFileSystemWatcherFactory FileSystemWatcher { get; }

	}
}
