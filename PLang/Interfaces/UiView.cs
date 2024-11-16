namespace PLang.Interfaces;

public interface IUiView
{
    public SynchronizationContext SynchronizationContext { get; set; }
    Task Write(string text, string type = "text", int statusCode = 200, int goalNr = -1);
    Task Append(string cssSelector, string text, string type = "text", int statusCode = 200, int goalNr = -1);
    Task ExecuteCode(string content);
}