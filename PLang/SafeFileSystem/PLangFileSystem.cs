﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Utils;
using System.IO.Abstractions;
using Websocket.Client.Logging;

namespace PLang.SafeFileSystem
{

	public record FileAccessControl(string appName, string path, DateTime? expires);


	//[Serializable]
	public sealed class PLangFileSystem : FileSystem, IPLangFileSystem
	{
		List<FileAccessControl>? fileAccesses = null;
		public bool IsRootApp { get; private set; }
		public string RelativeAppPath { get; private set; }

		public string RootDirectory { get; private set; }
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
			
			this.RootDirectory = appStartupPath.AdjustPathToOs();			
			this.RelativeAppPath = relativeAppPath.AdjustPathToOs();
			this.fileAccesses = new List<FileAccessControl>();

			DriveInfo = new PLangDriveInfoFactory(this);
			DirectoryInfo = new PLangDirectoryInfoFactory(this);
			FileInfo = new PLangFileInfoFactory(this);
			Path = new PLangPath(this);
			File = new PLangFile(this);
			Directory = new PLangDirectoryWrapper(this);
			FileStream = new PLangFileStreamFactory(this);
			FileSystemWatcher = new PLangFileSystemWatcherFactory(this);

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
				this.SharedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "plang");
				if (string.IsNullOrEmpty(SharedPath) || SharedPath == "plang")
				{
					SharedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "plang");
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

		// This is a security issue, here anybody can set what ever file access.
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
			if (fileAccesses == null) return;

			this.fileAccesses = fileAccesses;
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
			if (!Path.IsPathRooted(path))
			{
				path = Path.GetFullPath(Path.Join(RootDirectory, path));
			}
			RootDirectory = RootDirectory.TrimEnd(Path.DirectorySeparatorChar);
			path = Path.GetFullPath(path);
			if (!path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
			{
				var appName = RootDirectory ?? Path.DirectorySeparatorChar.ToString();

				if (fileAccesses.Count > 0)
				{
					var hasAccess = fileAccesses.FirstOrDefault(p => p.appName.ToLower() == appName.ToLower() && path.ToLower().StartsWith(p.path.ToLower()) && p.expires > DateTime.UtcNow);
					if (hasAccess != null) return path;
				}

				throw new FileAccessException(appName, path, $@"{appName} 

	is trying to access 

{path}

Do you accept that?

You can answer
- yes/y
- no/n

or in more natural language, e.g. 
- yes for 30 days 
- yes forever

Your answer:
");
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
