using LightInject;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.SafeFileSystem
{

	public interface IPLangFileSystemFactory
	{
		IPLangFileSystem CreateHandler();
	}

	public class PlangFileSystemFactory : BaseFactory, IPLangFileSystemFactory
	{
		public PlangFileSystemFactory(IServiceContainer container) : base(container)
		{
		}

		public IPLangFileSystem CreateHandler()
		{
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			if (!fileSystem.RootDirectory.Equals(fileSystem.SystemDirectory)) return fileSystem;

			var parentEngine = container.GetInstance<IEngine>("ParentEngine");
			if (parentEngine == null) throw new Exception($"Did not expect ParentEngine to be null.{ErrorReporting.CreateIssueShouldNotHappen}");

			return new PLangFileSystem(parentEngine.Path, "/", container.GetInstance<PLangAppContext>());

		}
	}

}
