using LightInject;
using Microsoft.Data.Sqlite;
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

		public DbServiceFactory(IServiceContainer container, bool isBuilder = false) : base(container)
		{
			this.isBuilder = isBuilder;
		}

		public IDbConnection CreateHandler()
		{
			var context = container.GetInstance<PLangAppContext>();
			var dataSource = context[ReservedKeywords.CurrentDataSource] as DataSource;
			if (dataSource == null) throw new Exception("Could not find datasource in context");

			var connection = container.GetInstance<IDbConnection>(dataSource.TypeFullName);

			if (dataSource.TypeFullName != typeof(SqliteConnection).ToString())
			{
				connection.ConnectionString = dataSource.ConnectionString;
			}
			else
			{
				if (isBuilder)
				{
					connection.ConnectionString = $"Data Source={dataSource.Name};Mode=Memory;Cache=Shared;";
				}
				else if (dataSource.ConnectionString.Contains("%"))
				{
					var variableHelper = container.GetInstance<VariableHelper>();
					connection.ConnectionString = variableHelper.LoadVariables(dataSource.ConnectionString).ToString();
				}
				else
				{
					connection.ConnectionString = dataSource.ConnectionString;
				}
			}


			return connection;
		}
	}
}
