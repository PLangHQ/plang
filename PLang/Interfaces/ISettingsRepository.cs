using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Models;

namespace PLang.Interfaces
{
	
    public interface ISettingsRepository
    {
        public void Init();
        public IEnumerable<Setting> GetSettings();
        public void Set(Setting setting);
        void Remove(Setting setting);
		LlmRequest? GetLlmRequestCache(string hash);
		void SetLlmRequestCache(string hash, LlmRequest question);
		void UseSharedDataSource(bool activateSharedDataSource = false);
	}
}
