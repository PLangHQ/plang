using LightInject;
using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.DbModule
{

	public interface IDbFactory
	{
		IDbConnection CreateHandler();
	}

	public class DbFactory : BaseFactory, IDbFactory
	{
		public DbFactory(IServiceContainer container) : base(container)
		{
		}

		public IDbConnection CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_IDbConnection);

			return container.GetInstance<IDbConnection>(serviceName);
		}
	}

}
