using LightInject;
using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	public class OutputStreamFactory : BaseFactory, IOutputStreamFactory
	{
		private readonly PLangAppContext appContext;
		private string defaultType;
		private string currentType;
		private IOutputStream? outputStream;

		public OutputStreamFactory(IServiceContainer container, string defaultType) : base(container)
		{
			this.appContext = container.GetInstance<PLangAppContext>();
			this.defaultType = defaultType;
			this.currentType = defaultType;
		}
		public void SetOutputStream(IOutputStream outputStream)
		{
			this.outputStream = outputStream;
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
			if (name == null && outputStream != null) return outputStream;

			var serviceName = (name != null) ? name : currentType;

			return container.GetInstance<IOutputStream>(serviceName);
		}
	}
}
