using System.Text.Json;
using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record save
{
    public virtual string path { get; init; } = null!;
    public virtual object value { get; init; } = null!;
}

public sealed partial class SaveHandler : BaseClass<save>
{

	/*
	 * LLM: this is wrong implementation, we should get IPLangFileSystem interface from handler, default is PlangFileSystem
	 * then others can define their own file systam
	 * I think path is something we use a lot, so maybe we can create Path class, so when set Value is the path, but it 
	 * will give us, path.AbsolutePath, path.DirName
	 * 
	 * also when saving, we are not doing serialization here, just sending the object to writer, write will take engine.Serializer and serialize
	 * the data
	 * 
	 * and who says it is WriteAllTextAsync, why text, we should just be writing the bytes
	 * */
	protected override async Task<Return> ExecuteAsync(save? p)
    {
        if (p == null || string.IsNullOrEmpty(p.path))
            return Error("Path is required");

        if (p.value == null)
            return Error("Value is required");

        try
        {
            var absPath = FileSystem.Path.GetFullPath(p.path);
            var dir = FileSystem.Path.GetDirectoryName(absPath);

            if (!string.IsNullOrEmpty(dir) && !FileSystem.Directory.Exists(dir))
            {
                FileSystem.Directory.CreateDirectory(dir);
            }

            string text;
            if (p.value is string strContent)
            {
                text = strContent;
            }
            else
            {
                text = JsonSerializer.Serialize(p.value, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
            }

            await FileSystem.File.WriteAllTextAsync(absPath, text);

            return Success(new { Path = absPath });
        }
        catch (Exception ex)
        {
            return Error($"Failed to save file: {ex.Message}");
        }
    }
}
