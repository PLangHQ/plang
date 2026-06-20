using app.type.path;

namespace app;

// app boots. A program is a file. The file contains goals.
// Everything owned once, here.
class app(file.list files, goal.list goals, channel.list channels,
          identity.list identities, signing.list signers, llm.list llms,
          type.list types, error.list errors, warning.list warnings) {

    public async Task<data.@this> start(data.@this<path.@this> entry) =>
        await new file(entry).start();
}
