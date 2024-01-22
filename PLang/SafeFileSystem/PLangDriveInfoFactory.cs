using PLang.Interfaces;
using System.IO.Abstractions;

namespace PLang.SafeFileSystem
{
	[Serializable]
	internal class PLangDriveInfoFactory : IDriveInfoFactory
	{
		private readonly IPLangFileSystem fileSystem;

		public PLangDriveInfoFactory(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		/// <inheritdoc />
		public IFileSystem FileSystem
			=> fileSystem;

		/// <inheritdoc />
		public IDriveInfo[] GetDrives()
		{
			fileSystem.ValidatePath("All Drives");

			var driveInfos = DriveInfo.GetDrives();
			var driveInfoWrappers = new DriveInfoBase[driveInfos.Length];
			for (int index = 0; index < driveInfos.Length; index++)
			{
				var driveInfo = driveInfos[index];
				driveInfoWrappers[index] = new DriveInfoWrapper(fileSystem, driveInfo);
			}

			return driveInfoWrappers;
		}

		/// <inheritdoc />
		public IDriveInfo New(string driveName)
		{
			fileSystem.ValidatePath("Drive " + driveName);

			var realDriveInfo = new DriveInfo(driveName);
			return new DriveInfoWrapper(fileSystem, realDriveInfo);
		}

		/// <inheritdoc />
		public IDriveInfo? Wrap(DriveInfo? driveInfo)
		{
			if (driveInfo == null)
			{
				return null;
			}

			return new DriveInfoWrapper(fileSystem, driveInfo);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DriveInfoBase"/> class, which acts as a wrapper for a logical drive.
		/// </summary>
		/// <param name="driveName">A valid drive path or drive letter.</param>
		[Obsolete("Use `IDriveInfoFactory.New(string)` instead")]
		public IDriveInfo FromDriveName(string driveName)
		{
			return New(driveName);
		}
	}
}
