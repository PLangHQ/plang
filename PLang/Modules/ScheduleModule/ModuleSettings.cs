using PLang.Interfaces;
using static PLang.Modules.ScheduleModule.Program;

namespace PLang.Modules.ScheduleModule;

public class ModuleSettings : IModuleSettings
{
    private readonly ISettings settings;

    public ModuleSettings(ISettings settings)
    {
        this.settings = settings;
    }

    public List<CronJob> GetCronJobs()
    {
        return settings.GetValues<CronJob>(typeof(ModuleSettings)).Where(p => !p.IsArchived).ToList();
    }

    public void SetCronJobAsArchived(string cronCommand, string goalName)
    {
        var cronJobs = GetCronJobs();
        var cronJob = cronJobs.FirstOrDefault(p => p.CronCommand == cronCommand && p.GoalName == goalName);
        if (cronJob == null) return;

        cronJob.IsArchived = true;
        settings.SetList(GetType(), cronJobs);
    }
}