using Dapper;
using PLang.Errors;
using PLang.Services.EventSourceService;
using System.Data;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Interfaces
{
	public interface IEventSourceRepository
	{
		DataSource DataSource { get; set; } 
		Task<int> Add(IDbConnection dbConnection, string sql, DynamicParameters parameters, IDbTransaction? transaction = null);
		Task<(int, IError?)> AddEventSourceData(IDbConnection dbConnection,long id, string data, string keyHash, IDbTransaction? transaction);
		Task<List<SqliteEventSourceRepository.EventData>> GetUnprocessedData();
	}
}
