using LightInject;
using Microsoft.Data.Sqlite;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
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
		IDbConnection CreateHandler(DataSource dataSource, MemoryStack memoryStack, bool ReadOnly = false);
	}

	public class DbServiceFactory : BaseFactory, IDbServiceFactory
	{
		private readonly bool isBuilder;

		public DbServiceFactory(IServiceContainer container, bool isBuilder = false) : base(container)
		{
			this.isBuilder = isBuilder;
		}

		public IDbConnection CreateHandler(DataSource dataSource, MemoryStack memoryStack, bool ReadOnly = false)
		{
			if (dataSource == null) throw new Exception("Data source cannot be empty");

			IDbConnection connection;
			try
			{
				connection = container.GetInstance<IDbConnection>(dataSource.TypeFullName);
			}
			catch (Exception ex)
			{
				// Only sqlite is registered by plang itself; every other driver arrives through an
				// `inject db` step. The message from the container names a type and leaves the reader
				// to work out the rest, so say which datasource wanted it and where a driver comes from.
				//
				// A build hits this on setup goals in particular: setup is built before the events are
				// built and before the start of app events run, so an `inject db` written in an event
				// goal has not happened yet when a setup step asks for the driver.
				throw new Exception($"No driver is registered for datasource '{dataSource.Name}' ({dataSource.TypeFullName}). " +
					$"plang registers sqlite itself; any other database is registered by an `inject db` step, e.g. " +
					$"`- inject db, 'MySqlConnector.dll', global`." +
					(isBuilder ? " This is a build, and setup goals are built before event goals run, so an inject written in an event goal has not taken effect yet." : ""),
					ex);
			}

			if (dataSource.TypeFullName != typeof(SqliteConnection).ToString())
			{
				connection.ConnectionString = dataSource.ConnectionString;
			}
			else
			{
				if (isBuilder)
				{
					connection.ConnectionString = $"Data Source={dataSource.Name};Mode=Memory;Cache=Shared;Default Timeout=3600";
				}
				else if (dataSource.ConnectionString.Contains("%"))
				{
					var variableHelper = container.GetInstance<VariableHelper>();
					connection.ConnectionString = memoryStack.LoadVariables(dataSource.ConnectionString).ToString();
				}
				else
				{
					connection.ConnectionString = dataSource.ConnectionString;
					
				}

				if (ReadOnly && !isBuilder) {
					//connection.ConnectionString += ";Mode=ReadOnly";
				}
			}


			return connection;
		}
	}
}
