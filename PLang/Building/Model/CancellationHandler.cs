using PLang.Attributes;
using PLang.Models;

namespace PLang.Building.Model;

public class CancellationHandler
{
    [DefaultValueAttribute(30)] public long? CancelExecutionAfterXMilliseconds { get; set; } = 30;

    [DefaultValueAttribute(null)] public GoalToCall? GoalNameToCallAfterCancellation { get; set; } = null;
}