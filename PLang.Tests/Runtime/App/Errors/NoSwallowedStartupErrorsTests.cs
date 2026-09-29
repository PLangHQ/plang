namespace PLang.Tests.App.Errors;

// Startup doesn't hide what's wrong: a corrupt app.pr is an error naming the file, not a fresh identity.
public class NoSwallowedStartupErrorsTests
{
    [Test] public async Task ACorruptAppPr_IsAnErrorNamingIt()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-corrupt-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, ".build"));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, ".build", "app.pr"), "{ not json");
        try
        {
            await using var app = new global::app.@this(root).Testing();
            var loaded = await app.Load();
            await Assert.That(loaded.Error?.Key).IsEqualTo("AppIdentityUnreadable");
            await Assert.That(loaded.Error!.Message).Contains("app.pr");
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }
}
