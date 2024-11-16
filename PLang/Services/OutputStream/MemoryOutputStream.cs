using System.Text;

namespace PLang.Services.OutputStream;

public class MemoryOutputStream : MemoryStream, IOutputStream
{
    public Stream Stream => this;

    public Stream ErrorStream => new MemoryStream();

    public string ContentType => "application/octet-stream";

    public Task<string> Ask(string text, string type = "text", int statusCode = 200)
    {
        return null;
    }

    public string Read()
    {
        return Read();
    }

    public async Task Write(object? obj, string type = "text", int statusCode = 200)
    {
        if (obj == null) return;
        var bytes = Encoding.Default.GetBytes(obj.ToString());
        Write(bytes, 0, bytes.Length);
    }

    public async Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200)
    {
        if (obj == null) return;
        var bytes = Encoding.Default.GetBytes(obj.ToString());
        Write(bytes, 0, bytes.Length);
    }
}