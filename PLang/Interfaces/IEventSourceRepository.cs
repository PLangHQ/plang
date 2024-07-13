using Dapper;
using PLang.Services.EventSourceService;
using System.Data;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Interfaces
{
	public interface IEventSourceRepository
	{
		DataSource DataSource { get; set; } 
		Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters parameters, IDbTransaction? transaction = null);
		Task<int> AddEventSourceData(string data, string privateKey, IDbTransaction? transaction);
		Task<List<SqliteEventSourceRepository.EventData>> GetUnprocessedData();
	}
}
