using LightInject;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Utils;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace PLang.Modules
{
	internal class SettingsLoader
	{
		public static void Load()
		{
			/*
			var Settings = PLanguage.GetSettings();
			var fileSystem = Instance.Container.GetInstance<IPLangFileSystem>();
			string filePath = null;
			if (fileSystem.File.Exists(Path.Combine(Settings.BuildPath, "Settings.pr")))
			{
				filePath = Path.Combine(Settings.BuildPath, "Settings.pr");
			}
			else
			{
				string startFolderPath = Path.Combine(Settings.BuildPath, "start");
				if (fileSystem.File.Exists(Path.Combine(startFolderPath, "Settings.pr")))
				{
					filePath = Path.Combine(startFolderPath, "Settings.pr");
				}
			}

			var configs = JsonHelper.ParseFilePath<List<SettingConfiguration>>(fileSystem, filePath) ?? new List<SettingConfiguration>();

			//go throw each config, try to find module, then find all settings in that module and try to match it with setting
			configs.ForEach(p => Settings.Set(p.Name, p.Value));
			*/
		}
	}
}
