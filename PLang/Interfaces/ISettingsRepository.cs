using Newtonsoft.Json;
using PLang.Models;

namespace PLang.Interfaces
{

    public interface ISettingsRepository
    {
        public IEnumerable<Setting> GetSettings();
        public void Set(Setting setting);
        void Remove(Setting setting);
		void SetSharedDataSource(string? appId = null);
		Setting? Get(string? fullName, string? type, string? key);
		string SerializeSettings();
	}
}
