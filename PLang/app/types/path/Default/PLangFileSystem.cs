using app.types.path;
using app.types.path.Default;
using app.Utils;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Websocket.Client.Logging;

namespace app.types.path.Default
{

	public record FileAccessControl(string appName, string path, DateTime? expires = null, string? ProcessId = null);


	//[Serializable]
	public sealed class PLangFileSystem : System.IO.Abstractions.FileSystem, IPLangFileSystem
	{
		public string Id { get; init; }

		private string rootPath;
		List<FileAccessControl> fileAccesses = new();

		public bool IsRootApp { get; private set; }
		public string RelativeAppPath { get; set; }
		public string OsDirectory
		{
			get
			{
				// The os symlink at the binary dir maps to /workspace/plang/os in dev,
				// /<install>/os in prod. PLang resolves /os/X paths against this directory.
				return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "os"));
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
			this.rootPath = System.IO.Path.GetFullPath(appStartupPath).AdjustPathToOs();
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
			fileAccesses.Add(new FileAccessControl(RootDirectory, OsDirectory, ProcessId: Id));

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
			this.fileAccesses.RemoveAll(p => p.path != this.OsDirectory);
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
			
			// Order matters: OS-rooted (//tmp/X on Unix, C:\... on Windows) is the
			// "escape the root" form; PLang-rooted (single leading /) gets root-
			// prefixed. IsPlangRooted matches the OS-rooted form too (both start
			// with /), so check IsOsRooted FIRST and PRESERVE the // prefix — that
			// way subsequent ValidatePath calls (PLangFile wraps every File.X call
			// with another ValidatePath) keep recognising the path as OS-rooted.
			// System.IO normalises "//tmp/X" → "/tmp/X" on Linux at the IO boundary.
			if (IsOsRooted(path))
			{
				// no-op: leave // prefix intact for idempotency. Permission.Authorize
				// gates these out-of-root accesses; System.IO handles normalisation.
			}
			else if (IsPlangRooted(path))
			{
				if (!path.StartsWith(RootDirectory)
					&& !path.StartsWith(OsDirectory))
				{
					var resolved = Path.GetFullPath(Path.Join(RootDirectory, path));

					// When path starts with /system/, fall back to <OsDirectory>/system/ if not
					// found in RootDirectory. The system folder lives under os/, but path strings
					// keep the /system/ form — the rename is a disk-layout change, not a syntax change.
					var sysPrefix = Path.DirectorySeparatorChar + "system" + Path.DirectorySeparatorChar;
					if (path.AdjustPathToOs().StartsWith(sysPrefix, StringComparison.OrdinalIgnoreCase)
						&& !System.IO.File.Exists(resolved) && !System.IO.Directory.Exists(resolved))
					{
						var afterPrefix = path.AdjustPathToOs().Substring(sysPrefix.Length);
						var osResolved = Path.GetFullPath(Path.Join(OsDirectory, "system", afterPrefix));
						if (System.IO.File.Exists(osResolved) || System.IO.Directory.Exists(osResolved))
						{
							resolved = osResolved;
						}
					}

					path = resolved;
				}
			}
			else
			{
				path = Path.GetFullPath(Path.Join(RootDirectory, path));
				
			}

		
			// Out-of-root access used to throw UnauthorizedAccessException here,
			// gated by the fileAccesses list. That model is replaced by
			// Path.Authorize (filesystem-permission branch) — every file action
			// handler calls Authorize first, which prompts + signs + stores via
			// Actor.Permission. ValidatePath now just normalises the path; gating
			// is Authorize's responsibility.
			if (!path.StartsWith(RootDirectory, global::app.types.path.@this.RootComparison))
			{
				return path;
			}


			if (!IsPlangRooted(path) && !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
			{
				var relativePath = path.Replace(rootPath, "").TrimStart(Path.DirectorySeparatorChar);

				var osPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, relativePath));
				if (System.IO.File.Exists(osPath) || System.IO.Directory.Exists(path))
				{
					path = osPath;
				}
				else
				{
					var systemPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, relativePath));
					if (System.IO.File.Exists(systemPath) || System.IO.Directory.Exists(systemPath))
					{
						path = systemPath;
					}
				}
			}

			// Fall back to <OsDirectory>/system/ for paths under RootDirectory/system/ that don't exist
			var rootSystemDir = RootDirectory + Path.DirectorySeparatorChar + "system" + Path.DirectorySeparatorChar;
			if (path.StartsWith(rootSystemDir, StringComparison.OrdinalIgnoreCase)
				&& !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
			{
				var afterSystem = path.Substring(rootSystemDir.Length);
				var osFallback = Path.GetFullPath(Path.Join(OsDirectory, "system", afterSystem));
				if (System.IO.File.Exists(osFallback) || System.IO.Directory.Exists(osFallback))
				{
					path = osFallback;
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
