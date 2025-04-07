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
		private readonly bool isBuilder;

		public DbServiceFactory(ServiceContainer container, bool isBuilder = false) : base(container)
		{
			this.isBuilder = isBuilder;
		}

		public IDbConnection CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_IDbConnection);
			var connection = container.GetInstance<IDbConnection>(serviceName);

			var context = container.GetInstance<PLangAppContext>();
			var dataSource = context[ReservedKeywords.CurrentDataSource] as DataSource;
			if (dataSource != null)
			{
				if (dataSource.ConnectionString.Contains("%") && isBuilder)
				{
					connection.ConnectionString = $"Data Source={dataSource.Name};Mode=Memory;Cache=Shared;";
				}
				else if (dataSource.ConnectionString.Contains("%"))
				{
					var variableHelper = container.GetInstance<VariableHelper>();
					connection.ConnectionString = variableHelper.LoadVariables(dataSource.ConnectionString).ToString();
				} else
				{
					connection.ConnectionString = dataSource.ConnectionString;
				}
			} else
			{
				connection.ConnectionString = "./.db/data.sqlite";
			}

			return connection;
		}
	}
}
