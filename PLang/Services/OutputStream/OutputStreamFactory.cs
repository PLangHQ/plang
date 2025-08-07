using LightInject;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static PLang.Modules.OutputModule.Program;

namespace PLang.Services.OutputStream
{
	public class OutputStreamFactory : BaseFactory, IOutputStreamFactory
	{
		private readonly PLangAppContext appContext;
		private IEngine engine;
		private string defaultType;
		private string currentType;

		public OutputStreamFactory(IServiceContainer container, IEngine engine, string defaultType) : base(container)
		{
			this.appContext = container.GetInstance<PLangAppContext>();
			this.engine = engine;
			this.defaultType = defaultType;
			this.currentType = defaultType;
		}

		public void SetEngine(IEngine engine)
		{
			this.engine = engine;
		}

		public IOutputStreamFactory SetContext(string? name)
		{
			if (string.IsNullOrEmpty(name))
			{
				appContext.Remove(ReservedKeywords.Inject_OutputStream);
				this.currentType = defaultType;

				return this;
			}
			this.currentType = name;
			appContext.AddOrReplace(ReservedKeywords.Inject_OutputStream, name);
			return this;
		}

		public IOutputStream CreateHandler(string? name = null)
		{
			if (name == null && engine.OutputStream != null)
			{
				return engine.OutputStream;
			}

			var serviceName = (name != null) ? name : currentType;

			var os = container.GetInstance<IOutputStream>(serviceName);
			return os;
		}
	}
}
