using LightInject;
using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Services.DbService
{
	public interface IDbServiceFactory
	{
		IDbConnection CreateHandler();
	}

	public class DbServiceFactory : BaseFactory, IDbServiceFactory
	{

		public DbServiceFactory(ServiceContainer container) : base(container)
		{
		}

		public IDbConnection CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_IDbConnection);
			var connection = container.GetInstance<IDbConnection>(serviceName);

			var context = container.GetInstance<PLangAppContext>();
			var dataSource = context[ReservedKeywords.CurrentDataSource] as DataSource;
			if (dataSource != null)
			{
				connection.ConnectionString = dataSource.ConnectionString;
			} else
			{
				connection.ConnectionString = "./.db/data.sqlite";
			}

			return connection;
		}
	}
}
