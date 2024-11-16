using PLang.Events;

namespace PLang.Exceptions;

public class RuntimeEventException : Exception
{
    private EventBinding? eventModel;

    public RuntimeEventException(string message, EventBinding? eventModel = null) : base(message)
    {
        this.eventModel = eventModel;
    }
}