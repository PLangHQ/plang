using Dapper;
using PLang.Errors;
using PLang.Services.EventSourceService;
using System.Data;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Interfaces
{
	public interface IEventSourceRepository
	{
		Task<long> Add(IDbConnection dbConnection, string sql, DynamicParameters? parameters, IDbTransaction? transaction = null, bool returnId = false);

	}
}
