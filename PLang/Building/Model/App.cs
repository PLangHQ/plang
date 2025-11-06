using PLang.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	// used by prParser
	public class RuntimeApp : App
	{
		public RuntimeApp(IPLangFileSystem fileSystem) : base(fileSystem)
		{
		}
	}

	// Used by goal parser
	public class BuilderApp : App
	{
		public BuilderApp(IPLangFileSystem fileSystem) : base(fileSystem)
		{
		}
	}


	public abstract class App
	{
		private readonly IPLangFileSystem fileSystem;

		public App(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
			Goals = new();
		}

		public string Name { get; set; }
		public string RelativePath { get; set; }
		public string AbsolutePath { get; set; }
		public List<Goal> Goals { get; set; }
		public IEnumerable<Goal> SetupGoals
		{
			get
			{
				return Goals.Where(p => p.IsSetup);
			}
		}

		public void Load()
		{

		}

	}
}
