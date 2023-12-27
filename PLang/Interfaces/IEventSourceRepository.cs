using Dapper;
using PLang.Services.EventSourceService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;

namespace PLang.Interfaces
{
	public interface IEventSourceRepository
	{
		DataSource DataSource { get; set; } 
		Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters parameters);
		Task<List<SqliteEventSourceRepository.EventData>> GetUnprocessedData();
	}
}
